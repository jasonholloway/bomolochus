using Bomolochus.Text;

namespace Bomolochus.Runner;

public static class ParserRunner
{
    public static Parsed<V> Parse<V>(this IStep<V> parser, Readable text)
    {
        var counts = (Binds: 0, Returns: 0);
        
        var context0 = new ParserOps.Context(
            [],
            TextSplitter.Create(text), 
            SpaceChars: [' ', '\t', '\n'], 
            SpaceParsable: true,
            CertaintyThreshold: 1
        );
        
        bool complete = false;
        
        //todo below should slough off frames given progress
        //and how would we know which ones we can get rid of?
        var frames = new Stack<Frame>([new Frame(context0, [parser])]);
        
        while (!complete && frames.TryPop(out var frame))
        {
            switch (frame.Steps)
            {
                case []: continue;
                case [var step]:
                {
                    var x = frame.Context.WithName(step);
                    
                    if (!RunStep(x, step))
                    {
                        complete = true;
                    }
                    break;
                }
                case [var step, ..var alternatives]:
                {
                    var x = frame.Context.WithName(step);

                    frames.Push(new(x.Fork(), alternatives));

                    if (!RunStep(x, step))
                    {
                        complete = true;
                    }
                    break;
                }
            }
        }

        throw new NotImplementedException();
        

        bool RunStep(ParserOps.Context x, IStep step)
        {
            switch (step)
            {
                case IReturnStep s:
                {
                    counts = counts with { Returns = counts.Returns + 1 };

                    x = x with { CurrentValue = s.Value };
                    
                    if (x.Binds.IsEmpty)
                    {
                        return false;
                    }

                    x = x with { Binds = x.Binds.Pop(out var bind) };

                    var next = bind.Right(x.CurrentValue)(x);
                    frames.Push(Frame.From(next));
                    return true;
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
                        var next = s.Right(default!)(x);
                        frames.Push(Frame.From(next));
                    }

                    return true;
                }
                
                default: throw new NotImplementedException();
            }
        }
        
        // so Terminals get completely squished into Yields,
        // even if they are terminal!
        // but a yield that results in [] is a completely different thing, that really should be ignored
        //
        

        // return (V)result;
    }
    
    private record Frame(ParserOps.Context Context, IStep[] Steps)
    {
        public static Frame From(INext next) => new(next.Context, next.Steps);
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
            
            
            
    // but how does the parsed stuff get accumulated?
    // here we just deal in results
    // easy: in the context
    //
    //
}

/* Parsing info to be accumulated on the Context
 * as we parse along, we accumulate inner parsings
 * but these are occasionally wrapped up into a Node
 * well, they are imprinted in the Node
 * but the tree on inner parseds just magically makes it into the new container
 *
 * we parse a number within an expression
 * the number text forms a parsing which gets put in the tree
 * then we emit the number node, which gets linked to this parsing
 * (and does the parsing get flattened here or only on completion?)
 *
 * in fact maybe the linking only gets done on completion as well
 * so values just get interleaved into the parsing tree, the head of which travels in the context
 * 
 * but then we start parsing the RHS 
 * even though we're subsequent to the LHS, and receive the right-to-left context from there
 * we don't close over the left parse tree at all
 *
 * normally this structure is given by the stackful progression of nested parse functions
 * rather than by the endless horizontal string of the Context
 * - so, the above stackless loop needs to provide it in its place
 * and of course the loop above does indeed feature a stack - just a heap-based one
 * so the stack frames above need to also capture the Parsings
 * but they hold the Context also? Yes, this makes sense
 * the frames need both bubbling, nested context from below, and also horizontal context
 *
 * So: two forms of context to pass around with each frame.
 * 
 */



