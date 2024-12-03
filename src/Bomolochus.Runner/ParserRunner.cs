using Bomolochus.Text;

namespace Bomolochus.Runner;

public static class ParserRunner
{
    record Frame(ParserOps.Context Context, IStep[] Steps)
    {
        public override string ToString()
            => $"{Context}, [{string.Join('|', Steps.Select(s => s.ToString()))}]";
    }
    
    public static Parsed<V> Parse<V>(this IStep<V> parser, Readable text)
    {
        var counts = (Enters: 0, Continues: 0, Yields: 0, Returns: 0);
        
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
        
        //we have enters then and matching yields
        //well we hope they're matching - there's a question here
        //how do we know they are?
        //
        //enters/runs/yields need to be constrained in some way
        //or they can just be programmed with discipline, right? (er yeah)
        //to return a result upwards there must be yields
        //
        //can we imagine yields after yields after yields with no balancing Enters
        //I think we can...
        //then this would be like a coroutine returning many results
        //but under one parser, seemingly
        //these yields then can't walk up the stack reliably
        //if there can be many of them
        //it's almost like yields need to be marked as terminating the stack frame _sometimes_
        //
        //this would give more of a role to the RunStep ctor (a good thing??)
        //
        //we're at this point now though where we're losing the thoroughgoing parsers everywhere thing
        //instead we're having to explicitly mark their boundaries
        //seems a shame
        //though every match can be a frame
        //
        //but then we interpolate this otherwise useless RunStep everywhere
        //but it would at least force a balance between Enter and Yield
        //which means... what does it mean?
        //on damage, then we know where to reparse up to
        //
        //but isn't every tiny subparser sufficient to reparse
        //the parse tree is accumulated until a yield is made
        //and at that point the aggregate is pinned to the result
        //
        //this is an interesting idea
        //instead of just capturing on yield of a node
        //we would attach parsings to each mini parse
        //but then there'll be a chain of causation between these
        //but the problem is, if a letter changes, how can we know if a mini parser has absorbed the shock or not
        //every change would cascade through the steps
        //as we have no way of distinguishing between success or failure
        //all we can do is reprocess, right to the end of the document
        //
        //the Yield is how inferior parsers communicate with their superiors
        //as we want damage to be handled locally as much as poss
        //these smaller parsers always need first dibs on the action
        //and whether they have succeeded or not in absorbing the changes
        //depends on the yield
        //
        //when damage occurs, we look up the attached parsings
        //and feed the same changes to the same subparsers again
        //does this mean - to the same Steps?
        //I think it might mean that
        //the steps stay in place, and after each one runs
        //we reparse forwards until
        //we find we're stable (somehow)
        //
        //the preexising approach only groups by yield (ie by Node)
        //rather than be Step - which I feel must be wrong
        //though each Node does properly take ownership of all the text
        //whose parsing led up to it
        //
        //so the yield of a node must have upstreams from various previous parsings
        //all feeding into it
        //you can imagine all parsings leading up to each yielded emission one by one
        //even one sibling leads up to the next, like
        
        
        //
        //
        //
        //

        throw new NotImplementedException();


        bool RunStep(ParserOps.Context x0, IStep step)
        {
            switch (step)
            {
                case IReturnStep<V> s:
                {
                    counts = counts with { Returns = counts.Returns + 1 };
                    
                    if (s.Value is Parsable p)
                    {
                        //todo handle here
                    }

                    terminalValue = s.Value;
                    return false;
                }

                case IRunStep<V> s:
                {
                    counts = counts with { Runs = counts.Continues + 1 };
                    
                    var c = s.Fn(x0);
                    frames.Push(new(c.Context, c.Steps));
                    return true;
                }
                
                case IEnterStep<V> s:
                {
                    counts = counts with { Enters = counts.Enters + 1 };
                    
                    frames.Push(new(x0, [s.Step]));
                    return true;
                }

                case IYieldStep<V> s:
                {
                    counts = counts with { Yields = counts.Yields + 1 };
                    
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



