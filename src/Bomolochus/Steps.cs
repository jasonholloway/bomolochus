namespace Bomolochus;

public interface IStep
{
    ParserInfo Info { get; }
    Func<string?>? GetName { get; }
};

public interface IStep<out V> : IStep;

public static class Step
{
    public static IStep<V> From<V>(string name, Func<ParserOps.Cursor, IStep<V>[]> run,
        ParserInfo? info = null)
        => From(run, info, name);
    
    internal static IStep<V> From<V>(Func<ParserOps.Cursor, IStep<V>[]> run, ParserInfo? info = null, string? name = null)
        => new Step<V>.Root(
            x => Next.From(run(x)),
            info, 
            name != null ? () => name : null);
    
    public static IStep<V> From<V>(V value)
        => new Step<V>.Return(value);
}

public abstract record Step<V>(ParserInfo Info, Func<string?>? GetName = null) : IStep<V>
{
    public record TypedBind<T>(IStep<T>? TypedLeft, Func<T, Func<ParserOps.Cursor, INext<V>>> TypedRight, ParserInfo? RightInfo = null, Func<string?>? GetName = null)
        : Bind(TypedLeft, o => TypedRight((T)o), RightInfo, GetName)
    {
        public override string ToString() => base.ToString();
    }

    public record Root(Func<ParserOps.Cursor, INext<V>> Fn, ParserInfo? Info = null, Func<string>? GetName = null)
        : Bind(null, _ => Fn, Info ?? ParserInfo.Empty, GetName)
    {
        public override string ToString() => base.ToString();
    }
        
    public record Bind(IStep? Left, Func<object?, Func<ParserOps.Cursor, INext<V>>> Right, ParserInfo RightInfo, Func<string?>? GetName = null)
        : Step<V>(Left?.Info ?? RightInfo, GetName), IBindStep<V>
    {
        public override string ToString() => $"B({GetName?.Invoke() ?? (Left + "...")})";
        Func<object?, Func<ParserOps.Cursor, INext>> IBindStep.Right => Right;
    }

    public record Return(V Value, Func<string?>? GetName = null)
        : Step<V>(ParserInfo.Empty, GetName), IReturnStep<V>
    {
        public override string ToString() => $"R({Value?.ToString() ?? "NULL"})";
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
    Func<object?, Func<ParserOps.Cursor, INext>> Right { get; }
    ParserInfo RightInfo { get; }
}

public interface IBindStep<out R> : IStep<R>, IBindStep
{
    new Func<object?, Func<ParserOps.Cursor, INext<R>>> Right { get; }
}

public interface ICacheableStep;

public interface INext
{
    IStep[] Steps { get; }
}

public interface INext<out V> : INext
{
    new IStep<V>[] Steps { get; }
}

public static class Next
{
    public static INext<V> From<V>(IStep<V>[] steps)
        => new Impl<V>(steps);
    
    public static INext From(IStep[] steps)
        => new Impl(steps);

    record Impl<V>(IStep<V>[] Steps) : INext<V>
    {
        IStep[] INext.Steps => Steps;
    }
    
    record Impl(IStep[] Steps) : INext
    {
        IStep[] INext.Steps => Steps;
    }
}

public record RunStep<V>(string Name, Func<IStep<V>> RootFn, int? strength = null)
    : Step<V>.Root(
        x => Next.From<V>([RootFn()]), 
        strength is int s ? ParserInfo.Empty with { Strength = s } : null, 
        () => Name
        ), ICacheableStep
{
    public override string ToString()
    {
        return base.ToString();
    }
}

public record ParserInfo(Spacing? Spacing, int? Strength = null)
{
    public static ParserInfo Empty = new(Spacing: null, Strength: null);
}

public static class StepExtensions
{
    public static IStep<B> Select<A, B>(this IStep<A> sa, Func<A, B> map) =>
        new Step<B>.TypedBind<A>(sa, 
            a => _ => Next.From([new Step<B>.Return(map(a), sa.GetName)]),
            sa.Info
        );

    public static IStep<C> SelectMany<A, B, C>(this IStep<A> sa, Func<A, IStep<B>> map, Func<A, B, C> join) =>
        new Step<C>.TypedBind<A>(sa, 
            a => _ => Next.From<C>([map(a).Select(b => join(a, b))]),
            sa.Info
        );

    public static IStep<B> SelectMany<A, B>(
        this IStep<A> step,
        Func<A, IStep<B>> map) =>
        SelectMany(step, map, (_, b) => b);

    public static IStep<A> Where<A>(this IStep<A> step, Func<A, bool> predicate) =>
        new Step<A>.TypedBind<A>(step,
            a => predicate(a) ? _ => Next.From([Step.From(a)]) : _ => Next.From<A>([]),
            step.Info
        );
}
