using NUnit.Framework;

namespace Bomolochus.Text.Tests;

public class ExtentTests
{
    [Test]
    public void ReadsIntoSplits()
    {
        var splitter = TextSplitter.Create("kitten mews meekly");

        Assert.That(splitter.ReadCharsWhile(c => c != ' '), Is.EqualTo(6));
        Assert.That(splitter.ReadCharsWhile(c => c == ' '), Is.EqualTo(1));
        Assert.That(splitter.ReadCharsWhile(c => c != ' '), Is.EqualTo(4));
        Assert.That(splitter.Split().Readable.ReadAll(), Is.EqualTo("kitten mews"));

        Assert.That(splitter.ReadCharsWhile(c => c == ' '), Is.EqualTo(1));
        Assert.That(splitter.ReadCharsWhile(c => c != ' '), Is.EqualTo(6));
        Assert.That(splitter.ReadCharsWhile(_ => true), Is.EqualTo(0));
        Assert.That(splitter.Split().Readable.ReadAll(), Is.EqualTo(" meekly"));
        
        Assert.That(splitter.ReadCharsWhile(_ => true), Is.EqualTo(0));
    }
    
    [Test]
    public void ReadsIntoSplits_WithTransactions()
    {
        var s0 = TextSplitter.Create("kitten mews meekly");
        Assert.That(s0.ReadCharsWhile(c => c != ' '), Is.EqualTo(6));
        Assert.That(s0.ReadCharsWhile(c => c == ' '), Is.EqualTo(1));
        Assert.That(s0.Split().Readable.ReadAll(), Is.EqualTo("kitten "));

        var s1 = s0.Clone();
        Assert.That(s1.ReadCharsWhile(c => c != ' '), Is.EqualTo(4));
        Assert.That(s1.ReadCharsWhile(c => c == ' '), Is.EqualTo(1));
        Assert.That(s1.Split().Readable.ReadAll(), Is.EqualTo("mews "));
        
        var s2 = s1.Clone();
        Assert.That(s2.ReadCharsWhile(c => c != ' '), Is.EqualTo(6));
        Assert.That(s2.Split().Readable.ReadAll(), Is.EqualTo("meekly"));

        Assert.That(s1.ReadCharsWhile(c => c != ' '), Is.EqualTo(6));
        Assert.That(s1.Split().Readable.ReadAll(), Is.EqualTo("meekly"));

        Assert.That(s0.ReadCharsWhile(c => c != ' '), Is.EqualTo(4));
        Assert.That(s0.Split().Readable.ReadAll(), Is.EqualTo("mews"));
    }
    
    [Test]
    public void Reads_WithCheckpoints()
    {
        var reader0 = ReadableReader.Create("kitten mews meekly");
        Assert.That(reader0.ReadCharsWhile(c => c != ' '), Is.EqualTo(6));
        Assert.That(reader0.ReadCharsWhile(c => c == ' '), Is.EqualTo(1));
        Assert.That(reader0.Emit().ReadAll(), Is.EqualTo("kitten "));

        var reader1 = reader0.Clone();
        Assert.That(reader1.ReadCharsWhile(c => c != ' '), Is.EqualTo(4));
        Assert.That(reader1.ReadCharsWhile(c => c == ' '), Is.EqualTo(1));
        Assert.That(reader1.Emit().ReadAll(), Is.EqualTo("mews "));

        var reader2 = reader1.Clone();
        Assert.That(reader2.ReadCharsWhile(c => c != ' '), Is.EqualTo(6));
        Assert.That(reader2.Emit().ReadAll(), Is.EqualTo("meekly"));
    }
    
    //and given some splits, we can convert them into Extents
    //but doing so involves sealing the parse tree
    //
    //well, we'd walk backwards through the parse tree
    //converting splits to Extents and as we ascend, grouping the Extents under us

