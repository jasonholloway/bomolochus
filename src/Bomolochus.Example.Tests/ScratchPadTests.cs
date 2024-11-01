using System.Reactive.Linq;
using System.Reactive.Subjects;
using NUnit.Framework;

namespace Bomolochus.Example.Tests;

public class ScratchPadTests
{
    [Test]
    public void Hello()
    {
        using var updates = new Subject<string>();

        var z = Reparse(updates, ExampleParser.ParseExpression);
        
        Console.WriteLine("woof");
    }

    public static IObservable<ParserOps.IResult<N>> Reparse<N>(IObservable<string> updates, _IParser<N> parser)
        => updates.Let(us =>
        {
            var pad = new ScratchPad();
            
            //the pad starts out empty
            //then it receives an extent
            //which it then offers to the listening parser
            //(listening extents form a pile, with the topmost the only one that momentarily matters)
            
            //so given an incoming extent, the pad would decide who to inform
            //and what we want is the first covering registration
            //in the pile of listeners
            //
            //though smaller, more focused listeners should also be tried, as they might more locally
            //absorb changes for us, leaving a more managable excess and residue
            //
            //so - do we try each and every registration?
            //I'd say yes, given a change, bubble parts of it to partial listeners
            //if the parser can find something acceptable then - should that be the end of the matter?
            
            //BUT a local parser may find omething vaguely acceptable to itself
            //that would nevertheless be beaten by a wider reparsing
            //if the local reparsing finds something of equal or greater certainty,
            //then we can be happy with the reparsing
            //but if less than before, we have to offer up to the parent
            //
            //working from right to left?
            //
            //
            //
            //
            //

            return Observable.Empty<ParserOps.IResult<N>>();
        });
}



public class ScratchPad
{
    public record State()
    {
        public static State Empty = new();
    }

    public static State Scan(State state, string fragment)
    {
        return state;
    }
}