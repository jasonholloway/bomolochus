namespace Bomolochus;

using Context = ParserOps.Context;

public interface IStep<out V> {}

public abstract record Step
{
    public static Step<V> From<V>(V value) => new Step<V>.Terminal(value);
    public static Step<V> From<V>(Func<Context, (Context, IStep<V>[])> fn) => new Step<V>.Continuation(fn);
}

public abstract record Step<V> : Step, IStep<V>
{
    public record Continuation(Func<Context, (Context, IStep<V>[])> Run) : Step<V>;
    public record Terminal(V Value) : Step<V>;
}

public class _Parser
{
    public static _Parser<V> From<V>(IStep<V> step) => new(step);
}

public class _Parser<V> : _IParser<V>
{
    public _Parser(IStep<V> Step, Spacing? Spacing = null)
    {
        
    }

    public _Parser(Func<_IParser<V>> fn)
    {
        
    }

    public IStep<V> Step { get; }
    public Spacing? Spacing { get; }
}


public interface _IParser<out V>
{
    IStep<V> Step { get; }
    Spacing Spacing { get; }
}



public static class StepExtensions
{
    public static IStep<B> Select<A, B>(
        this IStep<A> step,
        Func<A, B> map) =>
        step switch
        {
            Step<A>.Terminal(var val) => new Step<B>.Terminal(map(val)),
            Step<A>.Continuation(var fn) => new Step<B>.Continuation(x0 => //should this be memoized? but to be memoized it needs to close over Context
            {
                var (x1, next) = fn(x0);
                return (x1, next.Select(s => s.Select(map)).ToArray());
            }),
            _ => throw new NotImplementedException()
        };

    public static IStep<C> SelectMany<A, B, C>(
        this IStep<A> step,
        Func<A, IStep<B>> map,
        Func<A, B, C> join) =>
        step switch
        {
            Step<A>.Terminal(var a) => map(a) switch
            {
                Step<B>.Terminal(var b) => new Step<C>.Terminal(join(a, b)),
                Step<B>.Continuation(var fn) => new Step<C>.Continuation(x0 =>
                {
                    var (x1, sbs) = fn(x0);
                    return (x1, sbs.Select(sb => sb.Select(b => join(a, b))).ToArray());
                }),
                _ => throw new NotImplementedException()
            },
            Step<A>.Continuation(var fn) => new Step<C>.Continuation(x0 =>
            {
                var (x1, sas) = fn(x0);
                return(x1, sas.Select(sa => sa.SelectMany(a =>
                {
                    var sb = map(a);
                    return sb.Select(b => join(a, b));
                })).ToArray());
            }),
            _ => throw new NotImplementedException()
        };

    public static IStep<B> SelectMany<A, B>(
        this IStep<A> step,
        Func<A, IStep<B>> map) =>
        SelectMany(step, map, (_, b) => b);
}

public static class _ParserExtensions
{
    public static _IParser<B> Select<A, B>(
        this _IParser<A> parser,
        Func<A, B> map) =>
        new _Parser<B>(
            Step: parser.Step.Select(map),
            Spacing: parser.Spacing
        );

    public static _IParser<C> SelectMany<A, B, C>(
        this _IParser<A> parser,
        Func<A, _IParser<B>> map,
        Func<A, B, C> join) =>
        new _Parser<C>(
            Step: parser.Step.SelectMany(a => map(a).Step, join),
            Spacing: parser.Spacing
        );

    public static _IParser<B> SelectMany<A, B>(
        this _IParser<A> parser,
        Func<A, _IParser<B>> map) =>
        SelectMany(parser, map, (_, b) => b);
}