using Bomolochus.Text;

namespace Bomolochus;

public static class ExtentExtensions
{
    public static IEnumerable<Parsed> FindParseds(this Extent root, int line, int col)
        => root.FindDescendent(line, col)
            .LineageToRoot
            .SelectMany(e => e.Linked)
            .OfType<Parsed>();
}