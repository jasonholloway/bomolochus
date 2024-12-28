using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public interface Out<out N>
{
    IEnumerable<ParserOps.IResult<N>> Results { get; }
}

public static class OutExtensions
{
    public static Out<M> Select<N, M>(this Out<N> @this, Func<Parsing<N>, Parsing<M>> fn)
        => new OutImpl<M>(@this.Results.Select(r => r.Map(t => (t.Context, fn(t.Parsing)))));

    public static Out<M> SelectMany<N, M>(this Out<N> @this, Func<ParserOps.IResult<N>, Out<M>?> fn)
        => new OutImpl<M>(@this.Results.SelectMany(r => fn(r)?.Results ?? []));
}

internal readonly struct OutImpl<N>(IEnumerable<ParserOps.IResult<N>> results) : Out<N>
{
    public IEnumerable<ParserOps.IResult<N>> Results { get; } = results;
}

public interface Maybe
{
    public static Maybe<V> Empty<V>() => new(false, default);
    public static Maybe<V> From<V>(V val) => new(true, val);
}

public readonly struct Maybe<V>(bool hasValue, V? value) : Maybe
{
    public bool TryGetValue(out V val)
    {
        if (hasValue)
        {
            val = value!;
            return true;
        }
        
        val = default!;
        return false;
    }

    public readonly V? Value = value;
    public readonly bool HasValue = hasValue;
}


public class ParserOps
{
    public static IStep<Maybe<N>> Optional<N>(IStep<N> inner) =>
        OneOf(
            inner.Select(Maybe.From), 
            Return(Maybe.Empty<N>())
            );

    // public static Parser<N> Expand<N>(IParser<N> first, Func<N, IParser<N>> repeatedly) => 
    //     new(x => first.Run(x)?
    //         .SelectMany(r1 =>
    //         {
    //             return Out(_Expand(r1));
    //
    //             IEnumerable<IResult<N>> _Expand(IResult<N> r2)
    //             {
    //                 var results = repeatedly(r2.Parsing.Val)
    //                            .Run(r2.Context)?.Results ?? [];
    //                     
    //                 return results
    //                        .SelectMany(_Expand)
    //                        .Select(r => r.Map(t => 
    //                            (t.Context, Parsing.From(t.Parsing.Val, [r2.Parsing, t.Parsing]))
    //                        ))
    //                        .DefaultIfEmpty(r2);
    //             }
    //         }));

    public static Out<V> Out<V>(IEnumerable<IResult<V>> results) => new OutImpl<V>(results);
    public static Out<V> Out<V>(params IResult<V>[] results) => Out(results.AsEnumerable());
    

    public static IStep<ImmutableArray<N>> ParseEnclosedList<N>(
        IStep<object> parseOpen, 
        IStep<N> parseElement,
        IStep<object> parseDelimiter, 
        IStep<object> parseClose
        ) where N : Node =>
        from open in parseOpen
        from elements in ParseDelimitedList(parseElement, parseDelimiter)
        from close in parseClose
        select elements;
    
    public static IStep<ImmutableArray<N>> ParseDelimitedList<N>(
        IStep<N> parseElement,
        IStep<object> parseDelimiter)
        => Expand(
            from first in parseElement
            select ImmutableArray.Create(first), 
            ac =>
                from delimiter in parseDelimiter
                from next in parseElement
                select ac.Add(next)
            );

    //OneOf forks
    //but then it seems that Bind joins
    //it 
    //



    public static IStep<T> OneOf<T>(params IStep<T>[] parsers)
        => Step.From<T>(
            x => (x, parsers), 
            new ParserInfo(new Spacing(
                //parse space chars if they appear in _all_ below
                parsers.Aggregate(
                    seed: default(IEnumerable<char>),
                    (ac, f) => ac != null ? ac.Intersect(f.Info?.Spacing?.SpaceChars ?? []) : ac
                ) ?? [],
                //respect non-space chars is they appear in _any_ below
                parsers.SelectMany(f => f.Info?.Spacing?.NonSpaceChars ?? [])
            )), 
            "OneOf");
    
    public static IStep<N> Expand<N>(IStep<N> first, Func<N, IStep<N>> repeatedly)
         => OneOf(
                first.SelectMany(repeatedly).Select(b => (true, Step.From(b))),
                first.Select(a => (false, Step.From(a)))
             )
             .SelectMany(t => t switch
             {
                 (true, var sb) => Expand(sb, repeatedly),
                 (false, var sb) => sb
             });


