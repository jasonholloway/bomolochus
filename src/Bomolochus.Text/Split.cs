namespace Bomolochus.Text;

public record Split(Split? Previous, Readable Readable)
{
    public override string ToString() => Readable.ToString();

    public bool IsEmpty => Readable.Size.Chars == 0;
}
