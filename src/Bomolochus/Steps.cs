using Bomolochus.Text;

namespace Bomolochus;

public interface IStep
{
    ParserInfo Info { get; }
}

public interface IStep<out V> : IStep;

public abstract class Step
{
    internal static IStep<V> From<V>(Func<ParserOps.Cursor, IStep<V>[]> run, ParserInfo? info = null, string? name = null)
        => new Root<V>(
            x => Next.From(run(x)),
            info ?? ParserInfo.Empty, 
            name != null ? () => name : null);

    public static IStep<V> From<V>(V value)
        => new Return<V>(value);
    

    public abstract record Return(object? Value) : IStep
    {
        public ParserInfo Info => ParserInfo.Empty;
        public Func<string?> GetName => () => $"R({Value?.ToString() ?? "NULL"})";
        public override string ToString() => GetName()!;
    }
    
    public record Return<V>(V TypedValue) : Return(TypedValue), IStep<V>
    {
        public override string ToString() => base.ToString();
    }



    public abstract record AbsorbSpace(IStep Inner) : IStep
    {
        public ParserInfo Info => ParserInfo.Empty;
        public override string ToString() => nameof(AbsorbSpace);
    }

    public record AbsorbSpace<V>(IStep<V> TypedInner) : AbsorbSpace(TypedInner), IStep<V>
    {
        public override string ToString() => base.ToString();
    }
    
    
    

    public record MatchChar(char Char) : IStep<Readable>
    {
        public ParserInfo Info { get; } = new(new Spacing([], [Char]));
        public Func<string?> GetName => () => $"Match({Char})";
        public override string ToString() => GetName()!;
    }
    
    public record MatchArbitrary(string Name, Func<char, int, bool> Match) : IStep<Readable>
    {
        public ParserInfo Info { get; } = ParserInfo.Empty;
        public Func<string?> GetName => () => Name;
        public override string ToString() => GetName()!;
    }


    public abstract record Barrier(IStep Inner, Strength Strength, bool IsEnclave = false) : IStep
    {
        public ParserInfo Info => ParserInfo.Empty;
        public Func<string?> GetName => () => $"Strength({Strength.Value})";
        public override string ToString() => GetName()!;
    };

    public record Barrier<V>(IStep<V> TypedInner, Strength Strength, bool IsEnclave = false) : Barrier(TypedInner, Strength, IsEnclave), IStep<V>
    {
        public override string ToString() => base.ToString();
    }
    
    
    public abstract record SpaceMod(IStep Inner, Spacing Spacing) : IStep
    {
        public ParserInfo Info => ParserInfo.Empty;
        public Func<string?> GetName => () => $"SpaceMod";
        public override string ToString() => GetName()!;
    };

    public record SpaceMod<V>(IStep<V> TypedInner, Spacing Spacing) : SpaceMod(TypedInner, Spacing), IStep<V>
    {
        public override string ToString() => base.ToString();
    }
    
    
    public abstract record Bind(IStep? Left, Func<object?, Func<ParserOps.Cursor, INext>> Right, ParserInfo RightInfo, Func<string?>? GetName = null)
        : IStep
    {
        public ParserInfo Info => Left?.Info ?? RightInfo;
        public override string ToString() => $"B({GetName?.Invoke() ?? (Left + "...")})";
    }
    
    public record Bind<L, R>(IStep<L>? TypedLeft, Func<L, Func<ParserOps.Cursor, INext<R>>> TypedRight, ParserInfo RightInfo, Func<string?>? GetName = null)
        : Bind(TypedLeft, l => TypedRight((L)l), RightInfo, GetName), IStep<R>
    {
        public override string ToString() => base.ToString();
    }

    public record Root<V>(Func<ParserOps.Cursor, INext<V>> Fn, ParserInfo Info, Func<string>? GetName = null)
        : Bind<bool, V>(null, _ => Fn, Info, GetName)
    {
        public override string ToString() => base.ToString();
    }
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

public record RunStep<V>(string Name, Func<IStep<V>> RootFn)
    : Step.Root<V>(
        _ => Next.From<V>([RootFn()]), 
        ParserInfo.Empty, 
        () => Name
        ), ICacheableStep
{
    public override string ToString()
    {
        return base.ToString();
    }
}

public static class StepExtensions
{
    public static IStep<B> Select<A, B>(this IStep<A> sa, Func<A, B> map) =>
        new Step.Bind<A, B>(sa, 
            a => _ => Next.From([new Step.Return<B>(map(a))]),
            sa.Info
        );

    public static IStep<C> SelectMany<A, B, C>(this IStep<A> sa, Func<A, IStep<B>> map, Func<A, B, C> join) =>
        new Step.Bind<A, C>(sa, 
            a => _ => Next.From<C>([map(a).Select(b => join(a, b))]),
            sa.Info
        );

    public static IStep<B> SelectMany<A, B>(
        this IStep<A> step,
        Func<A, IStep<B>> map) =>
        SelectMany(step, map, (_, b) => b);

    public static IStep<A> Where<A>(this IStep<A> step, Func<A, bool> predicate) =>
        new Step.Bind<A, A>(step,
            a => predicate(a) ? _ => Next.From([Step.From(a)]) : _ => Next.From<A>([]),
            step.Info
        );
}