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



//!!!!!!!!!!!!!!
//but step also needs to have public properties, eg spacing
//so must be an object rather than just a delegate

//so parsers get turned into the above
//
//
//



//but as standard running a step produces multiple continuations
//disjunction is possible everywhere - not a special case to be detected
//
//and every time there are sibling possibilities, store them as points where we can go again
//but this doesn't need to be done within the context
//as we will have our own outer context, a machine almost
//
//what about the eventual result?
//a step needs to have an eventual result
//so can imagine a third output, a generically typed result
//
//however how would would this work in LINQ
//Steps aren't what get strung together in linq queries
//parsers are, as now - that is individual operations that output something or don't output something
//but the stringing of these together forms a structure of steps
//OneOfs and simple parsers are all strung into steps from the linq query mechanism
//
//so, to recap:
//- LINQ to be translated into steps, through matching Parsers
//- a Parser actually is a step - just individually there are no continuations;
//  through SelectMany the steps get combined into steps-begetting-steps
//- this tallies with Steps returning values as well as a third output;
//  this lets them be strung into LINQ queries
//
//
// TODO
// Steps to have value outputs
//
//
//







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




    public static IStep<T> OneOf<T>(params IStep<T>[] parsers)
        => Step.From(
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

    // public static Parser<T> OneOf<T>(params IParser<T>[] fns)
    //     => new(
    //         spacing: new Spacing(
    //             //parse space chars if they appear in _all_ below
    //             fns.Aggregate(
    //                 seed: default(IEnumerable<char>), 
    //                 (ac, f) => ac != null ? ac.Intersect(f.Spacing.SpaceChars) : ac
    //                 ) ?? [], 
    //             //respect non-space chars is they appear in _any_ below
    //             fns.SelectMany(f => f.Spacing.NonSpaceChars)),
    //         parse: x =>
    //         {
    //             //naively does full depth-first search
    //             return Out(fns
    //                 .SelectMany(fn => fn.Run(x.Fork())?.Results ?? [])
    //                 .Select(r => r
    //                     .Map(t => (t.Context, t.Parsing))) //not committing context
    //                 );
    //             
    //             //we have funcs we can put in a continuation stack
    //             //but its not just the immediates parser funcs we need,
    //             //its also the continuation context
    //             //ie the place in the parsing
    //             //that is, where the parsed result should be given to
    //             //a kind of emitter
    //             //but to avoid a massive stack, we do need to yield
    //             //and this sink itself is something that needs to be released when complete
    //             //
    //             
    //             /* if the parse tree is a data structure then each checkpoint in the past
    //              * can have text and a node in the tree - or rather, graph
    //              * that's sufficient to continue
    //              *
    //              * currently we're kind of tied into and around the structure
    //              * snaking through it, which in some ways gives us more agility
    //              * or rather it challenges our agility and requires a succession of tactics
    //              * to get by. Let's instead objectify the parsing and create space for
    //              * systematic processing.
    //              * 
    //              * But, what's the limit of our current snaking? ie could we achieve similar
    //              * without the whole hog. 
    //              *
    //              * our control of the looping, and the holding on to state
    //              * is done up top, so there needs to be some communication back from the actual parsing
    //              * that we can release here
    //              *
    //              * -------
    //              *
    //              * or, if we were to construct a parse graph, what would be the graph be like?
    //              * OneOf would be a special kind of node, that's for sure
    //              * but all else would just be Parsers in sequence
    //              * each parser would have it's next parser
    //              * and each parser would just be a combo of smaller parsers
    //              * though with inevitable laziness
    //              * parsing monads in fact would be exactly what we'd have
    //              *
    //              * running a parser would return a parser
    //              * and a OneOf would be a particular recognisable parser
    //              * so a reference back to a parser could be continued by running the parser
    //              * again - and the famed referential transparency would be implied
    //              * not unreasonably so that we could repeatedly traverse the same sequences
    //              * with consistent results
    //              *
    //              * we have parsers right now - but do they produce other parsers?
    //              * also we need to do the opposite of encapsulation, we need to bubble out of current contexts
    //              * eg ParseMany((ParseString | ParseNumber) + ParseComma)
    //              *
    //              * in the above we have a disjunction of string and number, each one represented by a 'Parser'
    //              * the ParseString parser is however embedded within a structure
    //              * really ParseString needs to be married to a context, an address within the structure
    //              * an awareness, a walking
    //              * ParseString itself isn't the monad we seek, it's the data that drives the parsing
    //              * the word Parsing needs to be reclaimed
    //              *
    //              * A Parsing produces a Parsing
    //              * that is another Step in the Walk
    //              * 
    //              * ------
    //              *
    //              * Currently: OneOf chooses between Parsers, which are functions that map between contexts
    //              * really, the choice is between not just mappings between contexts, but tuples of contexts and continuations
    //              * the choice should be across Func<Context, Context, Continuation>
    //              * 
    //              */
    //             
    //             
    //             
    //             
    //             
    //             
    //             
    //             //how to get the thresholds back into the next branch
    //             //this is backtracking
    //             //instead of hopping back to an arbitrary place in a search tree
    //             //the code above keeps the branches entirely separate, without shared context
    //             //I'm not sure this is te best approach
    //             //we need something a bit more imperative to allow sequencing of context
    //             //(also enabling "fair scheduling" maybe)
    //             
    //             //as well as thresholds
    //             //it should be possible to preempt the search as soon as we get a result with full certainty
    //             //but where is it decided that the search is complete?
    //             //of course originally the search was for just one parsing, 
    //             //and so it was determinate and close-to-hand and simple
    //             //
    //             //but now we want to test to some arbitrary distance in the future
    //             //though - this futurity should be limited to lessen the search space
    //             //
    //             //the problem here is in incremental parsing
    //             //ie we might not be parsing that much in response to say a character changing
    //             //a tiny reparsing would be operating within a certain context
    //             //that would have a certainty attached
    //             //
    //             //so a Parsing refound in situ may only have a partial certainty
    //             //though it might be the best we have/had
    //             //eg an expectation is in place
    //             //
    //             //would we store other potential parsings alongside it, just raring to go and displace the previous choice?
    //             //to an extent we could, though there will be a trade-off to be struck here
    //             //we could rule this out completely, though this would get us stuck in a particular parsing
    //             //we'd get snagged onto a previous obsolete optimum
    //             //therefore: this is unacceptable
    //             //and we must store, or have recourse to, other possible parsings which were previously rejected
    //             //"have recourse to" is key here
    //             //
    //             //
    //             //
    //             //
    //             
    //             
    //             
    //             /* so currently we do go depth first
    //              * is that a problem?
    //              * well, every point of disjunction is stored as a closure in memory
    //              * and are these points of disjunction ever released?
    //              * they should be released when we have succeeded a certain distance
    //              * 
    //              * in proper parsers presumably there's some kind of workqueue
    //              * allowing nodes to fall out of it once they've been happily parsed
    //              *
    //              * so the imagination is now of a Context filled with continuations 
    //              * at a certain point we slough off old continuations and everything they've inevitably captured
    //              *
    //              * at the mo we just map - ie explore - the lot indiscriminately
    //              * 
    //              * it would be a stack, with a sloughing operation
    //              * each node in the stack would refer to a previous one
    //              * with a threshold for releasing, ie erasing the upward reference
    //              * the stack itself, with its SloughOff(val), would either walk the nodes
    //              * or maintain some kind of quick index to be able to find erasable refs quickly
    //              *
    //              * each node would have a timestamp
    //              * that would be communicated downwards
    //              * so we always know whether there's anything to release
    //              * if the counter gets so far, then there's something to release
    //              *
    //              * but what would be stored in the stack?
    //              * contexts, including texts etc
    //              * but we don't have modes
    //              * we have functions, I suppose
    //              */
    //             
    //             
    //             
    //             
    //         });

    public static IStep<Readable> MatchWord()
        => Match("MatchWord", c => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'));
    
    public static IStep<Readable> MatchDigits()
        => Match("MatchDigits", c => c is >= '0' and <= '9');
    
    public static IStep<Readable> Match(char @char) 
        => Step.From<Readable>(
            $"Match({@char})",
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
            $"Match({str})",
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
        => Return<Node>(new Node.Expect()).WithError(expectation);

    public static IStep<V> Return<V>(V value) => 
        Step.From(value);
    
    public record Context(
        TextSplitter Text, 
        ImmutableHashSet<char> SpaceChars, 
        double CertaintyThreshold,
        string? LastNamedStep = null,
        bool SpaceParsable = true)
    {
        public Context Fork(double? certaintyThreshold = null) => 
            this with { 
                Text = Text.Clone(), 
                CertaintyThreshold = certaintyThreshold ?? CertaintyThreshold 
            };

        public override string ToString()
            => new string(Text.Clone().ReadAll().Take(3).ToArray()) + ">" + LastNamedStep; //temporary nasty hack for feedback

        public Context WithName(IStep step) =>
            step.ToString() switch
            {
                {} s => this with{ LastNamedStep = s },
                _ => this
            };
    }
    
    
    public interface IResult<out N>
    {
        Context Context { get; }
        Parsing<N> Parsing { get; }
    }
    
    // public static Out<N> Out<N>(params IResult<N>[] results)
    //     => new OutImpl<N>(results);
    //
    // public static Out<N> Out<N>(IEnumerable<IResult<N>> results)
    //     => new OutImpl<N>(results.ToArray());
    

    public record Result<N>(Context Context, Parsing<N> Parsing) : IResult<N>;

    public abstract class Parser
    {
        public static Parser<N> Create<N>(Func<Context, Out<N>?> fn) 
            => new(fn);

        public static Parser<N> Create<N>(Func<IParser<N>> fn)
            => new(fn);
    }

    public class Parser<N> : Parser, IParser<N>
    {
        private readonly Lazy<(Func<Context, Out<N>?> Fn, Spacing Spacing)> _lz;

        public Spacing Spacing => _lz.Value.Spacing;
        protected Func<Context, Out<N>?> Parse => _lz.Value.Fn;

        public Parser(Func<Context, Out<N>?> parse, Spacing? spacing = null)
        {
            _lz = new Lazy<(Func<Context, Out<N>?>, Spacing)>(() => 
                (parse, spacing ?? Spacing.Empty)
            );
        }

        public Parser(Func<IParser<N>> parse)
        {
            _lz = new Lazy<(Func<Context, Out<N>?>, Spacing)>(() =>
            {
                var fn = parse();
                return (x => fn.Run(x), fn.Spacing);
            });
        }

        public Out<N>? Run(Context x0)
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
    
    public record ParserExp<N>(Func<Context, Out<N>> parse, Spacing? spacing = null) : IParser<N>
    {
        public Out<N> Run(Context x)
            => parse(x);

        public Spacing Spacing => spacing ?? Spacing.Empty;
    }

    public interface IParser<out N>
    {
        Out<N>? Run(Context x);
        Spacing Spacing { get; }
    }
}

public record Spacing(IEnumerable<char> SpaceChars, IEnumerable<char> NonSpaceChars)
{
    public static readonly Spacing Empty = new([], []);
}

public static class ParseResultExtensions 
{
    public static ParserOps.IResult<T2> Map<T, T2>(this ParserOps.IResult<T> result, Func<(ParserOps.Context Context, Parsing<T> Parsing), (ParserOps.Context, Parsing<T2>)> map)
    {
        var mapped = map((result.Context, result.Parsing));
        return new ParserOps.Result<T2>(mapped.Item1, mapped.Item2);
    }
}