using System.Collections.Immutable;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Bomolochus.Text;

namespace Bomolochus.Runner;

public static class ParserRunner
{
    public static Parsed<V> Parse<V>(this IStep<V> parser, Readable text)
        where V : Parsable
        => Parse(parser,
            new ParserOps.Context(
                TextSplitter.Create(text), 
                SpaceChars: [' ', '\t', '\n'], 
                SpaceParsable: true,
                CertaintyThreshold: 1
            ));

    //todo below should slough off frames given progress
    //and how would we know which ones we can get rid of?
    
    public static Parsed<V> Parse<V>(this IStep<V> parser, ParserOps.Context parseContext)
        where V : Parsable
    {
        var frames = new Stack<Frame>(
        [
            new Frame(new RunContext([], [], parseContext), [parser])
        ]);

        while (TryGetNextStep(out var x, out var step))
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
                
                x = x with { ParseContext = x.ParseContext with { SpaceParsable = false } };
            }
            
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
                                        Binds = x.Binds.Push(new BindFrame.Started(s, space)) //TODO Uncertain about provenance of this space... !!!!!
                                    };
                                    
                                    Debug.Assert(next.Steps.All(s => s is IReturnStep));
                                    frames.Push(new(x, next.Steps));
                                    break;
                                
                                case { Next: null }:
                                    //cell is pending, we must be recursing - register our continuation
                                    //in the form of extra bind context to be unwound on cell completion
                                    cell.ExtraBinds.Add(x.Binds.Push(new BindFrame.Started(s, space)));
                                    break;
                            }
                        }
                        else
                        {
                            //left is cacheable but not yet cached - push Started frame with shared cell
                            x.StepCache[left] = cell = new StepCacheCell();
                            
                            x = x with { Binds = x.Binds.Push(new BindFrame.Started(s, space, cell)) };
                            frames.Push(new(x, [s.Left ?? Step.From(false)]));
                        }
                    }
                    else
                    {
                        //left not cacheable, process normally
                        x = x with { Binds = x.Binds.Push(new BindFrame.Started(s, space)) };
                        frames.Push(new(x, [s.Left ?? Step.From(false)])); //default step fills in when left leg is empty for convenience
                    }

                    continue;
                }
                
                case IReturnStep s:
                {
                    var parsed = Parsing.From(s.Value, x.ParseContext.Text.Split());
                    
                    if (space != null)
                    {
                        parsed = Parsing.From(s.Value!, [space, parsed]);
                    }
                    
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
                        case BindFrame.Started { Bind: var bind, Prefix: var prefix, Cell: var cell }:
                        {
                            if (cell is { ExtraBinds: { } extraBinds })
                            {
                                foreach (var extraBindStack in extraBinds)
                                {
                                    frames.Push(new(
                                        x.Fork() with { Binds = extraBindStack }, 
                                        [s]
                                    ));
                                }
                            }

                            var next = bind.Right(s.Value)(x.ParseContext);

                            var x2 = x with
                            {
                                ParseContext = next.Context,
                                Binds = x.Binds.Push(
                                    new BindFrame.Completing(
                                        bind, 
                                        prefix != null ? Parsing.From(parsed.Val, [prefix, parsed]) : parsed) //seems ugly like
                                    ),
                                StepCache = []
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
        public record Started(IBindStep Bind, Parsing? Prefix, StepCacheCell? Cell = null) : BindFrame(Bind);
        public record Completing(IBindStep Bind, Parsing ParsedLeft) : BindFrame(Bind);
    }
    

    private record RunContext(
        ImmutableStack<BindFrame> Binds, 
        Dictionary<ICacheableStep, StepCacheCell> StepCache,
        ParserOps.Context ParseContext)
    {
        public RunContext Fork()
            => this with { ParseContext = ParseContext.Fork() };
    }
    //todo some kind of RecreateCache method
    //to be run after processing

    private class StepCacheCell
    {
        public readonly List<ImmutableStack<BindFrame>> ExtraBinds = []; 
        public INext? Next = null;
    }
    
    
    
    
    
    
    /* the StepCache is a mutable thing
     * as it needs to be shared magically across Forks
     * but it needs to be recreated pristinely after every step of progress
     *
     * also - it doesn't store a result
     * it stores a hookable represenetation of the current computation
     * ie, if we find it populated, then we can register our own continuation against it
     */
    
    
    
    
            // if (x.SpaceParsable 
            //     && x.Text.ReadCharsWhile(x.SpaceChars.Contains) > 0)
            // {
            //     var space = x.Text.Split();
            //
            //     return Parse(x with { SpaceParsable = false })?
            //         .SelectMany(r => Out(r.Map(t => 
            //             (
            //                 t.Context with { SpaceParsable = true, SpaceChars = x0.SpaceChars }, 
            //                 Parsing.From(
            //                     t.Parsing!.Val, 
            //                     [new ParsingText<Readable>(space.Readable, space, true), t.Parsing]
            //                     )
            //             ))));
            // }
            //
            // return Parse(x)?
            //     .SelectMany(r => Out(r.Map(t => 
            //         (
            //             t.Context with { SpaceParsable = true, SpaceChars = x0.SpaceChars }, 
            //             t.Parsing
            //         ))));
}