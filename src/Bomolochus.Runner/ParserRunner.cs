using System.Collections.Immutable;
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
            new Frame(new RunContext([], parseContext), [parser])
        ]);

        while (TryGetNextStep(out var x, out var step))
        {
            switch (step)
            {
                case IReturnStep s:
                {
                    var parsed = Parsing.From(s.Value, x.ParseContext.Text.Split());

                    UnwindBinds:
                    
                    if (x.Binds.IsEmpty)
                    {
                        return parsed.MapValue(o => (V)o!).Complete();
                    }

                    x = x with { Binds = x.Binds.Pop(out var bindFrame) };

                    switch (bindFrame)
                    {
                        case BindFrame.Started(var bind):
                        {
                            var next = bind.Right(s.Value)(x.ParseContext);

                            x = x with
                            {
                                Binds = x.Binds.Push(new BindFrame.Completed(bind, parsed)),
                                ParseContext = next.Context
                            };
                            
                            frames.Push(new(x, next.Steps));
                            
                            break;
                        }
                        
                        //we have a Bind to process
                        //we push a Left frame onto te stack
                        //we continue because we have a bind - there must be more to do (makes sense though seems like a shortcut)
                        //we therefore find the left leg to process next, but under the aegis of the bind (shouldn't this bind context be part of the frame???)
                        
                        //surely surely surely the bind context should be under the frame
                        //as the frames allow us to explore different avenues concurrently
                        //these avenues may be pointing in different directions...
                        
                        //but in fact we already do this (of course)
                        //the binds are part of the run context
                        //
                        //is the issue then one of early termination?
                        //we still evidently have work to do when we find the bind stack empty
                        //and we are still processing the return of the tuple
                        //there should always be a bind covering this tuple
                        //
                        //
                        
                        case BindFrame.Completed(_, var leftParsed):
                        {
                            parsed = Parsing.From(s.Value, [leftParsed, parsed]);
                            goto UnwindBinds;
                        }
                    }
                    
                    continue;
                }

                case IBindStep s:
                {
                    x = x with { Binds = x.Binds.Push(new BindFrame.Started(s)) };
                    frames.Push(new(x, [s.Left ?? Step.From(666)]));
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
        public record Started(IBindStep Bind) : BindFrame(Bind);
        public record Completed(IBindStep Bind, Parsing ParsedLeft) : BindFrame(Bind);
    }
    

    private record RunContext(
        ImmutableStack<BindFrame> Binds, 
        ParserOps.Context ParseContext)
    {
        public RunContext Fork()
            => this with { ParseContext = ParseContext.Fork() };
    }
    
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