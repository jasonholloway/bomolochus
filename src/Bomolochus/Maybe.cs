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