    public static IStep<Readable> MatchWord()
        => Match("MatchWord", c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'));
    
    public static IStep<Readable> MatchDigits()
        => Match("MatchDigits", c => c is >= '0' and <= '9');
    
    public static IStep<Readable> Match(char @char) 
        => Step.From<Readable>(
            $"Match('{@char}')",
            x =>
            {
                if (x.Text.TryReadChar(@char, out var claimed))
                {
                    return (x, [
                        Step.From(claimed)
                    ]);
                    
                    // return Out(new Result<Readable>(
                    //     x, 
                    //     Parsing.From(claimed, x.Text.Split(), Addenda.Empty)
                    // ));
                }

                return (x, []);
                
            }, new ParserInfo(new Spacing([], [@char])));

    public static IStep<Readable> Match(string str)
        => Step.From<Readable>(
            $"Match(\"{str}\")",
            x =>
            {
                if (x.Text.ReadCharsWhile((c, i) => i < str.Length && c == str[i]) > 0)
                {
                    return (x, [Step.From(x.Text.Staged)]);
                    
                    // var split = x.Text.Split();
                    // return (x, [Step.From(split.Readable)]);
                }

                return (x, []);
            });

    public static IStep<Readable> Match(Predicate<char> predicate)
        => Match("Match", predicate);

    public static IStep<Readable> Match(string name, Predicate<char> predicate) 
        => Step.From<Readable>(
            name, 
            x =>
            {
                if (x.Text.ReadCharsWhile(predicate) > 0)
                {
                    return (x, [Step.From(x.Text.Staged)]);
                    
                    // var split = x.Text.Split();
                    // return (x, [Step.From(split.Readable)]);

                    // return Out(new Result<Readable>(
                    //     x, 
                    //     Parsing.From(split.Readable, split, Addenda.Empty)
                    // ));
                }

                return (x, []);
            });

    public static IStep<Node> Expect(string expectation)
        => Return<Node>(new Node.Expect().WithError(expectation));

    public static IStep<V> Return<V>(V value) => 
        Step.From(value);
    
    public record ParseContext(
        TextSplitter Text, 
        ImmutableHashSet<char> SpaceChars, 
        double CertaintyThreshold,
        string? LastNamedStep = null,
        bool SpaceParsable = true,
        int Strength = 1000)
    {
        public ParseContext Fork(double? certaintyThreshold = null) => 
            this with { 
                Text = Text.Clone(), 
                CertaintyThreshold = certaintyThreshold ?? CertaintyThreshold 
            };

        public override string ToString()
            => new string(Text.Clone().ReadAll().Take(5).ToArray()) + "...";
    }
    
    
    public interface IResult<out N>
    {
        ParseContext Context { get; }
        Parsing<N> Parsing { get; }
    }
    
    // public static Out<N> Out<N>(params IResult<N>[] results)
    //     => new OutImpl<N>(results);
    //
    // public static Out<N> Out<N>(IEnumerable<IResult<N>> results)
    //     => new OutImpl<N>(results.ToArray());
    

    public record Result<N>(ParseContext Context, Parsing<N> Parsing) : IResult<N>;

    public abstract class Parser
    {
        public static Parser<N> Create<N>(Func<ParseContext, Out<N>?> fn) 
            => new(fn);

        public static Parser<N> Create<N>(Func<IParser<N>> fn)
            => new(fn);
    }

    public class Parser<N> : Parser, IParser<N>
    {
        private readonly Lazy<(Func<ParseContext, Out<N>?> Fn, Spacing Spacing)> _lz;

        public Spacing Spacing => _lz.Value.Spacing;
        protected Func<ParseContext, Out<N>?> Parse => _lz.Value.Fn;

        public Parser(Func<ParseContext, Out<N>?> parse, Spacing? spacing = null)
        {
            _lz = new Lazy<(Func<ParseContext, Out<N>?>, Spacing)>(() => 
                (parse, spacing ?? Spacing.Empty)
            );
        }

        public Parser(Func<IParser<N>> parse)
        {
            _lz = new Lazy<(Func<ParseContext, Out<N>?>, Spacing)>(() =>
            {
                var fn = parse();
                return (x => fn.Run(x), fn.Spacing);
            });
        }

        public Out<N>? Run(ParseContext x0)
        {
            var x = x0;
            
            x = x with
            {
                SpaceChars = x.SpaceChars.Union(Spacing.SpaceChars).Except(Spacing.NonSpaceChars),
                SpaceParsable = true //todo should be set ol
            };
                
            if (x.SpaceParsable 
                && x.Text.ReadCharsWhile(x.SpaceChars.Contains) > 0)
            {
                var space = x.Text.Split();

                return Parse(x with { SpaceParsable = false })?
                    .SelectMany(r => Out(r.Map(t => 
                        (
                            t.Context with { SpaceParsable = true, SpaceChars = x0.SpaceChars }, 
                            Parsing.From(
                                t.Parsing!.Val, 
                                [new ParsingText<Readable>(space.Readable, space, true), t.Parsing]
                                )
                        ))));
            }

            return Parse(x)?
                .SelectMany(r => Out(r.Map(t => 
                    (
                        t.Context with { SpaceParsable = true, SpaceChars = x0.SpaceChars }, 
                        t.Parsing
                    ))));
        }
    }
    
    public record ParserExp<N>(Func<ParseContext, Out<N>> parse, Spacing? spacing = null) : IParser<N>
    {
        public Out<N> Run(ParseContext x)
            => parse(x);

        public Spacing Spacing => spacing ?? Spacing.Empty;
    }

    public interface IParser<out N>
    {
        Out<N>? Run(ParseContext x);
        Spacing Spacing { get; }
    }
}

public record Spacing(IEnumerable<char> SpaceChars, IEnumerable<char> NonSpaceChars)
{
    public static readonly Spacing Empty = new([], []);
}

public static class ParseResultExtensions 
{
    public static ParserOps.IResult<T2> Map<T, T2>(this ParserOps.IResult<T> result, Func<(ParserOps.ParseContext Context, Parsing<T> Parsing), (ParserOps.ParseContext, Parsing<T2>)> map)
    {
        var mapped = map((result.Context, result.Parsing));
        return new ParserOps.Result<T2>(mapped.Item1, mapped.Item2);
    }
}