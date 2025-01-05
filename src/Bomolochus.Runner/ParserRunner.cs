using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus.Runner;

public static class ParserRunner
{
    public static Parsed<V> Parse<V>(this IStep<V> parser, Readable text)
        where V : Parsable
        => Parse(parser,
            new ParserOps.Cursor(
                TextSplitter.Create(text), 
                new ParserOps.Continuations(100), //todo strength should be shared better
                new ParserOps.CursorInfo(
                    SpaceChars: [' ', '\t', '\n'], 
                    CertaintyThreshold: 1
                    )
            ));

    //todo below should slough off frames given progress
    //and how would we know which ones we can get rid of?
    //answer: threads
    
    //sibling fibres could be culled as sufficient progress is made on one leg
    
    public static Parsed<V> Parse<V>(this IStep<V> parser, ParserOps.Cursor cursor)
        where V : Parsable
    {
        var fibres = new Stack<Fibre>([
            new Fibre([], cursor, parser)
        ]);
        
        ContinueLoop:

        while (fibres.TryPop(out var x))
        {
            var step = x.Step;
            
            if (x is { Cursor.Strength: var strength } 
                && step.Info.Strength is int requiredStrength)
            {
                if (strength < requiredStrength)
                {
                    continue;
                }
            }
                    
            if (step is ICacheableStep c)
            {
                if (x.Cursor.Continuations.TryGetValue(c, out var cell))
                {
                    cell.AddContinuation(next =>
                    {
                        x = x with
                        {
                            Cursor = next.Context //will be forked below
                        };
                        
                        //todo also need to filter out results that are too strong... 
                        
                        foreach (var s in next.Steps)
                        {
                            fibres.Push(x.Fork(s));
                        }
                    });
                    
                    goto ContinueLoop;
                }
            }
                    
            switch (step)
            {
                case IBindStep s:
                {
                    ParserOps.ContinuationCell? cell = null;

                    if (s is ICacheableStep cs)
                    {
                        cell = new ParserOps.ContinuationCell(cs);
                        x.Cursor.Continuations[cs] = cell;
                    }
                    
                    var left = s.Left ?? Step.From(false);

                    x = x with
                    {
                        Bindings = x.Bindings.Push(new Binding.Left(s, cell))
                    };

                    fibres.Push(x with { Step = left }); //default step fills in when left leg is empty for convenience

                    continue;
                }
                
                case IReturnStep s:
                {
                    var split = x.Cursor.Move();
                    
                    var parsed = Parsing.From(s.Value, split);

                    x.Cursor.SpaceParsable = true;

                    UnwindBinds:

                    if (x.Bindings.IsEmpty)
                    {
                        if (x.Cursor.Text.IsEmpty)
                        {
                            //won't below play hell with completer?
                            return parsed.MapValue(o => (V)o!).Complete();
                        }
                        
                        continue;
                    }

                    x = x with { Bindings = x.Bindings.Pop(out var binding) };

                    switch (binding)
                    {
                        case Binding.Left { Bind: var bind } start:
                        {
                            Parsing<Readable>? space = null;

                            if (x.Cursor.SpaceParsable)
                            {
                                var info = bind.RightInfo;
                                
                                var spaceChars = x.Cursor.Info.SpaceChars
                                    .Union(info.Spacing?.SpaceChars ?? [])
                                    .Except(info.Spacing?.NonSpaceChars ?? []);
                                
                                if (x.Cursor.Text.ReadCharsWhile(spaceChars.Contains) > 0)
                                {
                                    var text = x.Cursor.Text.Split();
                                    space = new ParsingText<Readable>(text.Readable, text, true);
                                }

                                x.Cursor.SpaceParsable = false;
                            }
                            
                            //todo strength to be applied and reverted

                            var next = bind.Right(s.Value)(x.Cursor);

                            var x2 = x with
                            {
                                Cursor = next.Context,
                                Bindings = x.Bindings.Push(
                                    new Binding.Right(
                                        start, 
                                        space != null ? Parsing.From(parsed.Val, [space, parsed]) : parsed)
                                    )
                            };

                            foreach (var nextStep in next.Steps)
                            {
                                fibres.Push(x2.Fork(nextStep));
                            }
                            
                            break;
                        }
                        
                        case Binding.Right({ Cell: var cell }, var leftParsed):
                        {
                            cell?.Emit(Next.From(x.Cursor.Fork(), [s]));
                            
                            parsed = Parsing.From(s.Value, [leftParsed, parsed]);
                            
                            goto UnwindBinds;
                        }
                    }
                    
                    continue;
                }
                
                default: throw new NotImplementedException();
            }
        }
        
        return default!;
    }

    abstract record Binding(IBindStep Bind)
    {
        public record Left(IBindStep Bind, ParserOps.ContinuationCell? Cell = null, int? OrigPrecedence = null) : Binding(Bind)
        {
            public override string ToString()
                => $"Started({Bind}, {Cell})";
        }

        public record Right(Left Info, Parsing ParsedLeft) : Binding(Info.Bind)
        {
            public override string ToString()
                => $"Completing({Info.Bind}, {ParsedLeft})";
        }
    }
    
    private record Fibre(
        ImmutableStack<Binding> Bindings, 
        ParserOps.Cursor Cursor,
        IStep Step
        )
    {
        public Fibre Fork(IStep step)
            => this with { Cursor = Cursor.Fork(), Step = step };

        public override string ToString() => Cursor.ToString();
    }

}