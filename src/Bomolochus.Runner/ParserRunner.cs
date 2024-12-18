using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus.Runner;

public static class ParserRunner
{
    public static Parsed<V> Parse<V>(this IStep<V> parser, Readable text)
        where V : Parsable
        => Parse(parser,
            new ParserOps.ParseContext(
                TextSplitter.Create(text), 
                SpaceChars: [' ', '\t', '\n'], 
                SpaceParsable: true,
                CertaintyThreshold: 1
            ));

    //todo below should slough off frames given progress
    //and how would we know which ones we can get rid of?
    //answer: threads
    
    public static Parsed<V> Parse<V>(this IStep<V> parser, ParserOps.ParseContext parseContext)
        where V : Parsable
    {
        var frames = new Stack<Frame>(
        [
            new Frame(new RunContext([], [], parseContext), [parser])
        ]);
        
        ContinueLoop:

        while (TryGetNextStep(out var x, out var step))
        {
            if (step is ICacheableStep c)
            {
                if (x.StepCache.TryGetValue(c, out var cell))
                {
                    switch (cell)
                    {
                        case { Next: {} next }:
                            //cell is complete, continue from its results
                            x = x with
                            {
                                ParseContext = next.Context.Fork(),
                            };
                            
                            frames.Push(new(x, next.Steps));
                            break;
                        
                        case { Next: null }:
                            //cell is pending, we must be recursing - register our continuation
                            cell.ExtraBinds.Add(x.Binds);
                            break;
                    }
                    
                    goto ContinueLoop;
                }
            }
            
            switch (step)
            {
                case IBindStep s:
                {
                    ContinuationCell? cell = null;

                    if (s is ICacheableStep cs)
                    {
                        cell = new ContinuationCell(cs);
                        x.StepCache[cs] = cell;
                    }
                    
                    x = x with
                    {
                        Binds = x.Binds.Push(new BindFrame.Started(s, cell))
                    };
                    
                    frames.Push(new(x, [s.Left ?? Step.From(false)])); //default step fills in when left leg is empty for convenience

                    continue;
                }
                
                case IReturnStep s:
                {
                    var split = x.ParseContext.Text.Split();

                    if (!split.IsEmpty)
                    {
                        x = x with { StepCache = [] };
                    }
                    
                    var parsed = Parsing.From(s.Value, split);
                    
                    x = x with { ParseContext = x.ParseContext with { SpaceParsable = true }};

                    UnwindBinds:

                    if (x.Binds.IsEmpty)
                    {
                        if (x.ParseContext.Text.IsEmpty)
                        {
                            //won't below play hell with completer?
                            return parsed.MapValue(o => (V)o!).Complete();
                        }
                        
                        continue;
                    }

                    x = x with { Binds = x.Binds.Pop(out var bindFrame) };

                    switch (bindFrame)
                    {
                        case BindFrame.Started { Bind: var bind } start:
                        {
                            Parsing<Readable>? space = null;

                            if (x.ParseContext.SpaceParsable)
                            {
                                var spaceChars = x.ParseContext.SpaceChars
                                    .Union(step?.Info?.Spacing?.SpaceChars ?? [])
                                    .Except(step?.Info?.Spacing?.NonSpaceChars ?? []);
                                
                                if (x.ParseContext.Text.ReadCharsWhile(spaceChars.Contains) > 0)
                                {
                                    var text = x.ParseContext.Text.Split();
                                    space = new ParsingText<Readable>(text.Readable, text, true);
                                }
                                
                                //todo could just have a mutable prop on the below...
                                x = x with { ParseContext = x.ParseContext with { SpaceParsable = false } };
                            }

                            var next = bind.Right(s.Value)(x.ParseContext);

                            var x2 = x with
                            {
                                ParseContext = next.Context,
                                Binds = x.Binds.Push(
                                    new BindFrame.Completing(
                                        start, 
                                        space != null ? Parsing.From(parsed.Val, [space, parsed]) : parsed)
                                    )
                            };
                            
                            frames.Push(new(x2, next.Steps));
                            
                            break;
                        }
                        
                        case BindFrame.Completing(var start, var leftParsed):
                        {
                            if (start.Cell is { ExtraBinds: var extraBinds })
                            {
                                start.Cell.Next = Next.From(x.ParseContext.Fork(), [s]);
                                
                                foreach (var extraBindStack in extraBinds)
                                {
                                    frames.Push(new(
                                        x.Fork() with { Binds = extraBindStack }, 
                                        [s]
                                    ));
                                }
                            }
                            
                            parsed = Parsing.From(s.Value, [leftParsed, parsed]);
                            goto UnwindBinds;
                        }
                    }
                    
                    continue;
                }
                
                default: throw new NotImplementedException();
            }
        }

        return default!;
        

        bool TryGetNextStep(out RunContext context, out IStep step)
        {
            while (frames.TryPop(out var frame))
            {
                switch (frame.Steps)
                {
                    case []: continue;

                    case [var s]:
                    {
                        context = frame.Context;
                        step = s;
                        return true;
                    }

                    case [var s, ..var alternatives]:
                    {
                        frames.Push(new(frame.Context.Fork(), alternatives));
                        context = frame.Context;
                        step = s;
                        return true;
                    }
                }
            }

            context = default!;
            step = default!;
            return false;
        }
    }

    private record Frame(RunContext Context, IStep[] Steps);

    abstract record BindFrame(IBindStep Bind)
    {
        public record Started(IBindStep Bind, ContinuationCell? Cell = null) : BindFrame(Bind)
        {
            public override string ToString()
                => $"Started({Bind}, {Cell?.ExtraBinds.Count ?? 0})";
        }

        public record Completing(Started Info, Parsing ParsedLeft) : BindFrame(Info.Bind)
        {
            public override string ToString()
                => $"Completing({Info.Bind}, {ParsedLeft})";
        }
    }

    private record RunContext(
        ImmutableStack<BindFrame> Binds, 
        Dictionary<ICacheableStep, ContinuationCell> StepCache,
        ParserOps.ParseContext ParseContext)
    {
        public RunContext Fork()
            => this with { ParseContext = ParseContext.Fork() };
    }

    private class ContinuationCell(ICacheableStep Step)
    {
        public readonly ICacheableStep Step = Step;
        public readonly List<ImmutableStack<BindFrame>> ExtraBinds = []; 
        public INext? Next = null;
    }
}