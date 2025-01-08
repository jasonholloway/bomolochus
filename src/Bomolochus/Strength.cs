using System.Collections;

namespace Bomolochus;

public readonly struct Strength(int? value)
{
    public readonly int Value = value ?? -1;

    public bool IsEmpty => Value < 0;

    public static readonly Strength Empty = new(-1);

    public static Strength From(int? value)
        => new(value);

    public static Strength Max(Strength l, Strength r) 
        => Math.Max(l.Value, r.Value);
    
    public static implicit operator int(Strength s) => s.Value;
    public static implicit operator Strength(int? i) => new(i);
    
    public static bool operator <(Strength l, Strength r)
        => l.Value < r.Value;
    
    public static bool operator >(Strength l, Strength r)
        => l.Value > r.Value;

    public static bool operator <=(Strength l, Strength r)
        => l.Value <= r.Value;
    
    public static bool operator >=(Strength l, Strength r)
        => l.Value >= r.Value;

    public static readonly IComparer<Strength> Comparer = new _Comparer();

    class _Comparer : IComparer<Strength>
    {
        public int Compare(Strength x, Strength y)
        {
            return x.Value.CompareTo(y.Value);
        }
    }
}