    // [Test]
    // public void ConvertsSplitsToExtents()
    // {
    //     var reader = new TextSplitter(Readable.From("kitten mews meekly"));
    //     
    //     Assert.That(reader.TryReadChars(c => c != ' ', out var part0) && part0.ReadAll() == "kitten");
    //     var text0 = reader.Split();
    //     Assert.That(text0.Get().Readable.ReadAll(), Is.EqualTo("kitten"));
    //     
    //     Assert.That(reader.TryReadChars(c => c == ' ', out var part1) && part1.ReadAll() == " ");
    //     var text1 = reader.Split();
    //     Assert.That(text1.Get().Readable.ReadAll(), Is.EqualTo(" "));
    //     
    //     Assert.That(reader.TryReadChars(c => c != ' ', out var part2) && part2.ReadAll() == "mews");
    //     var text2 = reader.Split();
    //     Assert.That(text2.Get().Readable.ReadAll(), Is.EqualTo("mews"));
    //
    //     var extent = reader.BuildExtentTree();
    //     Assert.That(extent.ReadAll(), Is.EqualTo("kitten mews"));
    //     
    //     Assert.That(text0.Get().Readable.ReadAll(), Is.EqualTo("kitten"));
    //     Assert.That(text1.Get().Readable.ReadAll(), Is.EqualTo(" "));
    //     Assert.That(text2.Get().Readable.ReadAll(), Is.EqualTo("mews"));
    // }
    
    // [Test]
    // public void Contains()
    // {
    //     var tree = Extent.From(
    //         Extent.From(
    //             Extent.From((0, 0), "a"),
    //             Extent.From((0, 1), "b")
    //         ),
    //         Extent.From((0, 2), "c")
    //         );
    //
    //     Assert.That(tree.Contains((ExtentLeaf)Right(tree)), Is.True);
    //     Assert.That(tree.Contains((ExtentLeaf)Extent.From((0, 3), "")), Is.False);
    //     Assert.That(tree.Contains((ExtentLeaf)Extent.From((3, 0), "")), Is.False);
    //     Assert.That(tree.Contains((ExtentLeaf)Left(Left(tree))), Is.True);
    //     Assert.That(tree.Contains((ExtentLeaf)Right(Left(tree))), Is.True);
    // }
    
    [Test]
    public void Groups_Leaf()
    {
        var leaf = Extent.From("hello");

        var grouped = Extent.Group(leaf, leaf);

        Assert.That(grouped, Is.EqualTo(leaf));
    }
    
    [Test]
    public void Groups_Simple()
    {
        var tree = Extent.Combine(
            Extent.Combine(
                Extent.From("a"),
                Extent.From("b")
            ),
            Extent.From("c")
            );

        //group b + c
        var grouped = Extent.Group(Right(Left(tree)), Right(tree));

        Assert.That(grouped.ReadAll(), Is.EqualTo("bc"));
        Assert.That(tree.ReadAll(), Is.EqualTo("abc"));
        
        Assert.That(grouped, Is.EqualTo(
            Extent.Combine(
                Extent.From("b"),
                Extent.From("c")
            )));
        
        Assert.That(tree, Is.EqualTo(
            Extent.Combine(
                Extent.From("a"),
                grouped
            )));
    }
    
    
    
    [Test]
    public void Groups_TwoSurplus()
    {
        var tree = Extent.Combine(
            Extent.Combine(
                Extent.From("a"),
                Extent.Combine(
                    Extent.From("b"),
                    Extent.From("c")
                    )
            ),
            Extent.From("d")
            );

        //group c + d
        var grouped = Extent.Group(Right(Right(Left(tree))), Right(tree));
        
        Assert.That(grouped, Is.EqualTo(
            Extent.Combine(
                Extent.From("c"),
                Extent.From("d")
            )));
    }
    
    [Test]
    public void Groups_SubTrees()
    {
        var tree = Extent.Combine(
            Extent.Combine(
                Extent.From("a"),
                Extent.Combine(
                    Extent.From("b"),
                    Extent.From("c")
                    )
            ),
            Extent.Combine(
                Extent.Combine( 
                    Extent.From("d"),
                    Extent.From("e")
                    ),
                Extent.From("f")
            )
        );

        //group c + d
        var grouped = Extent.Group(
            Right(Right(Left(tree))), //c
            Left(Right(tree)) //d,e
            );
        
        Assert.That(grouped, Is.EqualTo(
            Extent.Combine(
                Extent.From("c"),
                Extent.Combine(
                    Extent.From("d"),
                    Extent.From("e")
                )
            )));
    }

    static Extent Left(Extent ex) => ((ExtentNode)ex).Left;
    static Extent Right(Extent ex) => ((ExtentNode)ex).Right;
}