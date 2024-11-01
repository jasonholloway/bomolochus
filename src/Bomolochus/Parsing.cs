using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

public interface Parsed
{
    Extent Centre { get; }
    Extent Left { get; }
    Extent Right { get; }
    Addenda Addenda { get; }
    IEnumerable<Parsed> Upstreams { get; }
}

public interface Parsed<out V> : Parsed
    // where V : Parsable
{
    V Value { get; }
}

public interface Parsing
{
    Addenda Addenda { get; }
    
    public static Parsing<T> From<T>(T val, Split split, Addenda? addenda = null)
        => new ParsingText<T>(val, split, IsSpace: val is Token.Space, addenda);
    
    public static Parsing<T> From<T>(T val, ImmutableArray<Parsing> upstreams, Addenda? addenda = null)    
        => new ParsingGroup<T>(val, upstreams, addenda);    
}

public interface Parsing<out N> : Parsing
{
    N Val { get; }
    // Parsing<N2> MapValue<N2>(Func<N, N2> fn);
    // Parsing<N2> SelectMany<N2>(Func<N, Parsing<N2>> fn);
}

public interface ParsingText : Parsing
{
    Split Text { get; }
    bool IsSpace { get; }
}

public interface ParsingGroup : Parsing
{
    ImmutableArray<Parsing> Upstreams { get; }
}

public record ParsingGroup<T>(T Val, ImmutableArray<Parsing> Upstreams, Addenda? Addenda = null) 
    : ParsingVal<T>(Val, Addenda ?? Addenda.Empty), ParsingGroup
{
    public override Parsing<T2> MapValue<T2>(Func<T, T2> fn)
        => new ParsingGroup<T2>(fn(Val), Upstreams, Addenda);
}

public record ParsingText<T>(T Val, Split Text, bool IsSpace = false, Addenda? Addenda = null) 
    : ParsingVal<T>(Val, Addenda ?? Addenda.Empty), ParsingText
{
    public override Parsing<N2> MapValue<N2>(Func<T, N2> fn)
        => new ParsingText<N2>(fn(Val), Text, IsSpace, Addenda);
}

public abstract record ParsingVal<T>(T Val, Addenda Addenda) : Parsing<T>
{
    public abstract Parsing<T2> MapValue<T2>(Func<T, T2> fn);
}