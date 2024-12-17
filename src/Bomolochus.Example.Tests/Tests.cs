using Bomolochus.Runner;
using Bomolochus.Text;
using NUnit.Framework;

namespace Bomolochus.Example.Tests;

using static Printer;
using static ParserOps;

public class Tests
{
    [TestCase("123", "Number(123)")]
    [TestCase("(123)", "(Number(123))")]
    [TestCase("  ( 123) ", "(Number(123))")]
    [TestCase("Hello", "Ref(Hello)")]
    [TestCase("\"Hello\"", "String(Hello)")]
    [TestCase("A = 5", "Is[Ref(A), Number(5)]")]
    [TestCase("Name = /^blah.*/", "Is[Ref(Name), Regex(^blah.*)]")]
    [TestCase("Cat = Dog = Ape", "Is[Ref(Cat), Ref(Dog), Ref(Ape)]")]
    [TestCase("A & B", "And[Ref(A), Ref(B)]")]
    [TestCase("A&B", "And[Ref(A), Ref(B)]")]
    [TestCase("A | B", "Or[Ref(A), Ref(B)]")]
    [TestCase("A & B | C", "Or[And[Ref(A), Ref(B)], Ref(C)]")]
    [TestCase("A | B & C", "Or[Ref(A), And[Ref(B), Ref(C)]]")]
    [TestCase("A = 1 & B = 2", "And[Is[Ref(A), Number(1)], Is[Ref(B), Number(2)]]")]
    [TestCase("A = B = 3", "Is[Ref(A), Ref(B), Number(3)]")]
    [TestCase("A = 1 & B = 2 | C", "Or[And[Is[Ref(A), Number(1)], Is[Ref(B), Number(2)]], Ref(C)]")]
    [TestCase("(A | B) & C", "And[(Or[Ref(A), Ref(B)]), Ref(C)]")]
    [TestCase("((A) | (B = C))", "(Or[(Ref(A)), (Is[Ref(B), Ref(C)])])")]
    [TestCase("A = (1 & B)", "Is[Ref(A), (And[Number(1), Ref(B)])]")]
    [TestCase("A.B", "Prop(Ref(A), Ref(B))")]
    [TestCase("A.B.C", "Prop(Prop(Ref(A), Ref(B)), Ref(C))")]
    [TestCase("A.B = C.D", "Is[Prop(Ref(A), Ref(B)), Prop(Ref(C), Ref(D))]")]
    [TestCase("A.B = 1", "Is[Prop(Ref(A), Ref(B)), Number(1)]")]
    [TestCase("A.B=1", "Is[Prop(Ref(A), Ref(B)), Number(1)]")]
    [TestCase("A.B = C.D & E.F", "And[Is[Prop(Ref(A), Ref(B)), Prop(Ref(C), Ref(D))], Prop(Ref(E), Ref(F))]")]
    [TestCase("A = (1|2|3)", "Is[Ref(A), (Or[Number(1), Number(2), Number(3)])]")]
    [TestCase("***", "!Noise")]
    [TestCase("A = ", "!Is[Ref(A), !?]")]
    [TestCase("A.B = 3", "Is[Prop(Ref(A), Ref(B)), Number(3)]")]
    [TestCase("", "!?")]
    [TestCase("[1, 2]", "[Number(1), Number(2)]")]
    [TestCase("[1, ***]", "![Number(1), !Noise]")]
    [TestCase("[1, ]", "![Number(1), !?]")]
    [TestCase("[blah", "!Noise")]
    public void ParsesExpressions(string text, string expected)
    {
        var tree = ExampleParser.ParseExpression.Parse(text);
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);
        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));
    }
    
    
    
    
    //but to combine over sub-parsers with different space chars
    //these sub-parsers need to advertise their space chars
    //ie they can't just update the context via a delegate
    //they need to advertise!

    [TestCase("Z", "Number(1)")]
    [TestCase("\n", "Number(1)")]
    [TestCase(" ", "Number(1)")]
    public void ParsesSpaces(string text, string expected)
    {
        var tree = (
            from c in OneOf(
                Match('\n'),
                Match('Z'),
                Match(' '))
            select new Node.Number(1)
        ).Parse(text);
            
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));    
    }
    
    [TestCase("ABC", "String(ABC)")]
    public void ParsesSequence(string text, string expected)
    {
        var tree = (
            from a in Match('A')
            from b in Match('B')
            from c in Match('C')
            select new Node.String(a + b + c)
        ).Parse(text);
            
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));    
    }
    
    [TestCase("A&B", "And[Ref(A), Ref(B)]")]
    public void ParsesLeftRecursive(string text, string expected)
    {
        RunStep<Node> parseExp = null!;

        var parseVar = new RunStep<Node>("Var", () => 
            from name in MatchWord()
            select new Node.Ref(name)
            );

        parseExp = new RunStep<Node>("Exp", () =>
            OneOf(
                parseVar,
                from left in parseExp
                from op in Match('&')
                from right in parseExp
                select new Node.And([left, right])
            ));
        
        var tree = parseExp.Parse(text);
            
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));    
    }
    
    [TestCase("AB", "String(AB)")]
    public void ParsesDisjunction(string text, string expected)
    {
        var tree = (
            from a in OneOf(Match('A'), Match('B'))
            from b in OneOf(Match('A'), Match('B'))
            select new Node.String(a + b)
        ).Parse(text);
            
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));    
    }
    
    [TestCase("AB", "String(AB)")]
    public void ParsesDisjunctionWithUncertainty(string text, string expected)
    {
        var tree = (
            from a in OneOf(Match('A').WithError("blah"), Match('A'))
            from b in Match('B')
            select new Node.String(a + b)
        ).Parse(text);
            
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));    
    }
    
    [TestCase("1,2", "[Number(1), Number(2)]")]
    public void ParsesList(string text, string expected)
    {
        var tree = (
            from els in ParseDelimitedList(
                from n in Match(c => c is '1' or '2')
                select (Node)new Node.Number(int.Parse(n.ReadAll())),
                Match(',')
                )
            select new Node.List(els)
        ).Parse(text);
            
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));    
    }
    
    [TestCase("ABBBZZZ", "String(ABBB)")]
    public void ParsesExpansions(string text, string expected)
    {
        var tree = (
            Expand(
                Match('A'), 
                prev => Match('B').Select(b => Readable.From(prev.ReadAll() + b.ReadAll()))
                )
                .Select(s => new Node.String(s))
            // from expanded in Expand(
            //     Match('A'), 
            //     prev => Match('B').Select(b => Readable.From(prev.ReadAll() + b.ReadAll())))
            // select new Node.String(expanded)
        ).Parse(text);
            
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));    
    }
    
    [TestCase("A", "Ref(A)")]
    public void ParseExpansion2(string text, string expected)
    {
        var tree = (
            Expand((IStep<Node>)ExampleParser.ParseRef,
                left => 
                    from right in ExampleParser.ParseRef
                    select new Node.Prop(left, right)
                )
        ).Parse(text);
            
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));    
    }
    
    [TestCase("{ Woof(1) }", "{Call(Ref(Woof), Number(1))}")]
    [TestCase("{ Woof(1); Meeow(2) }", "{Call(Ref(Woof), Number(1)); Call(Ref(Meeow), Number(2))}")]
    public void ParsesStatements(string text, string expected)
    {
        var tree = ExampleParser.ParseStatementBlock.Parse(text);
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);
        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase("A { B }", "{Rule(Ref(A), {Ref(B)})}")]
    [TestCase("{ B }", "{Rule(NULL, {Ref(B)})}")]
    [TestCase("{ Emit(123) }", "{Rule(NULL, {Call(Ref(Emit), Number(123))})}")]
    [TestCase("0 { Emit(123) }", "{Rule(Number(0), {Call(Ref(Emit), Number(123))})}")]
    [TestCase("A = 1 { Emit(123) }", "{Rule(Is[Ref(A), Number(1)], {Call(Ref(Emit), Number(123))})}")]
    [TestCase("{ Count += 1 }", "{Rule(NULL, {Incr(Ref(Count), Number(1))})}")]
    [TestCase(
        """
        A { 1 }
        B { 2 }
        """
        , "{Rule(Ref(A), {Number(1)}), Rule(Ref(B), {Number(2)})}"
        )]
    [TestCase("A { 1 }; B { 2 }", "{Rule(Ref(A), {Number(1)}), Rule(Ref(B), {Number(2)})}")]
    public void ParsesRules(string text, string expected)
    {
        var tree = ExampleParser.ParseRules.Parse(text);
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);
        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase("13", "<0,2>Number(13)")]
    [TestCase("A = 7", "<0,5>Is[<0,1>Ref(A), <0,1>Number(7)]")]
    [TestCase("A = (1 & B)", "<0,11>Is[<0,1>Ref(A), <0,7>(<0,5>And[<0,1>Number(1), <0,1>Ref(B)])]")]
    [TestCase("(1 & 20)", "<0,8>(<0,6>And[<0,1>Number(1), <0,2>Number(20)])")]
    public void ParsesExpressionsWithSizes(string text, string expected)
    {
        var tree = ExampleParser.ParseExpression.Parse(text);
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);
        
        Assert.That(Print(doc, Flags.WithSizes), Is.EqualTo(PrepNodeString(expected)));
    }
    
    [TestCase(
        """
        Z = (
          1 & 2
          )
        """, 
        "<0,0-2,3>Is[<0,0-0,1>Ref(Z), <0,4-2,3>(<1,2-1,7>And[<1,2-1,3>Number(1), <1,6-1,7>Number(2)])]")]
    [TestCase(
        """
        
         1
          
        """, 
        "<1,1-1,2>Number(1)", Description = "space should be gutterised")]
    [TestCase(" 13  ", "<0,1-0,3>Number(13)")]
    [TestCase("13  ", "<0,0-0,2>Number(13)")]
    [TestCase("(7)", "<0,0-0,3>(<0,1-0,2>Number(7))")]
    [TestCase("A = (1 & B)", "<0,0-0,11>Is[<0,0-0,1>Ref(A), <0,4-0,11>(<0,5-0,10>And[<0,5-0,6>Number(1), <0,9-0,10>Ref(B)])]")]
    [TestCase(" Z = 1 ", "<0,1-0,6>Is[<0,1-0,2>Ref(Z), <0,5-0,6>Number(1)]")]
    [TestCase("A.B", "<0,0-0,3>Prop(<0,0-0,1>Ref(A), <0,2-0,3>Ref(B))")]
    public void ParsesExpressionsWithExtents(string text, string expected)
    {
        var tree = ExampleParser.ParseExpression.Parse(text);
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);
        
        Assert.That(Print(doc, Flags.WithExtents), Is.EqualTo(PrepNodeString(expected)));
    }

    [TestCase("**", "!Noise")]
    [TestCase("Bob = **", "!Is[Ref(Bob), !Noise]")]
    [TestCase("(** = 3)", "!(!Is[!Noise, Number(3)])")]
    [TestCase("(Bob = **) = **", "!Is[!(!Is[Ref(Bob), !Noise]), !Noise]")]
    [TestCase("(Bob = **)", "!(!Is[Ref(Bob), !Noise])")]
    public void ParseUncertainties(string text, string expected)
    {
        var tree = ExampleParser.ParseExpression.Parse(text);
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);
        
        Assert.That(Print(doc), Is.EqualTo(PrepNodeString(expected)));
    }

    [TestCase("(Bob = 123)", 0, 2, "Ref(Bob)")]
    [TestCase("(Bob = 123)", 0, 9, "Number(123)")]
    [TestCase("(Bob = 123)", 0, 5, "Is[Ref(Bob), Number(123)]")]
    [TestCase("""
              
              (Bob = 123)
              
              """, 1, 8, "Number(123)")]
    [TestCase("A = B = C", 0, 6, "Is[Ref(A), Ref(B), Ref(C)]")]
    public void FindsNodes(string text, int line, int col, string expected)
    {
        var tree = ExampleParser.ParseExpression.Parse(text);
        var doc = new ParsedDoc(Extent.Combine(tree?.Left, tree?.Centre, tree?.Right), tree);

        var found = doc.Extent.FindParseds(line, col).FirstOrDefault();
        
        Assert.That(Print(doc, found), Is.EqualTo(PrepNodeString(expected)));
    }

    static string PrepNodeString(string raw) =>
        string.Join(' ', raw.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
}