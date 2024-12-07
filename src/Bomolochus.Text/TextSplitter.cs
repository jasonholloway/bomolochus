namespace Bomolochus.Text;

public class TextSplitter
{
    private readonly ReadableReader _reader;
    private Split? _lastSplit;

    public static TextSplitter Create(Readable readable)
        => new(ReadableReader.Create(readable), null);
    
    public TextSplitter Clone() => new(_reader.Clone(), _lastSplit);
    
    private TextSplitter(ReadableReader reader, Split? lastSplit)
    {
        _reader = reader;
        _lastSplit = lastSplit;
    }

    public Readable Staged => _reader.Staged;

    public Split Split()
    {
        var readable = _reader.Emit();
        return _lastSplit = new Split(_lastSplit, readable);
    }

    public void Reset()
        => _reader.Reset();
    
    public bool TryReadChar(char @char, out Readable claimed)
        => _reader.TryReadChar(@char, out claimed);

    public bool TryReadChar(out char @char)
        => _reader.TryReadChar(out @char);
    
    public int ReadCharsWhile(Func<char, int, bool> predicate)
        => _reader.ReadCharsWhile(predicate);

    public int ReadCharsWhile(Predicate<char> predicate)
        => ReadCharsWhile((c, _) => predicate(c));

    public string ReadAll()
        => _reader.ReadAll();
};