namespace Bomolochus;

public static class NodeExtensions
{
    public static IStep<V> WithError<V>(this IStep<V> parser, string message) =>
        from v in parser
        from _ in Step.From<bool>(x =>
        {
            //todo update contextual certainty here
            //todo add message here
            return (x, [Step.From(true)]);
        })
        select v;
    
    // fn.SelectMany(v =>
    // {
    //     return _Parser.From(Step.From<V>(x =>
    //     {
    //         //todo update contextual certainty here
    //         //todo add message here
    //         return (x, [Step.From(v)]);
    //     }));
    // });
        
    public static N WithError<N>(this N node, string message)
        where N : Annotatable
    {
        node.Add(new Addenda(0.5, [message]));
        return node;
    }
}