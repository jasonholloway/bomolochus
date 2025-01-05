namespace Bomolochus;

using static ParserOps;

//todo Contexts need to be immutable for this kind of parsing
//poss supporting ref counting

public static class ParserFnExtensions
{
    // public static Parser<B> Select<A, B>(
    //     this IParser<A> fn,
    //     Func<A, B> map) =>
    //     new(
    //         parse: x => fn.Run(x)?
    //             .Select(p => Parsing.From(map(p.Val), [p], p.Addenda)),
    //         spacing: fn.Spacing
    //     );
    //
    // public static ParserExp<C> SelectMany<A, B, C>(
    //     this IParser<A> fn0,
    //     Func<A, IParser<B>> map,
    //     Func<A, B, C> join) =>
    //     new(
    //         parse: x0 => Out(
    //             (fn0.Run(x0)?.Results ?? [])
    //                 .SelectMany<IResult<A>, IResult<C>>(r1 =>
    //                 {
    //                     if (r1 is { Context: var x1, Parsing: { Val: var v1 } p1 })
    //                     {
    //                         var fn1 = map(v1);
    //                                 
    //                         return (fn1.Run(x1)?.Results ?? [])
    //                             .SelectMany<IResult<B>, IResult<C>>(r2 =>
    //                             {
    //                                 if (r2 is { Context: var x2, Parsing: { Val: var v2 } p2 })
    //                                 {
    //                                     return [
    //                                         new Result<C>(x2, Parsing.From(join(v1, v2), [p1, p2], p1.Addenda + p2.Addenda))
    //                                     ];
    //                                 }
    //
    //                                 return [];
    //                             });
    //                     }
    //
    //                     return [];
    //                 })
    //             ),
    //         spacing: fn0.Spacing);
    
    //todo: what about space parsing before fn1 above? it's nested
}

