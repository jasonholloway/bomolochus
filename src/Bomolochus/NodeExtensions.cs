namespace Bomolochus;

public static class NodeExtensions
{
    public static IStep<V> WithPrecedence<V>(this IStep<V> step, int? precedence) =>
        Step.From<V>(
            x => (x, [step]),
            step.Info with
            {
                Precedence = precedence
            },
            nameof(WithPrecedence)
        );


    // new Step<V>.TypedBind<(int? OrigPrecedence, V Value)>(
        //     new Step<(int?, V)>.TypedBind<V>(
        //         Step.From(default(V)!),
        //         _ => x =>
        //         {
        //             var origPrecedence = x.Precedence;
        //
        //             if (precedence < origPrecedence)
        //             {
        //                 return Next.From<(int?, V)>(x, []);
        //             }
        //             
        //             return Next.From(
        //                 x with { Precedence = precedence }, 
        //                 [step.Select(v => (origPrecedence, v))]
        //                 );
        //         }, 
        //         Info: step.Info,
        //         GetName: () => $"{nameof(WithPrecedence)}0"),
        //     tup => x => Next
        //         .From(
        //             x with { Precedence = tup.OrigPrecedence }, 
        //             [Step.From(tup.Value)]
        //         ),
        //     Info: step.Info,
        //     GetName: () => $"{nameof(WithPrecedence)}1");
    
    public static IStep<V> WithError<V>(this IStep<V> step, string message) =>
        from v in step
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