namespace Bomolochus.Example;

using static ParserOps;

public static class ExampleParser
{
    /* TODO the current space-parsing does not cope with nested parsers with different space expectations
     * ie ';' | '\n', since it is at the top level a OneOf
     * will greedily consume '\n' before it delegates in to the speciaised matchers
     * the space chars of child parsers should be take into account by their combinators
     * ie OneOf should pre-read a lowest-common denominator list of spaces
     * and allow sub-parsers to whittle down beyond that
     */
    
    /* Testing of the above:
     * we need a case in which a OneOf covers different matchings
     */



    // static ExampleParser()
    // {
    //     Parser<Node> parseExpression;
    //     
    //     var parseWord = 
    //         from word in Match(c => c is >= 'A' and <= 'z')
    //         select new Node.String(word);
    //
    //     var parseNum =
    //         from num in Match(c => c is >= '0' and <= '9')
    //         select new Node.Number(int.Parse(num.ReadAll()));
    //
    //     var parseAnd =
    //         from exps in ParseDelimitedList(parseExpression, Match('&'))
    //         select new Node.And(exps.ToArray());
    //
    //     parseExpression = OneOf<Node>(parseAnd, parseWord, parseNum);
    // }

    public static readonly ParseStep<Node.Rules> ParseRules = new(
        "Rules", () =>
            from rules in ParseDelimitedList(ParseRule, OneOf(Match(';'), Match('\n')))
            select new Node.Rules(rules)
        );

    static readonly ParseStep<Node> ParseDisjunction = new(
        "Disjunction", () => 
            from els in ParseDelimitedList(ParseConjunction, Match('|'))
            select els.Length > 1 
                ? new Node.Or(els.ToArray()) 
                : els.Single()
        );
    
    public static readonly ParseStep<Node> ParseExpression = new(
        "Expression", () => 
            ParseDisjunction
        );
    
    static readonly ParseStep<Node.Rule> ParseRule = new(
        "Rule", () => 
            from expr in Optional(ParseExpression)
            from block in OneOf(ParseStatementBlock, Expect("Expected statement block"))
            select new Node.Rule(expr.Value, block)
        );
    
    // static readonly Parser<Node.Rule> ParseRule = new(() => 
    //     OneOf(
    //         from expr in ParseExpression
    //         from block in OneOf(ParseStatementBlock, Expect("Expected statement block"))
    //         select new Node.Rule(expr, block),
    //         
    //         from block in OneOf(ParseStatementBlock, Expect("Expected statement block"))
    //         select new Node.Rule(null, block)
    //         )
    // );

    static readonly ParseStep<Node> ParseConjunction = new(
        "Conjunction", () =>
            from els in ParseDelimitedList(ParseEquality, Match('&'))
            select els.Length > 1 
                ? new Node.And(els.ToArray()) 
                : els.Single()
        );

    static readonly ParseStep<Node> ParseEquality = new(
        "Equality", () =>
            from els in ParseDelimitedList(
                OneOf(ParseProp, Expect("Expression expected")), 
                Match('=')
                )
            select els.Length > 1 
                ? new Node.Is(els.ToArray()) 
                : els.Single()
        );

    public static readonly ParseStep<Node> ParseProp = new(
        "Prop", () =>
            Expand(ParseTerminal,
                left => 
                    from op in Match('.')
                    from right in ParseTerminal
                    select new Node.Prop(left, right)
            ));

    private static readonly ParseStep<Node> ParseCall = new(
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

    static readonly ParseStep<Node> ParseIncrement = new(
        "Increment", () => 
            from left in ParseNameNode
            from op in Match("+=")
            from right in ParseExpression
            select new Node.Incr(left, right)
        );
    
    public static readonly ParseStep<Node> ParseTerminal = new(
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

    public static readonly ParseStep<Node.StatementBlock> ParseStatementBlock = new(
        "StatementBlock", () => 
            from statements in ParseEnclosedList(
                Match('{'),
                ParseExpression,
                Match(';'),
                Match('}')
            )
            select new Node.StatementBlock(statements)
        );

    static readonly ParseStep<Node.ExpressionBlock> ParseExpressionBlock = new(
        "ExpressionBlock", () => 
            from open in Match('(')
            from exp in ParseExpression
            from close in Match(')')
            select new Node.ExpressionBlock(exp)
        );

    private static readonly ParseStep<Node.List> ParseList = new(
        "List", () =>
            from els in ParseEnclosedList(
                Match('['),
                OneOf(ParseExpression, Expect("Element expected")),
                Match(','),
                Match(']')
            )
            select new Node.List(els)
        );

    static readonly ParseStep<Node.Ref> ParseNameNode = new(
        "NameNode", () => 
            from name in MatchWord()
            select new Node.Ref(name)
        );

    static readonly ParseStep<Node> ParseValueNode = new(
        "ValueNode", () =>
            OneOf<Node>(
                ParseString,
                ParseRegex,
                ParseNumber
            )
        );

    static readonly ParseStep<Node.String> ParseString = new(
        "String", () =>
            from open in Match('"')
            from str in Match(c => c != '"')
            from close in Match('"')
            select new Node.String(str)
        );

    static readonly ParseStep<Node.Regex> ParseRegex = new(
        "Regex", () =>
            from open in Match('/')
            from pattern in Match(c => c != '/')
            from close in Match('/')
            select new Node.Regex(pattern)
        );

    static readonly ParseStep<Node.Number> ParseNumber = new(
        "Number", () =>
            from num in MatchDigits()
            select new Node.Number(int.Parse(num.ReadAll()))
        );

    private static readonly ParseStep<Node.Noise> ParseNoise = new(
        "Noise", () =>
            from noise in Match(c => c is not ' ' and not ')' and not '}' and not ']' and not '{')
            select new Node.Noise().WithError("Unrecognised symbol")
        );
}