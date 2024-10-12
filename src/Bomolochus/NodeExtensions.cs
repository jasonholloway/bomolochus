namespace Bomolochus;

using static ParserOps;

public static class NodeExtensions
{
    public static N WithError<N>(this N node, string message)
        where N : Annotatable
    {
        node.Add(new Addenda(0.5, [message]));
        return node;
    }
    
    //todo below needs to map including Spacing as well as modified addenda

    public static Parser<N> WithError<N>(this IParser<N> fn, string message)
        where N : Node =>
        Parser.Create(x => fn.Run(x)?
            .Select(p => p != null 
                ? new ParsingGroup<N>(
                    p.Val,
                    [p],
                    new Addenda(0.5, [message])
                ) 
                : null));
        
}