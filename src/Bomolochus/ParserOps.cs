using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

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

    public static IStep<T> OneOf<T>(params IStep<T>[] parsers)
        => Step.From<T>(
            x => (x, parsers), 
            new ParserInfo(
                new Spacing(
                    //parse space chars if they appear in _all_ below
                    parsers.Aggregate(
                        seed: default(IEnumerable<char>),
                        (ac, f) => ac != null ? ac.Intersect(f.Info?.Spacing?.SpaceChars ?? []) : ac
                    ) ?? [],
                    //respect non-space chars is they appear in _any_ below
                    parsers.SelectMany(f => f.Info?.Spacing?.NonSpaceChars ?? [])
                ),
                parsers.Max(p => p.Info.Strength)
            ), 
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



    public record CursorInfo(
        ImmutableHashSet<char> SpaceChars,
        double CertaintyThreshold);
    
    public class Cursor(
        TextSplitter text, 
        Continuations continuations,
        CursorInfo info,
        int strength = 100,
        bool spaceParsable = true
        )
    {
        public TextSplitter Text { get; } = text;
        public Continuations Continuations { get; private set; } = continuations;
        public CursorInfo Info { get; private set; } = info;
        public int Strength { get; set; } = strength;
        public bool SpaceParsable { get; set; } = spaceParsable;

        public Cursor Fork(double? certaintyThreshold = null) =>
            new(Text.Clone(), 
                Continuations,
                Info with { CertaintyThreshold = certaintyThreshold ?? Info.CertaintyThreshold },
                Strength,
                SpaceParsable
                );

        public Split Move(int? strength = null)
        {
            var split = Text.Split();

            if (!split.IsEmpty)
            {
                Continuations = new Continuations(Strength);
            }

            Strength = strength ?? Strength;

            return split;
        }

        public override string ToString()
            => new string(Text.Clone().ReadAll().Take(5).ToArray()) + "...";
    }
    
    public class Continuations(int strength) : Dictionary<ICacheableStep, ContinuationCell>
    {
        public int Strength => strength;
    }
    
    public class ContinuationCell(ICacheableStep origin)
    {
        public readonly ICacheableStep Origin = origin;

        private readonly List<INext> _results = new();
        private readonly List<Action<INext>> _continuations = new();

        public void Emit(INext next)
        {
            _results.Add(next);
            
            foreach (var fn in _continuations)
            {
                fn(next);
            }
        }

        public void AddContinuation(Action<INext> continuation)
        {
            _continuations.Add(continuation);

            foreach (var next in _results)
            {
                continuation(next);
            }
        }
    }
    
    
    
    public interface IResult<out N>
    {
        Cursor Context { get; }
        Parsing<N> Parsing { get; }
    }

    public record Result<N>(Cursor Context, Parsing<N> Parsing) : IResult<N>;
}

public record Spacing(IEnumerable<char> SpaceChars, IEnumerable<char> NonSpaceChars)
{
    public static readonly Spacing Empty = new([], []);
}

public static class ParseResultExtensions 
{
    public static ParserOps.IResult<T2> Map<T, T2>(this ParserOps.IResult<T> result, Func<(ParserOps.Cursor Context, Parsing<T> Parsing), (ParserOps.Cursor, Parsing<T2>)> map)
    {
        var mapped = map((result.Context, result.Parsing));
        return new ParserOps.Result<T2>(mapped.Item1, mapped.Item2);
    }
}