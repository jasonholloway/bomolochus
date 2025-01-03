using System.Collections.Immutable;
using Bomolochus;
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
        var fibre = new Fibre([], [], parseContext, parser);
        var frames = new Stack<Fibre>([fibre]);
        
        ContinueLoop:

        while (frames.TryPop(out var x))
        {
            var step = x.Step;
            
            if (x is { ParseContext.Strength: var strength } 
                && step.Info.RequiresStrength is int requiredStrength)
            {
                if (strength < requiredStrength)
                {
                    continue;
                }
            }
                    
            if (step is ICacheableStep c)
            {
                if (x.Continuations.TryGetValue(c, out var cell))
                {
                    cell.AddContinuation(next =>
                    {
                        x = x with
                        {
                            ParseContext = next.Context //will be forked below
                        };
                        
                        foreach (var s in next.Steps)
                        {
                            frames.Push(x.Fork(s));
                        }
                    });
                    
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
                        x.Continuations[cs] = cell;
                    }
                    
                    var left = s.Left ?? Step.From(false);

                    x = x with
                    {
                        Binds = x.Binds.Push(new BindFrame.Started(s, cell))
                    };

                    frames.Push(x with { Step = left }); //default step fills in when left leg is empty for convenience

                    continue;
                }
                
                case IReturnStep s:
                {
                    var split = x.ParseContext.Text.Split();

                    if (!split.IsEmpty)
                    {
                        x = x with { Continuations = [] };
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

                            foreach (var nextStep in next.Steps)
                            {
                                frames.Push(x2.Fork(nextStep));
                            }
                            
                            break;
                        }
                        
                        case BindFrame.Completing(var start, var leftParsed):
                        {
                            start.Cell?.Emit(Next.From(x.ParseContext.Fork(), [s]));
                            
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
    }

    record FibreStep(Fibre Context, IStep Step);





    abstract record BindFrame(IBindStep Bind)
    {
        public record Started(IBindStep Bind, ContinuationCell? Cell = null, int? OrigPrecedence = null) : BindFrame(Bind)
        {
            public override string ToString()
                => $"Started({Bind}, {Cell})";
        }

        public record Completing(Started Info, Parsing ParsedLeft) : BindFrame(Info.Bind)
        {
            public override string ToString()
                => $"Completing({Info.Bind}, {ParsedLeft})";
        }
    }

    private record Fibre(
        ImmutableStack<BindFrame> Binds, 
        Dictionary<ICacheableStep, ContinuationCell> Continuations,
        ParserOps.ParseContext ParseContext,
        IStep Step
        )
    {
        public Fibre Fork(IStep step)
            => this with { ParseContext = ParseContext.Fork(), Step = step };

        public override string ToString() => ParseContext.ToString();
    }

    private class ContinuationCell(ICacheableStep origin)
    {
        public readonly ICacheableStep Origin = origin;

        private readonly List<INext> _results = new();
        private readonly List<Action<INext>> _continuations = new();

        public void Emit(INext next)
        {
            _results.Add(next);
            
            foreach (var fn in _continuations)
            {
                fn(next);
            }
        }

        public void AddContinuation(Action<INext> continuation)
        {
            _continuations.Add(continuation);

            foreach (var next in _results)
            {
                continuation(next);
            }
        }
    }
}