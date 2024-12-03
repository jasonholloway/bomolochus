namespace Bomolochus;

using Context = ParserOps.Context;

public interface IStep
{
    ParserInfo Info { get; }
};

public interface IStep<out V> : IStep;

public static class Step
{
    public static IStep<V> From<V>(string name, Func<Context, (Context Context, IStep<V>[] Steps)> run,
        ParserInfo? info = null)
        => new RunStep<V>(name, () => From(run, info));
    
    public static IStep<V> From<V>(Func<Context, (Context Context, IStep<V>[] Steps)> run, ParserInfo? info = null)
        => new Step<V>.Run(x =>
            {
                var c = run(x); 
                return Next.From(c.Context, c.Steps);
            }, 
            info);
    
    public static IStep<V> From<V>(V value)
        => new Step<V>.Return(value);
}


public abstract record Step<V>(ParserInfo Info) : IStep<V>
{
    public record Run(Func<Context, INext<V>> Fn, ParserInfo? Info = null)
        : Step<V>(Info ?? ParserInfo.Empty), IRunStep<V>
    {
        public override string ToString() => "Run";
    }

    public record Return(V Value)
        : Step<V>(ParserInfo.Empty), IReturnStep<V> //todo infos should propagate you'd think
    {
        public override string ToString() => $"R({Value})";
    }
    
    
    public record Enter(IStep<V> Step, string Name)
        : Step<V>(Step.Info), IEnterStep<V>
    {
        public override string ToString() => Name;
    }

    public record Yield(object? Value, Func<object?, IStep<V>> Next)
        : Step<V>(ParserInfo.Empty), IYieldStep<V>
    {
        public override string ToString() => $"Y({Value})";
    }

    public record TypedYield<T>(T TypedValue, Func<T, IStep<V>> TypedNext) 
        : Yield(TypedValue, v => TypedNext((T)v))
    {
        public override string ToString() => base.ToString();
    }
}

public interface IRunStep<out V> : IStep<V>
{
    Func<Context, INext<V>> Fn { get; }
    ParserInfo? Info { get; }
}

public interface IReturnStep<out V> : IStep<V>
{
    V Value { get; }
}

public interface IEnterStep<out V> : IStep<V>
{
    string Name { get; }
    IStep<V> Step { get; }
}

public interface IYieldStep<out V> : IStep<V>
{
    object? Value { get; }
    Func<object?, IStep<V>> Next { get; }
}



public interface INext<out V>
{
    Context Context { get; }
    IStep<V>[] Steps { get; }
}

public static class Next
{
    public static INext<V> From<V>(Context context, IStep<V>[] steps)
        => new Impl<V>(context, steps);
    
    record Impl<V>(Context Context, IStep<V>[] Steps) : INext<V>;
}

public record RunStep<V>(string Name, Func<IStep<V>> RootFn)
    : Step<V>.Enter(new Run(x => Next.From<V>(x, [RootFn()])), Name);


public record ParserInfo(Spacing? Spacing)
{
    public static ParserInfo Empty = new(Spacing: null);
}




// public class _Parser
// {
//     public static _Parser<V> From<V>(IStep<V> step) => new(step);
// }

//so parser too is bimodal, instead of being a simple thing to call



// public class _Parser<V>(IStep<V> step, Spacing? spacing = null) : _IParser<V>
// {
//     public _Parser(Func<_IParser<V>> fn) 
//         : this(Bomolochus.Step.From<V>(x => (x, [fn()]))) 
//     {}
//
//     public IStep<V> Step => step;
//     public Spacing Spacing => spacing ?? Spacing.Empty;
// }
//
// public interface _IParser
// {
//     Spacing Spacing { get; }
// }
//
// public interface _IParser<out V> : _IParser
// {
//     IStep<V> Step { get; }
// }



public static class StepExtensions
{
    public static IStep<B> Select<A, B>(
        this IStep<A> step,
        Func<A, B> map) =>
        step switch
        {
            IReturnStep<A> sa => new Step<B>.Return(map(sa.Value)),
            
            IRunStep<A> sa => new Step<B>.Run(x0 => //should this be memoized? but to be memoized it needs to close over Context
            {
                var c = sa.Fn(x0);
                return Next.From(c.Context, c.Steps.Select(s => s.Select(map)).ToArray());
            }, sa.Info),
            
            IEnterStep<A> sa => new Step<B>.Enter(sa.Step.Select(map), sa.Name),
            
            IYieldStep<A> sa => new Step<B>.Yield(sa.Value, v => sa.Next(v).Select(map)),
            
            _ => throw new NotImplementedException()
        };

    public static IStep<C> SelectMany<A, B, C>(
        this IStep<A> step,
        Func<A, IStep<B>> map,
        Func<A, B, C> join) =>
        step switch
        {
            IReturnStep<A> sa => new Step<C>.TypedYield<A>(sa.Value, a =>
            {
                return MapInnerStep(map(a));

                IStep<C> MapInnerStep(IStep<B> sb) =>
                    sb switch
                    {
                        IReturnStep<B> { Value: var v } => new Step<C>.Return(join(a, v)),

                        IRunStep<B> { Fn: var fn, Info: var info } => new Step<C>.Run(x0 =>
                        {
                            var cb = fn(x0);
                            return Next.From(cb.Context, cb.Steps.Select(sb2 => sb2.Select(b => join(a, b))).ToArray());
                        }, info),

                        IEnterStep<B> { Step: var inner, Name: var name } => new Step<C>.Enter(
                            MapInnerStep(inner),
                            name),

                        IYieldStep<B> { Value: var val, Next: var next } => new Step<C>.Yield(val, v => next(v).Select(b => join(a, b))),

                        _ => throw new NotImplementedException()
                    };
            }),
            
            IRunStep<A> sa => new Step<C>.Run(x0 =>
            {
                var c = sa.Fn(x0);
                return Next.From(c.Context, c.Steps.Select(sa1 => sa1.SelectMany(a =>
                {
                    var sb = map(a);
                    return sb.Select(b => join(a, b));
                })).ToArray());
            }, sa.Info),
            
            IEnterStep<A> sa => new Step<C>.Enter(sa.Step.SelectMany(map, join), sa.Name),
            
            IYieldStep<A> sa => new Step<C>.Yield(sa.Value, v => 
                sa.Next(v).SelectMany(a => map(a).Select(b => join(a, b)))),
            
            _ => throw new NotImplementedException()
        };

    public static IStep<B> SelectMany<A, B>(
        this IStep<A> step,
        Func<A, IStep<B>> map) =>
        SelectMany(step, map, (_, b) => b);
}
