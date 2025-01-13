using System.Collections.Immutable;
using System.Linq.Expressions;
using Bomolochus.Text;

namespace Bomolochus;

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
            _ => parsers, 
            new ParserInfo(
                new Spacing(
                    //parse space chars if they appear in _all_ below
                    parsers.Aggregate(
                        seed: default(IEnumerable<char>),
                        (ac, f) => ac != null ? ac.Intersect(f.Info?.Spacing?.SpaceChars ?? []) : ac
                    ) ?? [],
                    //respect non-space chars is they appear in _any_ below
                    parsers.SelectMany(f => f.Info?.Spacing?.NonSpaceChars ?? [])
                )
            ),
            "OneOf");

    public static IStep<N> Expand<N>(IStep<N> first, Func<N, IStep<N>> repeatedly)
        => first
            .SelectMany(a => OneOf(
                repeatedly(a).SelectMany(b => Expand(Step.From(b), repeatedly)),
                Step.From(a)
            ));

    public static IStep<Readable> MatchWord()
        => new Step.MatchArbitrary("MatchWord", (c, _) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'));
    
    public static IStep<Readable> MatchDigits()
        => new Step.MatchArbitrary("MatchDigits", (c, _) => c is >= '0' and <= '9');

    public static IStep<Readable> Match(Predicate<char> predicate)
        => new Step.MatchArbitrary("Match", (c, _) => predicate(c));
    
    public static IStep<Readable> Match(char @char) 
        => new Step.MatchChar(@char);

    public static IStep<Readable> Match(string str)
        => new Step.MatchArbitrary($"Match(\"{str}\")", (c, i) => i < str.Length && c == str[i]);

    public static IStep<Node> Expect(string expectation)
        => Return<Node>(new Node.Expect().WithError(expectation));

    public static IStep<V> Return<V>(V value) => 
        Step.From(value);



    public record CursorInfo(
        ImmutableHashSet<char> SpaceChars,
        double CertaintyThreshold
        );
    
    public class Cursor(
        TextSplitter text, 
        Continuations continuations,
        CursorInfo info,
        Strength strength,
        bool spaceParsable = true
        )
    {
        public TextSplitter Text { get; } = text;
        public Continuations Continuations { get; private set; } = continuations;
        public CursorInfo Info { get; set; } = info;
        public Strength Strength { get; set; } = strength;
        public bool SpaceParsable { get; set; } = spaceParsable;

        public Cursor Fork(double? certaintyThreshold = null) =>
            new(Text.Clone(), 
                Continuations,
                Info with { CertaintyThreshold = certaintyThreshold ?? Info.CertaintyThreshold },
                Strength,
                SpaceParsable
                );

        public Split Move()
        {
            var split = Text.Split();

            if (!split.IsEmpty)
            {
                //the originating strength of the cursor is captured into the continuations
                Continuations = new Continuations(Strength);
            }

            return split;
        }

        public override string ToString()
            => $"{Strength.Value:000} \"{new string(Text.Clone().ReadAll().Take(4).ToArray())}\"";
    }
    
    public class Continuations(int strength) : Dictionary<ICacheableStep, ContinuationCell>
    {
        public int Strength => strength;
    }
    
    public class ContinuationCell(ICacheableStep origin)
    {
        public readonly ICacheableStep Origin = origin;

        private readonly List<(Cursor Cursor, Strength Strength, IStep Step)> _results = new(4);
        private readonly Stack<Action<Cursor, Strength, IStep>> _continuations = new(4);

        public void Emit(Cursor cursor, Strength strength, IStep step)
        {
            _results.Add((cursor, strength, step));
            
            foreach (var fn in _continuations)
            {
                fn(cursor, strength, step);
            }
        }

        public void AddContinuation(Action<Cursor, Strength, IStep> continuation)
        {
            _continuations.Push(continuation);

            foreach (var next in _results)
            {
                continuation(next.Cursor, next.Strength, next.Step);
            }
        }
    }
}

public record Spacing(IEnumerable<char> SpaceChars, IEnumerable<char> NonSpaceChars)
{
    public static readonly Spacing Empty = new([], []);
}