namespace Bomolochus.Example;

using static ParserOps;

public static class ExampleParser
{
    public static readonly RunStep<Node.Rules> ParseRules = new(
        "Rules", () =>
            from rules in ParseDelimitedList(ParseRule, OneOf(Match(';'), Match('\n')))
            select new Node.Rules(rules)
        );
    
    /* there's some contamination afoot
     * equality is duffing up conjunctions below
     * there must be a missed fork somewhere
     *
     *
     * 
     */
    
    public static readonly RunStep<Node> ParseExpression = new(
        "Expression", () => 
            OneOf(
                // ParseExpressionBlock,
                // ParseValue,
                ParseRef,
                // ParseList,
                // ParseEquality,
                ParseConjunction,
                ParseDisjunction,
                ParseNoise
                //,
                // ParseProp
            )
        );
    
    static readonly RunStep<Node> ParseDisjunction = new(
        "Disjunction", () => 
            from left in ParseExpression
            from op in Match('|').WithPrecedence(100)
            from right in ParseExpression
            select new Node.Or([left, right])
        );
    
    static readonly RunStep<Node> ParseConjunction = new(
        "Conjunction", () =>
            from left in ParseExpression
            from op in Match('&').WithPrecedence(90)
            from right in ParseExpression
            select new Node.And([left, right])
            // from els in ParseDelimitedList(ParseExpression, Match('&'))
            // where els.Length > 1
            // select new Node.And(els.ToArray()) 
        );

    static readonly RunStep<Node> ParseEquality = new(
        "Equality", () =>
            from left in ParseExpression
            from op in Match('=')
            from right in ParseExpression
            select new Node.Is([left, right])
        );

    public static readonly RunStep<Node> ParseProp = new(
        "Prop", () =>
            Expand(ParseTerminal,
                left => 
                    from op in Match('.')
                    from right in ParseTerminal
                    select new Node.Prop(left, right)
            ));
    
    static readonly RunStep<Node.Rule> ParseRule = new(
        "Rule", () => 
            from expr in Optional(ParseExpression)
            from block in OneOf(ParseStatementBlock, Expect("Expected statement block"))
            select new Node.Rule(expr.Value, block)
        );

    private static readonly RunStep<Node> ParseCall = new(
        "Call", () =>
            from name in ParseRef
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
            from left in ParseRef
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
                ParseRef, 
                ParseValue,
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

    public static readonly RunStep<Node.Ref> ParseRef = new(
        "NameNode", () => 
            from name in MatchWord()
            select new Node.Ref(name)
        );

    static readonly RunStep<Node> ParseValue = new(
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

    static readonly RunStep<Node> ParseNumber = new(
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