using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus.Runner;

public static class ParserRunner
{
    public static Parsed<V> Parse<V>(this IStep<V> parser, Readable text)
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
    {
        var counts = (Binds: 0, Returns: 0);
        
        var frames = new Stack<Frame>(
        [
            new Frame(new RunContext([], null, parseContext), [parser])
        ]);

        while (TryGetNextStep(out var x, out var step))
        {
            switch (step)
            {
                case IReturnStep s:
                {
                    counts = counts with { Returns = counts.Returns + 1 };

                    x = x with { CurrentValue = s.Value };
                    
                    if (x.Binds.IsEmpty)
                    {
                        return default!;
                    }

                    x = x with { Binds = x.Binds.Pop(out var bind) };

                    var next = bind.Right(x.CurrentValue)(x.ParseContext);

                    x = x with { ParseContext = next.Context };
                    
                    frames.Push(new(x, next.Steps));
                    
                    continue;
                }

                case IBindStep s:
                {
                    counts = counts with { Binds = counts.Binds + 1 };

                    if (s.Left is { } left)
                    {
                        x = x with { Binds = x.Binds.Push(s) };
                        frames.Push(new(x, [left]));
                    }
                    else
                    {
                        var next = s.Right(default!)(x.ParseContext);

                        x = x with { ParseContext = next.Context };
                        
                        frames.Push(new(x, next.Steps));
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

    public record RunContext(ImmutableStack<IBindStep> Binds, object? CurrentValue, ParserOps.Context ParseContext)
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