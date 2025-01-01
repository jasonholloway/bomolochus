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
            new Frame.RunStep(new RunContext([], [], parseContext), parser)
        ]);
        
        ContinueLoop:
        
        /* A | B is being parsed as a complete expression
         * which is then happily fed into Conjunction
         *
         * but - the strength isn't communicated to the continuation 
         * if we've parsed A|B, this result is too rich for Conjunction
         *
         * Conjunction can consume subexpressions
         * with only relatively weak composite strengths
         * eg single values, or additions
         *
         * these strengths are accumulated upwards
         * rather than the power of parsing propagating downwards
         * we have both directions in play
         *
         * composite strengths would be sufficient to work
         * but they would require us to evaluate all decontextualised possibilities per step
         * which is plainly wasteful
         * there must always be a need for a particular parsing
         * hence, propagation required
         *
         * the LHS Exp should itself have a propagated strength
         * ie an addition can only combine weakly-parsed expressions on both of its legs
         *
         * so we start parsing the addition,
         * but if we constrain it, we can't cache it
         * given a site can have multiple results
         * we can filter out too-strong parsings
         * ie on our continuation, we filter out those that our operator can't contain
         * this is what we are currently missing
         *
         * and as we parse forwards,
         * we constrain the parsing to only try steps that are possible
         * caching is still in place, but there is _always_ a strength in play for each site
         * 
         * an additional requirement: 
         * we need to specify strength on both legs then
         * yet more syntactical detritus
         * or - not if we use DelimitedList... we get it for free
         *
         * so all we need is to read the RequiredStrength of a continuation bind
         */

        while (TryGetNextStep(out var x, out var step))
        {
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
                if (x.StepCache.TryGetValue(c, out var cell))
                {
                    cell.AddContinuation(next =>
                    {
                        x = x with
                        {
                            ParseContext = next.Context //will be forked below
                        };
                        
                        foreach (var s in next.Steps)
                        {
                            frames.Push(new Frame.RunStep(x.Fork(), s));
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
                        x.StepCache[cs] = cell;
                    }
                    
                    var left = s.Left ?? Step.From(false);

                    x = x with
                    {
                        Binds = x.Binds.Push(new BindFrame.Started(s, cell))
                    };

                    frames.Push(new Frame.RunStep(x, left)); //default step fills in when left leg is empty for convenience

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

                            foreach (var nextStep in next.Steps)
                            {
                                frames.Push(new Frame.RunStep(x2.Fork(), nextStep));
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
        

        bool TryGetNextStep(out RunContext context, out IStep step)
        {
            while (frames.TryPop(out var frame))
            {
                switch (frame)
                {
                    // case Frame.RunSteps(_, []): continue;

                    case Frame.RunStep(var x, var s):
                    {
                        context = x;
                        step = s;
                        return true;
                    }

                    // case Frame.RunSteps(var x, [var s, .. var alternatives]):
                    // {
                    //     context = x;
                    //     step = s;
                    //     frames.Push(new Frame.RunSteps(x.Fork(), alternatives));
                    //     return true;
                    // }

                    case Frame.RunContinuations:
                    {
                        break;
                    }
                }
            }

            context = default!;
            step = default!;
            return false;
        }
    }
    
    abstract record Frame
    {
        public record RunStep(RunContext Context, IStep Step) : Frame;
        public record RunContinuations() : Frame;
    }








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

    private record RunContext(
        ImmutableStack<BindFrame> Binds, 
        Dictionary<ICacheableStep, ContinuationCell> StepCache,
        ParserOps.ParseContext ParseContext)
    {
        public RunContext Fork()
            => this with { ParseContext = ParseContext.Fork() };

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