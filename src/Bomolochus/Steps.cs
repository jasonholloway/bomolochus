namespace Bomolochus;

using Context = ParserOps.Context;

public interface IStep
{
    ParserInfo? Info { get; }
    string Name { get; }
}

public interface IStep<out V> : IStep {}

public static class Step
{
    public static IStep<V> From<V>(Func<Context, (Context Context, IStep<V>[] Steps)> run, ParserInfo? info = null)
        => new Step<V>.Continuation(x =>
            {
                var c = run(x); 
                return Continued.From(c.Context, c.Steps);
            }, 
            info);
    
    public static IStep<V> From<V>(V value)
        => new Step<V>.Terminal(value);
}


public abstract record Step<V>(ParserInfo? Info, string Name) : IStep<V>
{
    public record Continuation(Func<Context, IContinued<V>> Run, ParserInfo? Info = null, string? Name = null)
        : Step<V>(Info, Name ?? "C"), IContinuationStep<V>
    {
        public override string ToString() => Name;
    }

    public record Terminal(V Value, string? Name = null)
        : Step<V>(ParserInfo.Empty, Name ?? "T"), ITerminalStep<V> //todo infos should propagate you'd think
    {
        public override string ToString() => Name;
    }

    public record Yield(object? Value, Func<object?, IStep<V>> Next, string? Name = null)
        : Step<V>(ParserInfo.Empty, Name ?? $"Y({Value})"), IYieldStep<V>
    {
        public override string ToString() => Name;
    }

    public record TypedYield<T>(T TypedValue, Func<T, IStep<V>> TypedNext, string? Name = null) 
        : Yield(TypedValue, v => TypedNext((T)v), Name)
    {
        public override string ToString() => Name;
    }

    public override string ToString() => Name;
}

public interface IContinuationStep<out V> : IStep
{
    Func<Context, IContinued<V>> Run { get; }
    ParserInfo? Info { get; }
}

public interface ITerminalStep<out V> : IStep
{
    V Value { get; }
}

public interface IYieldStep<out V> : IStep
{
    object? Value { get; }
    Func<object?, IStep<V>> Next { get; }
}



public interface IContinued<out V>
{
    Context Context { get; }
    IStep<V>[] Steps { get; }
}

public static class Continued
{
    public static IContinued<V> From<V>(Context context, IStep<V>[] steps)
        => new Impl<V>(context, steps);
    
    record Impl<V>(Context Context, IStep<V>[] Steps) : IContinued<V>;
}

public record ParseStep<V>(string Name, Func<IStep<V>> Fn)
    : Step<V>.Continuation(x => Continued.From<V>(x, [Fn()]), null, Name);


// public record _Parser<V>(Func<IStep<V>> fn) : ParseStep<V>.Continuation(x => (x, [fn()]));





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
            ITerminalStep<A> sa => new Step<B>.Terminal(map(sa.Value), sa.Name),
            
            IContinuationStep<A> sa => new Step<B>.Continuation(x0 => //should this be memoized? but to be memoized it needs to close over Context
            {
                var c = sa.Run(x0);
                return Continued.From(c.Context, c.Steps.Select(s => s.Select(map)).ToArray());
            }, sa.Info, sa.Name),
            
            IYieldStep<A> sa => new Step<B>.Yield(sa.Value, v => sa.Next(v).Select(map), sa.Name),
            
            _ => throw new NotImplementedException()
        };

    public static IStep<C> SelectMany<A, B, C>(
        this IStep<A> step,
        Func<A, IStep<B>> map,
        Func<A, B, C> join) =>
        step switch
        {
            ITerminalStep<A> sa => new Step<C>.TypedYield<A>(sa.Value, a => map(a) switch
            {
                ITerminalStep<B> sb => new Step<C>.Terminal(join(a, sb.Value), sb.Name),
                
                IContinuationStep<B> sb => new Step<C>.Continuation(x0 =>
                {
                    var cb = sb.Run(x0);
                    return Continued.From(cb.Context, cb.Steps.Select(sb2 => sb2.Select(b => join(a, b))).ToArray());
                }, sb.Info, sb.Name),
                
                IYieldStep<B> sb => new Step<C>.Yield(sb.Value, v => sb.Next(v).Select(b => join(a, b)), sb.Name),
                
                _ => throw new NotImplementedException()
            }),
            
            IContinuationStep<A> sa => new Step<C>.Continuation(x0 =>
            {
                var c = sa.Run(x0);
                return Continued.From(c.Context, c.Steps.Select(sa1 => sa1.SelectMany(a =>
                {
                    var sb = map(a);
                    return sb.Select(b => join(a, b));
                })).ToArray());
            }, sa.Info, sa.Name),
            
            IYieldStep<A> sa => new Step<C>.Yield(sa.Value, v => 
                sa.Next(v).SelectMany(a => map(a).Select(b => join(a, b))), sa.Name),
            
            _ => throw new NotImplementedException()
        };

    public static IStep<B> SelectMany<A, B>(
        this IStep<A> step,
        Func<A, IStep<B>> map) =>
        SelectMany(step, map, (_, b) => b);
}

// public static class _ParserExtensions
// {
//     public static _IParser<B> Select<A, B>(
//         this _IParser<A> parser,
//         Func<A, B> map) =>
//         new _Parser<B>(
//             Step: parser.Step.Select(map),
//             Spacing: parser.Spacing
//         );
//
//     public static _IParser<C> SelectMany<A, B, C>(
//         this _IParser<A> parser,
//         Func<A, _IParser<B>> map,
//         Func<A, B, C> join) =>
//         new _Parser<C>(
//             Step: parser.Step.SelectMany(a => map(a).Step, join),
//             Spacing: parser.Spacing
//         );
//
//     public static _IParser<B> SelectMany<A, B>(
//         this _IParser<A> parser,
//         Func<A, _IParser<B>> map) =>
//         SelectMany(parser, map, (_, b) => b);
// }