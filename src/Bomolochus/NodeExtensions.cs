namespace Bomolochus;

public static class NodeExtensions
{
    // public static IStep<V> WithMaxStrength<V>(this IStep<V> step) =>
    //     WithStrength(step, int.MaxValue);

    // public static IStep<V> WithStrength<V>(this IStep<V> step, int strength)
    // {
    //     var origStrength = 0;
    //     
    //     return new Step<V>.TypedBind<V>(
    //         Step.From<V>(x =>
    //         {
    //             origStrength = x.Strength;
    //             return (x with { Strength = strength }, [step]);
    //         }),
    //         v => x => Next.From(
    //             x with { Strength = origStrength }, 
    //             [Step.From(v)]
    //             ),
    //         null,
    //         () => $"{nameof(WithStrength)}({step})"
    //     );
    // }
    //
    // public static IStep<V> RequireStrength<V>(this IStep<V> step, int strength) =>
    //     new Step<V>.TypedBind<V>(
    //         step, 
    //         v => x => Next.From(x, [Step.From(v)]),
    //         step.Info with
    //         {
    //             Strength = strength
    //         },
    //         () => $"{nameof(RequireStrength)}({step})"
    //     );
    
    public static IStep<V> WithError<V>(this IStep<V> step, string message) =>
        from v in step
        from _ in Step.From<bool>(x =>
        {
            //todo update contextual certainty here
            //todo add message here
            
            
            return ([Step.From(true)]);
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