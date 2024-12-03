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

    public static readonly RunStep<Node.Rules> RunRules = new(
        "Rules", () =>
            from rules in ParseDelimitedList(RunRule, OneOf(Match(';'), Match('\n')))
            select new Node.Rules(rules)
        );

    static readonly RunStep<Node> RunDisjunction = new(
        "Disjunction", () => 
            from els in ParseDelimitedList(RunConjunction, Match('|'))
            select els.Length > 1 
                ? new Node.Or(els.ToArray()) 
                : els.Single()
        );
    
    public static readonly RunStep<Node> RunExpression = new(
        "Expression", () => 
            RunDisjunction
        );
    
    static readonly RunStep<Node.Rule> RunRule = new(
        "Rule", () => 
            from expr in Optional(RunExpression)
            from block in OneOf(RunStatementBlock, Expect("Expected statement block"))
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

    static readonly RunStep<Node> RunConjunction = new(
        "Conjunction", () =>
            from els in ParseDelimitedList(RunEquality, Match('&'))
            select els.Length > 1 
                ? new Node.And(els.ToArray()) 
                : els.Single()
        );

    static readonly RunStep<Node> RunEquality = new(
        "Equality", () =>
            from els in ParseDelimitedList(
                OneOf(RunProp, Expect("Expression expected")), 
                Match('=')
                )
            select els.Length > 1 
                ? new Node.Is(els.ToArray()) 
                : els.Single()
        );

    public static readonly RunStep<Node> RunProp = new(
        "Prop", () =>
            Expand(RunTerminal,
                left => 
                    from op in Match('.')
                    from right in RunTerminal
                    select new Node.Prop(left, right)
            ));

    private static readonly RunStep<Node> RunCall = new(
        "Call", () =>
            from name in RunNameNode
            from args in ParseEnclosedList(
                Match('('),
                RunExpression,
                Match(','),
                Match(')')
                )
            select new Node.Call(name, args.ToArray())
        );

    static readonly RunStep<Node> RunIncrement = new(
        "Increment", () => 
            from left in RunNameNode
            from op in Match("+=")
            from right in RunExpression
            select new Node.Incr(left, right)
        );
    
    public static readonly RunStep<Node> RunTerminal = new(
        "Terminal", () => 
            OneOf(
                RunCall,
                RunIncrement,
                RunExpressionBlock,
                RunList,
                RunNameNode, 
                RunValueNode,
                RunNoise
            ));

    public static readonly RunStep<Node.StatementBlock> RunStatementBlock = new(
        "StatementBlock", () => 
            from statements in ParseEnclosedList(
                Match('{'),
                RunExpression,
                Match(';'),
                Match('}')
            )
            select new Node.StatementBlock(statements)
        );

    static readonly RunStep<Node.ExpressionBlock> RunExpressionBlock = new(
        "ExpressionBlock", () => 
            from open in Match('(')
            from exp in RunExpression
            from close in Match(')')
            select new Node.ExpressionBlock(exp)
        );

    private static readonly RunStep<Node.List> RunList = new(
        "List", () =>
            from els in ParseEnclosedList(
                Match('['),
                OneOf(RunExpression, Expect("Element expected")),
                Match(','),
                Match(']')
            )
            select new Node.List(els)
        );

    static readonly RunStep<Node.Ref> RunNameNode = new(
        "NameNode", () => 
            from name in MatchWord()
            select new Node.Ref(name)
        );

    static readonly RunStep<Node> RunValueNode = new(
        "ValueNode", () =>
            OneOf<Node>(
                RunString,
                RunRegex,
                RunNumber
            )
        );

    static readonly RunStep<Node.String> RunString = new(
        "String", () =>
            from open in Match('"')
            from str in Match(c => c != '"')
            from close in Match('"')
            select new Node.String(str)
        );

    static readonly RunStep<Node.Regex> RunRegex = new(
        "Regex", () =>
            from open in Match('/')
            from pattern in Match(c => c != '/')
            from close in Match('/')
            select new Node.Regex(pattern)
        );

    static readonly RunStep<Node.Number> RunNumber = new(
        "Number", () =>
            from num in MatchDigits()
            select new Node.Number(int.Parse(num.ReadAll()))
        );

    private static readonly RunStep<Node.Noise> RunNoise = new(
        "Noise", () =>
            from noise in Match(c => c is not ' ' and not ')' and not '}' and not ']' and not '{')
            select new Node.Noise().WithError("Unrecognised symbol")
        );
}