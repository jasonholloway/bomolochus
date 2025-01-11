namespace Bomolochus;

public static class NodeExtensions
{
    public static IStep<V> WithFullStrength<V>(this IStep<V> step) =>
        WithNestedStrength(step, 999);

    public static IStep<V> WithNestedStrength<V>(this IStep<V> step, Strength strength)
    {
        var origStrength = 0;
        
        return new Step<V>.TypedBind<V>(
            Step.From<V>(x =>
            {
                origStrength = x.Strength;
                x.Strength = strength;
                return [step];
            }),
            v => x =>
            {
                x.Strength = origStrength;
                return Next.From([Step.From(v)]);
            },
            ParserInfo.Empty,
            Strength.Empty, //this is important to keep nested strength from bubbling
            true,
            () => $"{nameof(WithNestedStrength)}({step})"
        );
    }
    
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