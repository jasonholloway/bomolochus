using Bomolochus.Text;

namespace Bomolochus.Runner;

public static class ParserRunner
{
    record Frame(ParserOps.Context Context, IStep[] Steps)
    {
        public override string ToString()
            => $"{Context}, [{string.Join('|', Steps.Select(s => s.Name))}]";
    }
    
    public static Parsed<V> Parse<V>(this IStep<V> parser, Readable text)
    {
        var context0 = new ParserOps.Context(
            TextSplitter.Create(text), 
            SpaceChars: [' ', '\t', '\n'], 
            SpaceParsable: true,
            CertaintyThreshold: 1
        );
        
        bool complete = false;
        object? terminalValue = null;
        
        //todo below should slough off frames given progress
        var frames = new Stack<Frame>([new(context0, [parser])]);
        
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
                    
                    frames.Push(new Frame(x.Fork(), alternatives));

                    if (!RunStep(x, step))
                    {
                        complete = true;
                    }
                    break;
                }
            }
        }
        
        /* to accumulate the parsed graph
         * we need to know the limits of things
         * just as we need the same to trace progress
         * we have a real nested structure which we traverse
         * yet in the actual crawling we process these disconnected fragments
         * one after the other
         * ie at this layer all we know are monads
         * yet there is a richer structure in the graph
         * monads return monads 
         *
         * simple of course is a benefit here,
         * as it keeps the mechanism simple
         * but it therefore moves some responsibilty to the mapping layers
         *
         * the parse graph proceeds as query statements
         * fragments ENTER and RETURN
         * we possibly already have Returns, in the form of Yields
         * but these Yields do not retain their original context
         * 
         * ENTER = Continuation aka PARSE
         * RETURN = Yield
         * the above should be balanced then: each time we enter a new Parse, there should be a Yield with a result
         *
         *
         *
         * 
         * 
         */

        throw new NotImplementedException();
        

        bool RunStep(ParserOps.Context x0, IStep step)
        {
            switch (step)
            {
                case ITerminalStep<V> s:
                {
                    if (s.Value is Parsable p)
                    {
                        //todo handle here
                    }

                    terminalValue = s.Value;
                    return false;
                }

                case IContinuationStep<V> s:
                {
                    var c = s.Run(x0);
                    frames.Push(new(c.Context, c.Steps));
                    return true;
                }

                case IYieldStep<V> s:
                {
                    if (s.Value is Parsable p)
                    {
                        //todo handle value here...
                        //
                        //but the parsing tree flows by itself
                        //as continuations beget continuations
                        //but the value caught in the parsing tree is only known on completion of a branch
                        //that is a problem here...
                        //
                        //as we don't close over conclusions, we just pass the baton forward each time
                        //and what does each continuation offer back? another step, another context is the answer
                        //
                        //(1 + 3)
                        //
                        //1 is parsed, with a terminal/yield offering up Num(1)
                        //at this yield point we have a parsing - but we don't know if it's a subbranch or a subsequent
                        //the + is then parsed past without returning - yet
                        //then Num(3) is yielded - this again yields its own parsing
                        //there has to be a reset of the parsing tree between these two
                        //but from our current vantage point, we have no idea of which way to go
                        //
                        //Previously knowledge of this has been encoded into the SelectMany combinations
                        //
                        //
                    }

                    var nextStep = s.Next(s.Value);
                    frames.Push(new(x0, [nextStep]));
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



