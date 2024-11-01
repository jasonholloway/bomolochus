using NUnit.Framework;

namespace Bomolochus.Text.Tests;

public class ReadableTests
{
    [Test]
    public void ReadAll_Simple()
    {
        var reader = Readable.From("big dog yaps").GetReader();
        Assert.That(reader.ReadAll(), Is.EqualTo("big dog yaps"));
    }
    
    [Test]
    public void ReadAll_Tree()
    {
        var reader = new ReadableNode(new ReadableNode(Readable.From("big"), Readable.From(" dog")), Readable.From(" yaps")).GetReader();
        Assert.That(reader.ReadAll(), Is.EqualTo("big dog yaps"));
    }
    
    [Test]
    public void ReadAll_Tree_MarksEnd()
    {
        var reader = new ReadableNode(
            Readable.From("woof"),
            Readable.From("")
            ).GetReader();

        reader.ReadCharsWhile((_, _) => true);
        reader.Emit();

        Assert.That(reader.TryReadChar(out _), Is.False);
    }
    
    [Test]
    public void ReadIndividualChars()
    {
        var reader = 
            new ReadableNode(
                new ReadableNode(
                    Readable.From("b"), 
                    Readable.From("ig")), 
                Readable.From(" d")
            ).GetReader();

        Assert.That(reader.TryReadChar(out var c0));
        Assert.That(c0, Is.EqualTo('b'));
        
        Assert.That(reader.TryReadChar(out var c1));
        Assert.That(c1, Is.EqualTo('i'));
        
        Assert.That(reader.TryReadChar(out var c2));
        Assert.That(c2, Is.EqualTo('g'));
        
        Assert.That(reader.TryReadChar(out var c3));
        Assert.That(c3, Is.EqualTo(' '));
        
        Assert.That(reader.TryReadChar(out var c4));
        Assert.That(c4, Is.EqualTo('d'));
        
        Assert.That(reader.TryReadChar(out _), Is.False);
    }
    
    [Test]
    public void ReadChars()
    {
        var reader = TextSplitter.Create(
            new ReadableNode(
                new ReadableNode(
                    Readable.From("b"), 
                    Readable.From("ig")), 
                Readable.From(" dog")
            ));

        Assert.That(reader.ReadCharsWhile(c => c is 'b' or 'i'), Is.EqualTo(2));
        Assert.That(reader.ReadCharsWhile(c => c is 'g'), Is.EqualTo(1));
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo("big"));

        Assert.That(reader.ReadCharsWhile(c => true), Is.EqualTo(4));
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo(" dog"));

        Assert.That(reader.ReadCharsWhile(c => true), Is.EqualTo(0));
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo(""));
    }
    
    [Test]
    public void ReadPositionalChars()
    {
        var reader = TextSplitter.Create(
            new ReadableNode(
                new ReadableNode(
                    Readable.From("b"), 
                    Readable.From("ig")), 
                Readable.From(" dog")
            ));

        var chars = reader.ReadCharsWhile((c, i) =>
            (i, c) switch
            {
                (0, 'b') => true,
                (1, 'i') => true,
                (2, 'g') => true,
                _ => false
            });
        
        Assert.That(chars, Is.EqualTo(3));
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo("big"));
    }
    
    [Test]
    public void ReadChars_Reset()
    {
        var reader = TextSplitter.Create(
            new ReadableNode(
                new ReadableNode(
                    Readable.From("b"),
                    Readable.From("ig")),
                Readable.From(" dog")
            ));

        Assert.That(reader.ReadCharsWhile(c => c is 'b' or 'i' or 'g' or ' '), Is.EqualTo(4));
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo("big "));

        Assert.That(reader.ReadCharsWhile(c => c is 'd' or 'o' or 'g'), Is.EqualTo(3));
        
        Assert.That(reader.ReadCharsWhile(c => true), Is.EqualTo(0));
        
        reader.Reset();

        Assert.That(reader.ReadCharsWhile(c => c is 'd' or 'o'), Is.EqualTo(2));
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo("do"));

        Assert.That(reader.ReadCharsWhile(c => true), Is.EqualTo(1));
        Assert.That(reader.Split().Readable.ReadAll(), Is.EqualTo("g"));
        
        Assert.That(reader.ReadCharsWhile(c => true), Is.EqualTo(0));
    }
    
    [Test]
    public void CanCheckpointRestore()
    {
        var readable = R("wo") + (R("of ") + (R("say") + R("s ") + R("the")) + (R(" ") + R("d"))) + R("og sometimes");
        var reader0 = ReadableReader.Create(readable);
        
        reader0.ReadCharsWhile(c => c != ' ');
        Assert.That(reader0.Emit().ReadAll(), Is.EqualTo("woof"));
        
        reader0.ReadCharsWhile(c => c == ' ');
        Assert.That(reader0.Emit().ReadAll(), Is.EqualTo(" "));
        
        reader0.ReadCharsWhile(c => c != ' ');
        Assert.That(reader0.Emit().ReadAll(), Is.EqualTo("says"));
        
        reader0.ReadCharsWhile(c => c == ' ');
        Assert.That(reader0.Emit().ReadAll(), Is.EqualTo(" "));

        var reader1 = reader0.Clone();
        
        reader1.ReadCharsWhile(c => c != ' ');
        Assert.That(reader1.Emit().ReadAll(), Is.EqualTo("the"));
        
        reader1.ReadCharsWhile(c => c == ' ');
        Assert.That(reader1.Emit().ReadAll(), Is.EqualTo(" "));

        var reader2 = reader1.Clone();
        
        reader2.ReadCharsWhile(c => c != ' ');
        Assert.That(reader2.Emit().ReadAll(), Is.EqualTo("dog"));
        
        //and now continue on upstream...
        reader0.ReadCharsWhile(c => c != ' ');
        Assert.That(reader0.Emit().ReadAll(), Is.EqualTo("the"));
    }
    
    [Test]
    public void SplitsBigBuffer()
    {
        var splitter0 = TextSplitter.Create("Bow wow wow woof woof woof");
        
        Assert.That(splitter0.ReadCharsWhile(c => c is not ' '), Is.GreaterThan(0));
        Assert.That(splitter0.Split().Readable.ReadAll(), Is.EqualTo("Bow"));

        var splitter1 = splitter0.Clone();
        Assert.That(splitter1.ReadCharsWhile(c => c is ' '), Is.GreaterThan(0));
        Assert.That(splitter1.Split().Readable.ReadAll(), Is.EqualTo(" "));
        
        var splitter2 = splitter1.Clone();
        Assert.That(splitter2.ReadCharsWhile(c => c is not ' '), Is.GreaterThan(0));
        Assert.That(splitter2.Split().Readable.ReadAll(), Is.EqualTo("wow"));
    }
    
    static Readable R(string s) => Readable.From(s);
}