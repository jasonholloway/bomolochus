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
    
    
    
    
    
    public static readonly _Parser<Node.Rules> ParseRules = new(() =>
        from rules in ParseDelimitedList(ParseRule, OneOf(Match(';'), Match('\n')))
        select new Node.Rules(rules)
    );

    static readonly _Parser<Node> ParseDisjunction = new(() => 
        from els in ParseDelimitedList(ParseConjunction, Match('|'))
        select els.Length > 1 
            ? new Node.Or(els.ToArray()) 
            : els.Single()
    );
    
    public static readonly _Parser<Node> ParseExpression = new(() => 
        ParseDisjunction
    );
    
    static readonly _Parser<Node.Rule> ParseRule = new(() => 
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

    static readonly _Parser<Node> ParseConjunction = new(() =>
        from els in ParseDelimitedList(ParseEquality, Match('&'))
        select els.Length > 1 
            ? new Node.And(els.ToArray()) 
            : els.Single()
    );

    static readonly _Parser<Node> ParseEquality = new(() =>
        from els in ParseDelimitedList(
            OneOf(ParseProp, Expect("Expression expected")), 
            Match('=')
            )
        select els.Length > 1 
            ? new Node.Is(els.ToArray()) 
            : els.Single()
    );

    static readonly _Parser<Node> ParseProp = new(() =>
        Expand(ParseTerminal,
            left => 
                from op in Match('.')
                from right in ParseTerminal
                select new Node.Prop(left, right)
        ));

    private static readonly _Parser<Node> ParseCall = new(() =>
        from name in ParseNameNode
        from args in ParseEnclosedList(
            Match('('),
            ParseExpression,
            Match(','),
            Match(')')
            )
        select new Node.Call(name, args.ToArray())
    );

    static readonly _Parser<Node> ParseIncrement = new(() => 
        from left in ParseNameNode
        from op in Match("+=")
        from right in ParseExpression
        select new Node.Incr(left, right)
    );
    
    static readonly _Parser<Node> ParseTerminal = new(() => 
        OneOf(
            ParseCall,
            ParseIncrement,
            ParseExpressionBlock,
            ParseList,
            ParseNameNode, 
            ParseValueNode,
            ParseNoise
            ));

    public static readonly _Parser<Node.StatementBlock> ParseStatementBlock = new(() => 
        from statements in ParseEnclosedList(
            Match('{'),
            ParseExpression,
            Match(';'),
            Match('}')
        )
        select new Node.StatementBlock(statements)
    );

    static readonly _Parser<Node.ExpressionBlock> ParseExpressionBlock = new(() => 
        from open in Match('(')
        from exp in ParseExpression
        from close in Match(')')
        select new Node.ExpressionBlock(exp)
    );

    private static readonly _Parser<Node.List> ParseList = new(() =>
        from els in ParseEnclosedList(
            Match('['),
            OneOf(ParseExpression, Expect("Element expected")),
            Match(','),
            Match(']')
        )
        select new Node.List(els)
    );

    static readonly _Parser<Node.Ref> ParseNameNode = new(() => 
        from name in MatchWord()
        select new Node.Ref(name)
    );

    static readonly _Parser<Node> ParseValueNode = new(() =>
        OneOf<Node>(
            ParseString,
            ParseRegex,
            ParseNumber
        ));

    static readonly _Parser<Node.String> ParseString = new(() =>
        from open in Match('"')
        from str in Match(c => c != '"')
        from close in Match('"')
        select new Node.String(str)
    );

    static readonly _Parser<Node.Regex> ParseRegex = new(() =>
        from open in Match('/')
        from pattern in Match(c => c != '/')
        from close in Match('/')
        select new Node.Regex(pattern)
    );

    static readonly _Parser<Node.Number> ParseNumber = new(() =>
        from num in MatchDigits()
        select new Node.Number(int.Parse(num.ReadAll()))
    );

    private static readonly _Parser<Node.Noise> ParseNoise = new(() =>
        from noise in Match(c => c is not ' ' and not ')' and not '}' and not ']' and not '{')
        select new Node.Noise().WithError("Unrecognised symbol")
    );
}