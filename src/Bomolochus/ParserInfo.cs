namespace Bomolochus;

public record ParserInfo(Spacing? Spacing)
{
    public static ParserInfo Empty = new(Spacing: null);
}