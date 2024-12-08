namespace Bomolochus;

using Context = ParserOps.Context;

public interface IStep
{
    ParserInfo Info { get; }
    Func<string?>? GetName { get; }
};

public interface IStep<out V> : IStep;

public static class Step
{
    public static IStep<V> From<V>(string name, Func<Context, (Context Context, IStep<V>[] Steps)> run,
        ParserInfo? info = null)
        => From(run, info, name);
    
    internal static IStep<V> From<V>(Func<Context, (Context Context, IStep<V>[] Steps)> run, ParserInfo? info = null, string? name = null)
        => new Step<V>.Bind(
            null,
            _ => x =>
            {
                var c = run(x); 
                return Next.From(c.Context, c.Steps);
            }, 
            info, 
            name != null ? () => name : null);
    
    public static IStep<V> From<V>(V value)
        => new Step<V>.Return(value);
}


public abstract record Step<V>(ParserInfo Info, Func<string?>? GetName = null) : IStep<V>
{
    public record TypedBind<T>(IStep<T>? TypedLeft, Func<T, Func<Context, INext<V>>> TypedRight, ParserInfo? Info = null, Func<string?>? GetName = null)
        : Bind(TypedLeft, o => TypedRight((T)o), Info, GetName)
    {
        public override string ToString() => base.ToString();
    }
        
    public record Bind(IStep? Left, Func<object?, Func<Context, INext<V>>> Right, ParserInfo? Info = null, Func<string?>? GetName = null)
        : Step<V>(Info ?? Left?.Info ?? ParserInfo.Empty, GetName), IBindStep<V>
    {
        public override string ToString() => $"B({GetName?.Invoke() ?? ""})";
        Func<object?, Func<Context, INext>> IBindStep.Right => Right;
    }

    public record Return(V Value, Func<string?>? GetName = null)
        : Step<V>(ParserInfo.Empty, GetName), IReturnStep<V>
    {
        public override string ToString() => $"R({GetName?.Invoke() ?? Value?.ToString() ?? "NULL"})";
        object? IReturnStep.Value => Value;
    }
}


public interface IReturnStep : IStep
{
    object? Value { get; }
}

public interface IReturnStep<out V> : IStep<V>, IReturnStep
{
    new V Value { get; }
}


public interface IBindStep : IStep
{
    IStep? Left { get; }
    Func<object?, Func<Context, INext>> Right { get; }
}

public interface IBindStep<out R> : IStep<R>, IBindStep
{
    new Func<object?, Func<Context, INext<R>>> Right { get; }
}





// public interface IContinueStep<out V> : IStep<V>
// {
//     Func<Context, INext<V>> Fn { get; }
//     ParserInfo? Info { get; }
// }
//
// public interface IEnterStep<out V> : IStep<V>
// {
//     string Name { get; }
//     IStep<V> Step { get; }
// }
//
// public interface IYieldStep<out V> : IStep<V>
// {
//     object? Value { get; }
//     Func<object?, IStep<V>> Next { get; }
// }




public interface INext
{
    Context Context { get; }
    IStep[] Steps { get; }
}

public interface INext<out V> : INext
{
    new IStep<V>[] Steps { get; }
}

public static class Next
{
    public static INext<V> From<V>(Context context, IStep<V>[] steps)
        => new Impl<V>(context, steps);

    record Impl<V>(Context Context, IStep<V>[] Steps) : INext<V>
    {
        IStep[] INext.Steps => Steps;
    }
}

public record RunStep<V>(string Name, Func<IStep<V>> RootFn)
    : Step<V>.Bind(null, _ => x => Next.From<V>(x, [RootFn()]), null, () => Name);


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
    public static IStep<B> Select<A, B>(this IStep<A> sa, Func<A, B> map) =>
        new Step<B>.TypedBind<A>(sa, 
            a => x => Next.From(x, [new Step<B>.Return(map(a), sa.GetName)]),
            sa.Info
        );

    public static IStep<C> SelectMany<A, B, C>(this IStep<A> sa, Func<A, IStep<B>> map, Func<A, B, C> join) =>
        new Step<C>.TypedBind<A>(sa, 
            a => x => Next.From<C>(x, [map(a).Select(b => join(a, b))]),
            sa.Info
        );

    public static IStep<B> SelectMany<A, B>(
        this IStep<A> step,
        Func<A, IStep<B>> map) =>
        SelectMany(step, map, (_, b) => b);
}
