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

        while (TryGetNextStep(out var x, out var step))
        {
            switch (step)
            {
                case IBindStep s:
                {
                    //the cache below is crucial: it makes left-recursion possible
                    if (s.Left is ICacheableStep left)
                    {
                        if (x.StepCache.TryGetValue(left, out var cell))
                        {
                            switch (cell)
                            {
                                case { Next: {} next }:
                                    //cell is complete, continue from its results
                                    x = x with
                                    {
                                        ParseContext = next.Context,
                                        Binds = x.Binds.Push(new BindFrame.Started(s)) //TODO Uncertain about provenance of this space... !!!!!
                                    };
                                    
                                    frames.Push(new(x, next.Steps));
                                    break;
                                
                                case { Next: null }:
                                    //cell is pending, we must be recursing - register our continuation
                                    //in the form of extra bind context to be unwound on cell completion
                                    cell.ExtraBinds.Add(x.Binds.Push(new BindFrame.Started(s)));
                                    break;
                            }
                        }
                        else
                        {
                            //left is cacheable but not yet cached - push Started frame with shared cell
                            x.StepCache[left] = cell = new StepCacheCell();
                            
                            x = x with { Binds = x.Binds.Push(new BindFrame.Started(s, cell)) };
                            frames.Push(new(x, [s.Left ?? Step.From(false)]));
                        }
                    }
                    else
                    {
                        //left not cacheable, process normally
                        x = x with { Binds = x.Binds.Push(new BindFrame.Started(s, null)) };
                        frames.Push(new(x, [s.Left ?? Step.From(false)])); //default step fills in when left leg is empty for convenience
                    }

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
                        case BindFrame.Started { Bind: var bind, Cell: var cell }:
                        {
                            if (cell is { ExtraBinds: var extraBinds })
                            {
                                cell.Next = Next.From(x.ParseContext.Fork(), [s]);
                                
                                foreach (var extraBindStack in extraBinds)
                                {
                                    frames.Push(new(
                                        x.Fork() with { Binds = extraBindStack }, 
                                        [s]
                                    ));
                                }
                            }
                            
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
                                
                                x = x with { ParseContext = x.ParseContext with { SpaceParsable = false } };
                            }

                            var next = bind.Right(s.Value)(x.ParseContext);

                            var x2 = x with
                            {
                                ParseContext = next.Context,
                                Binds = x.Binds.Push(
                                    new BindFrame.Completing(
                                        bind, 
                                        space != null ? Parsing.From(parsed.Val, [space, parsed]) : parsed)
                                    )
                            };
                            
                            frames.Push(new(x2, next.Steps));
                            
                            break;
                        }
                        
                        case BindFrame.Completing(_, var leftParsed):
                        {
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
        public record Started(IBindStep Bind, StepCacheCell? Cell = null) : BindFrame(Bind);
        public record Completing(IBindStep Bind, Parsing ParsedLeft) : BindFrame(Bind);
    }

    private record RunContext(
        ImmutableStack<BindFrame> Binds, 
        Dictionary<ICacheableStep, StepCacheCell> StepCache,
        ParserOps.ParseContext ParseContext)
    {
        public RunContext Fork()
            => this with { ParseContext = ParseContext.Fork() };
    }

    private class StepCacheCell
    {
        public readonly List<ImmutableStack<BindFrame>> ExtraBinds = []; 
        public INext? Next = null;
    }
}