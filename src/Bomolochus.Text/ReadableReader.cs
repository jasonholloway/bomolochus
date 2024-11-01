using System.Collections.Immutable;
using System.Text;

namespace Bomolochus.Text;

    //todo OPTIMISE: allow ReadableNodes to have List<Readable> children
    //with frame state containing integer cursor
    
    //todo OPTIMISE: store a current buffer
    //instead of popping/pushing on every character!

/// <summary>
/// Clonable walker through a Readable to yield spans to read 
/// </summary>
public class ReadableReader
{
    public enum State { ReadLeft, ReadRight }
    
    private ImmutableStack<(State State, Readable Readable)> _stack;
    private Readable _staged;

    public ReadableReader Clone() => new(_stack, _staged);

    public static ReadableReader Create(Readable readable) 
        => new(
            ImmutableStack<(State, Readable)>.Empty.Push((State.ReadLeft, readable)), 
            Readable.Empty
        );

    private ReadableReader(ImmutableStack<(State, Readable)> stack, Readable staged)
    {
        _stack = stack;
        _staged = staged;
    }

    public Readable Emit()
    {
        var emittable = _staged;
        _staged = Readable.Empty;
        return emittable;
    }

    public void Reset()
    {
        Push(_staged);
        _staged = Readable.Empty;
    }

    public TAc Visit<TAc>(TAc seed, Visitor<TAc> visitor)
    {
        //todo does this not visit staged?
        var ac = seed;
        
        while (TryPop(out var buffer))
        {
            ac = visitor(ac, buffer.Span);
        }

        return ac;
    }

    public delegate TAc Visitor<TAc>(TAc ac, ReadOnlySpan<char> span);

    void Push(Readable readable)
    {
        if (readable != Readable.Empty)
        {
            _stack = _stack.Push((State.ReadLeft, readable));
        }
    }

    bool TryPop(out ReadableBuffer buffer)
    {
        while (!_stack.IsEmpty)
        {
            _stack = _stack.Pop(out var frame);
            
            switch (frame)
            {
                case (_, ReadableEmpty):
                    continue;
                
                case (_, ReadableBuffer leaf):
                    buffer = leaf;
                    return true;

                case (State.ReadLeft, ReadableNode node):
                {
                    _stack = _stack.Push((State.ReadRight, node));
                    _stack = _stack.Push((State.ReadLeft, node.Left));
                    continue;
                }

                case (State.ReadRight, ReadableNode node):
                {
                    _stack = _stack.Push((State.ReadLeft, node.Right));
                    continue;
                }

                default: throw new NotImplementedException();
            }
        }
        
        buffer = default!;
        return false;
    }

    public bool TryReadChar(char @char, out Readable claimed)
    {
        while (TryPop(out var buff))
        {
            var span = buff.Span;

            if (span.Length > 0)
            {
                if (span[0] == @char 
                    && buff.TrySplit(1, out var split))
                {
                    claimed = split.Left;
                    _staged += split.Left;
                    Push(split.Right);
                    return true;
                }
                
                Push(buff);
                break;
            }
        }
        
        claimed = Readable.Empty;
        return false;
    }

    public bool TryReadChar(out char @char)
    {
        while (TryPop(out var buff))
        {
            var span = buff.Span;

            if (buff.TrySplit(1, out var split))
            {
                @char = span[0];
                _staged += split.Left;
                Push(split.Right);
                return true;
            }
        }

        @char = default;
        return false;
    }

    public int ReadCharsWhile(Predicate<char> predicate)
        => ReadCharsWhile((c, _) => predicate(c));
    
    public int ReadCharsWhile(Func<char, int, bool> predicate)
    {
        var claimed = Readable.Empty;
        var i = 0;
        
        while (TryPop(out var buff))
        {
            var j = 0;
            var span = buff.Span;
            
            for (; j < span.Length && predicate(span[j], i); j++, i++) {}

            if (j > 0 && buff.TrySplit(j, out var split))
            {
                claimed += split.Left;
                Push(split.Right);
            }
            else
            {
                Push(buff);
                break;
            }
        }

        _staged += claimed;
        return i;
    }

    public string ReadAll()
        => Visit(new StringBuilder(), (sb, span) => sb.Append(span)).ToString();
}