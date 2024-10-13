using Bomolochus.Text;

namespace Bomolochus;

public static class Printer
{
    public static string Print(ParsedDoc doc, Flags flags = Flags.Default) 
        => Print(doc, doc.Root, flags);

    public static string Print(ParsedDoc doc, Parsed? parsed, Flags flags = Flags.Default)
        => parsed switch
        {
            Parsed<Parsable> { Value: var v } => PrintNode(doc, v, flags),
            null => "NULL",
            _ => "",
        };

    [Flags]
    public enum Flags
    {
        Default,
        WithSizes,
        WithExtents
    }

    static string PrintNode(ParsedDoc doc, Parsable node, Flags flags)
    {
        var parsed = node?.Parsed;

        return
            (flags.HasFlag(Flags.WithExtents)
             && doc.Extent.GetBoundsOf(parsed.Centre) is ({ } @from, { } to)
                ? $"<{@from.Lines},{@from.Cols}-{to.Lines},{to.Cols}>"
                : "") +
            (flags.HasFlag(Flags.WithSizes)
             && parsed is not null
             && parsed.Centre.Readable.Size is { } vec
                ? $"<{vec.Lines},{vec.Cols}>"
                : "") +
            (parsed?.Addenda.Certainty < 1 ? "!" : "") +
            (node switch
            {
                Node.Ref(var s) => $"Ref({s.ReadAll()})",
                Node.Number(var n) => $"Number({n})",
                Node.String(var s) => $"String({s?.ReadAll()})",
                Node.Regex(var s) => $"Regex({s.ReadAll()})",
                Node.Is(var nodes) => $"Is[{string.Join(", ", nodes.Select(_Print))}]",
                Node.And(var nodes) => $"And[{string.Join(", ", nodes.Select(_Print))}]",
                Node.Or(var nodes) => $"Or[{string.Join(", ", nodes.Select(_Print))}]",
                Node.Prop(var left, var right) => $"Prop({_Print(left)}, {_Print(right)})",
                Node.Rule(var left, var right) => $"Rule({_Print(left)}, {_Print(right)})",
                Node.Call(var left, var args) => $"Call({_Print(left)}, {_Print(args[0])})",
                Node.Incr(var left, var right) => $"Incr({_Print(left)}, {_Print(right)})",
                Node.List(var nodes) => $"[{string.Join(", ", nodes.Select(_Print))}]",
                Node.ExpressionBlock(var inner) => $"({_Print(inner)})",
                Node.StatementBlock(var nodes) => $"{{{string.Join("; ", nodes.Select(_Print))}}}",
                Node.Rules(var inner) => $"{{{string.Join(", ", inner.Select(_Print))}}}",
                Node.Expect => $"?",
                Node.Noise => "Noise",
                Node.Delimiter => "Delimiter",
                Node.Syntax => "Syntax",
                null => "NULL",
                _ => throw new Exception($"Bad value, can't print: {node}")
            });

        string _Print(Parsable n)
            => PrintNode(doc, n, flags);
    }
}
