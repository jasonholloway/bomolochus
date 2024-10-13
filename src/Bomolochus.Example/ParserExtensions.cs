using Bomolochus.Text;

namespace Bomolochus.Example;

using static ParserOps;

public static class ParserExtensions
{
    public static Parsed<N>? Run<N>(this IParser<N> parser, Readable text)
        where N : Node
    {
        var results = parser
            .Run(new Context(TextSplitter.Create(text), [' ', '\t', '\n']))?.Results;
        
        return results?
            .MaxBy(r => r.Parsing.Addenda.Certainty)?
            .Parsing
            .Complete();
    }
}

/* TODO
 * we need a more eager choosing of the winner
 * at the mo it's deferred to the last poss moment,
 * which explores literally everything: unnecessary
 */



