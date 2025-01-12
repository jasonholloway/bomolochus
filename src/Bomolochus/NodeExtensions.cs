namespace Bomolochus;

public static class NodeExtensions
{
    public static IStep<V> WithFullStrength<V>(this IStep<V> step) 
        => WithStrength(step, 100, true);

    public static IStep<V> WithStrength<V>(this IStep<V> step, Strength strength, bool isEnclave = false)
        => new Step<V>.StrengthBarrier(step, strength, isEnclave);

    // public static RunStep<V> WithStrength<V>(this RunStep<V> step, Strength strength)
    //     => step with
    //     {
    //         //todo obvs below is grotesque
    //         Fn = x => Next.From(step.Fn(x).Steps.Select(s => s.WithStrength(strength)).ToArray()) 
    //     };
    
    
    public static IStep<V> WithError<V>(this IStep<V> step, string message) =>
        from v in step
        from _ in Step.From<bool>(x =>
        {
            //todo update contextual certainty here
            //todo add message here
            
            
            return ([Step.From(true)]);
        })
        select v;
        
    public static N WithError<N>(this N node, string message)
        where N : Annotatable
    {
        node.Add(new Addenda(0.5, [message]));
        return node;
    }
}