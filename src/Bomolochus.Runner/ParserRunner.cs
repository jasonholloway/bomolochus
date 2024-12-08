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
            Parsing<Readable>? space = null;

            var spaceChars = x.ParseContext.SpaceChars
                .Union(step?.Info?.Spacing?.SpaceChars ?? [])
                .Except(step?.Info?.Spacing?.NonSpaceChars ?? []);
            
            if (x.ParseContext.SpaceParsable
               && x.ParseContext.Text.ReadCharsWhile(spaceChars.Contains) > 0)
            {
                var text = x.ParseContext.Text.Split();
                space = new ParsingText<Readable>(text.Readable, text, true);
            }
            
            x = x with { ParseContext = x.ParseContext with { SpaceParsable = false } };
            
            switch (step)
            {
                case IBindStep s:
                {
                    x = x with { Binds = x.Binds.Push(new BindFrame.StartedLeft(s, space)) };
                    frames.Push(new(x, [s.Left ?? Step.From(666)]));
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
                        //won't below play hell with completer?
                        return parsed.MapValue(o => (V)o!).Complete();
                    }

                    x = x with { Binds = x.Binds.Pop(out var bindFrame) };

                    switch (bindFrame)
                    {
                        case BindFrame.StartedLeft(var bind, var prefix):
                        {
                            var next = bind.Right(s.Value)(x.ParseContext);

                            x = x with
                            {
                                Binds = x.Binds.Push(
                                    new BindFrame.CompletingRight(
                                        bind, 
                                        prefix != null ? Parsing.From(parsed.Val, [prefix, parsed]) : parsed) //seems ugly like
                                    ),
                                ParseContext = next.Context
                            };
                            
                            frames.Push(new(x, next.Steps));
                            
                            break;
                        }
                        
                        case BindFrame.CompletingRight(_, var leftParsed):
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
        public record StartedLeft(IBindStep Bind, Parsing? Prefix) : BindFrame(Bind);
        public record CompletingRight(IBindStep Bind, Parsing ParsedLeft) : BindFrame(Bind);
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