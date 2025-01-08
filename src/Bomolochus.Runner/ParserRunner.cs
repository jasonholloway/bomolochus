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
                new ParserOps.Continuations(100),
                new ParserOps.CursorInfo(
                    SpaceChars: [' ', '\t', '\n'], 
                    CertaintyThreshold: 1
                    ),
                strength: 100
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

        while (fibres.TryPop(out var f))
        {
            var step = f.Step;
            ParserOps.ContinuationCell? cell = null;
            
            if (step.Strength > f.Cursor.Strength)
            {
                continue;
            }

            if (step is ICacheableStep c)
            {
                if (f.Cursor.Continuations.TryGetValue(c, out cell))
                {
                    cell.AddContinuation((nextCursor, maxStrength, nextStep) =>
                    {
                        //todo these nextSteps will always be Returns and could be typed as such
                        
                        if(maxStrength <= f.Cursor.Strength)
                        {
                            fibres.Push(new Fibre(f.Bindings, nextCursor.Fork(), nextStep));
                        }
                    });
                    
                    goto ContinueLoop;
                }
                
                cell = new ParserOps.ContinuationCell(c);
                f.Cursor.Continuations[c] = cell;
            }

            switch (step)
            {
                case IBindStep s:
                {
                    f.Step = s.Left ?? Step.From(false);
                    f.Bindings = f.Bindings.Push(new Binding.Left(s, cell));
                    fibres.Push(f);

                    continue;
                }
                
                case IReturnStep s:
                {
                    var split = f.Cursor.Move();
                    
                    var parsed = Parsing.From(s.Value, split);

                    f.Cursor.SpaceParsable = true;

                    var maxRealStrength = Strength.Empty;

                    UnwindBinds:

                    if (f.Bindings.IsEmpty)
                    {
                        if (f.Cursor.Text.IsEmpty)
                        {
                            //won't below play hell with completer?
                            return parsed.MapValue(o => (V)o!).Complete();
                        }
                        
                        continue;
                    }

                    f.Bindings = f.Bindings.Pop(out var binding);

                    switch (binding)
                    {
                        case Binding.Left { Bind: var bind } start:
                        {
                            Parsing<Readable>? space = null;

                            if (f.Cursor.SpaceParsable)
                            {
                                var info = bind.RightInfo;
                                
                                var spaceChars = f.Cursor.Info.SpaceChars
                                    .Union(info.Spacing?.SpaceChars ?? [])
                                    .Except(info.Spacing?.NonSpaceChars ?? []);
                                
                                if (f.Cursor.Text.ReadCharsWhile(spaceChars.Contains) > 0)
                                {
                                    var text = f.Cursor.Text.Split();
                                    space = new ParsingText<Readable>(text.Readable, text, true);
                                }

                                f.Cursor.SpaceParsable = false;
                            }

                            var originalStrength = Strength.Empty;

                            if (!bind.Strength.IsEmpty)
                            {
                                originalStrength = f.Cursor.Strength;
                                f.Cursor.Strength = bind.Strength;
                            }

                            var next = bind.Right(s.Value)(f.Cursor);

                            f.Bindings = f.Bindings.Push(
                                new Binding.Right(
                                    start,
                                    originalStrength,
                                    space != null ? Parsing.From(parsed.Val, [space, parsed]) : parsed,
                                    maxRealStrength
                                    )
                                );

                            //todo only need to fork if there are multiple steps...
                            foreach (var nextStep in next.Steps)
                            {
                                fibres.Push(f.Fork(nextStep));
                            }
                            
                            break;
                        }
                        
                        case Binding.Right(
                            { Cell: var cell0, Bind.RightStrength: var strength }, 
                            var originalStrength, 
                            var leftParsed,
                            var leftRealStrength
                            ):
                        {
                            maxRealStrength = Strength.Max(maxRealStrength, Strength.Max(leftRealStrength, strength));
                            
                            cell0?.Emit(f.Cursor.Fork(), maxRealStrength, s);

                            f.Cursor.Strength = originalStrength;
                            
                            parsed = Parsing.From(s.Value, [leftParsed, parsed]);
                            
                            goto UnwindBinds;
                        }
                            
                        default: throw new NotImplementedException();
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
                => $"Left({Bind}, {Cell})";
        }

        public record Right(Left Info, Strength OriginalStrength, Parsing LeftParsed, Strength LeftStrength) : Binding(Info.Bind)
        {
            public override string ToString()
                => $"Right({Info.Bind}, {LeftParsed})";
        }
    }
    
    private class Fibre(
        ImmutableStack<Binding> bindings,
        ParserOps.Cursor cursor,
        IStep step
        )
    {
        public ImmutableStack<Binding> Bindings { get; set; } = bindings;
        public ParserOps.Cursor Cursor { get; set; } = cursor;
        public IStep Step { get; set; } = step;

        public Fibre Fork(IStep? step = null) 
            => new(Bindings, Cursor.Fork(), step ?? Step);

        public override string ToString() => Cursor.ToString();
    }
}