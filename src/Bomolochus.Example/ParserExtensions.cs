using Bomolochus.Text;

namespace Bomolochus.Example;

using static ParserOps;

public static class ParserExtensions
{
    // public static Parsed<N>? Run<N>(this IParser<N> parser, Readable text)
    //     where N : Node
    // {
    //     var strength = 1000;
    //     
    //     var results = parser
    //         .Run(new Cursor(TextSplitter.Create(text), new(strength), [' ', '\t', '\n'], 1, Strength: strength))?.Results;
    //     
    //     return results?
    //         .MaxBy(r => r.Parsing.Addenda.Certainty)?
    //         .Parsing
    //         .Complete();
    // }
}

/* TODO
 * we need a more eager choosing of the winner
 * at the mo it's deferred to the last poss moment,
 * which explores literally everything: unnecessary
 *
 * need to nip disunctions in the bud
 * but this requires fairness
 * or - a Context always has a requisite certainty (based on previous iteration)
 * as soon as it crosses the downward threshold, the stream stops
 *
 * but OneOf is embedded at an arbitrary position in the search
 * but it flows through the Context
 *
 * TODO: 1) Context to have certainty threshold
 * TODO: 2) certainty to flow in Context
 *
 * 
 * 
 */



