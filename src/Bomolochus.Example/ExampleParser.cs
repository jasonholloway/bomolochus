namespace Bomolochus.Example;

using static ParserOps;

public static class ExampleParser
{
    public static readonly RunStep<Node.Rules> ParseRules = new(
        "Rules", () =>
            from rules in ParseDelimitedList(ParseRule, OneOf(Match(';'), Match('\n')))
            select new Node.Rules(rules)
        );

    //interestingly, a table optimisation of the above would need to pre-expand ParseDelimitedList
    //ie the left leg of its resultant bind should be the visible thing
    //ie we wouldn't be going by 'names' so much as actual resultant steps formed into the bind tree
    //and the left leg of a bind always precedes the named container and is pristine
    
    public static readonly RunStep<Node> ParseExpression = new(
        "Expression", () => 
            OneOf(
                ParseExpressionBlock,
                ParseValueNode,
                ParseDisjunction,
                ParseConjunction,
                ParseEquality,
                ParseProp
            )
        );
    
    static readonly RunStep<Node> ParseDisjunction = new(
        "Disjunction", () => 
            from els in ParseDelimitedList(ParseExpression, Match('|'))
            where els.Length > 1
            select new Node.Or(els.ToArray()) 
        );
    
    static readonly RunStep<Node> ParseConjunction = new(
        "Conjunction", () =>
            from els in ParseDelimitedList(ParseExpression, Match('&'))
            where els.Length > 1
            select new Node.And(els.ToArray()) 
        );

    static readonly RunStep<Node> ParseEquality = new(
        "Equality", () =>
            from els in ParseDelimitedList(
                OneOf(ParseExpression, Expect("Expression expected")), 
                Match('=')
                )
            where els.Length > 1
            select new Node.Is(els.ToArray()) 
        );

    public static readonly RunStep<Node> ParseProp = new(
        "Prop", () =>
            Expand(ParseTerminal,
                left => 
                    from op in Match('.')
                    from right in ParseTerminal
                    select new Node.Prop(left, right)
            ));

    
    /*
     * (A | B) & C
     *
     * when we've read (A | B)
     * we're _certain_ we have here an expression
     * but then the next '&' tells us we need to step back and nest the current parsing
     *
     * having successfully parsed the expression
     * then there are continuations that are available
     *
     * with the current approach
     * we'd be wastefully trying different approaches
     * so an expression block would be parsed
     * but only the parsing of a conjunction (including a nested exp) would work
     * so to make it work... we'd need to treat an expression block as a separate expression type
     * instead of as a possible surrounding of any expression
     * (as making it always available opts us into the top level surrounding, which leads nowhere)
     *
     * WE REALLY NEED A CONTINUATION TABLE...
     * the wastefulness of the current parsing is grotesque
     * is what we're building a parser or a regular expression machine?
     */
    
    
    static readonly RunStep<Node.Rule> ParseRule = new(
        "Rule", () => 
            from expr in Optional(ParseExpression)
            from block in OneOf(ParseStatementBlock, Expect("Expected statement block"))
            select new Node.Rule(expr.Value, block)
        );

    private static readonly RunStep<Node> ParseCall = new(
        "Call", () =>
            from name in ParseNameNode
            from args in ParseEnclosedList(
                Match('('),
                ParseExpression,
                Match(','),
                Match(')')
                )
            select new Node.Call(name, args.ToArray())
        );

    static readonly RunStep<Node> ParseIncrement = new(
        "Increment", () => 
            from left in ParseNameNode
            from op in Match("+=")
            from right in ParseExpression
            select new Node.Incr(left, right)
        );
    
    public static readonly RunStep<Node> ParseTerminal = new(
        "Terminal", () => 
            OneOf(
                ParseCall,
                ParseIncrement,
                ParseExpressionBlock,
                ParseList,
                ParseNameNode, 
                ParseValueNode,
                ParseNoise
            ));

    public static readonly RunStep<Node.StatementBlock> ParseStatementBlock = new(
        "StatementBlock", () => 
            from statements in ParseEnclosedList(
                Match('{'),
                ParseExpression,
                Match(';'),
                Match('}')
            )
            select new Node.StatementBlock(statements)
        );

    static readonly RunStep<Node.ExpressionBlock> ParseExpressionBlock = new(
        "ExpressionBlock", () => 
            from open in Match('(')
            from exp in ParseExpression
            from close in Match(')')
            select new Node.ExpressionBlock(exp)
        );

    private static readonly RunStep<Node.List> ParseList = new(
        "List", () =>
            from els in ParseEnclosedList(
                Match('['),
                OneOf(ParseExpression, Expect("Element expected")),
                Match(','),
                Match(']')
            )
            select new Node.List(els)
        );

    public static readonly RunStep<Node.Ref> ParseNameNode = new(
        "NameNode", () => 
            from name in MatchWord()
            select new Node.Ref(name)
        );

    static readonly RunStep<Node> ParseValueNode = new(
        "ValueNode", () =>
            OneOf<Node>(
                ParseString,
                ParseRegex,
                ParseNumber
            )
        );

    static readonly RunStep<Node.String> ParseString = new(
        "String", () =>
            from open in Match('"')
            from str in Match(c => c != '"')
            from close in Match('"')
            select new Node.String(str)
        );

    static readonly RunStep<Node.Regex> ParseRegex = new(
        "Regex", () =>
            from open in Match('/')
            from pattern in Match(c => c != '/')
            from close in Match('/')
            select new Node.Regex(pattern)
        );

    static readonly RunStep<Node.Number> ParseNumber = new(
        "Number", () =>
            from num in MatchDigits()
            select new Node.Number(int.Parse(num.ReadAll()))
        );

    private static readonly RunStep<Node.Noise> ParseNoise = new(
        "Noise", () =>
            from noise in Match(c => c is not ' ' and not ')' and not '}' and not ']' and not '{')
            select new Node.Noise().WithError("Unrecognised symbol")
        );
}