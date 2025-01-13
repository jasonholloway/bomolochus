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

            if (step != null)
            {
                Console.WriteLine($"{f} {string.Join("", Enumerable.Repeat(' ', f.Bindings.Count()))}{f.Step}");

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
                        
                        Console.WriteLine($"{f} {string.Join("", Enumerable.Repeat(' ', f.Bindings.Count()))}WAIT");
                        goto ContinueLoop;
                    }
                    
                    cell = new ParserOps.ContinuationCell(c);
                    f.Cursor.Continuations[c] = cell;
                }
            }

            switch (step ?? Step.From(false))
            {
                case Step.MatchChar(var @char):
                {
                    if (f.Cursor.Text.TryReadChar(@char, out var claimed))
                    {
                        f.Step = Step.From(claimed);
                        fibres.Push(f);
                    }
                    
                    continue;
                }
                
                case Step.MatchArbitrary(_, var fn):
                {
                    if (f.Cursor.Text.ReadCharsWhile(fn) > 0)
                    {
                        f.Step = Step.From(f.Cursor.Text.Staged);
                        fibres.Push(f);
                    }
                    
                    continue;
                }
                
                case Step.Barrier s:
                {
                    if (s.IsEnclave || f.Cursor.Strength >= s.Strength)
                    {
                        f.Step = s.Inner;
                        f.Bindings = f.Bindings.Push(new Binding.Strength(s, f.Cursor.Strength));
                        f.Cursor.Strength = s.Strength;
                        fibres.Push(f);
                    }

                    continue;
                }
                
                case ISpacingStep s:
                {
                    var oldSpaceChars = f.Cursor.Info.SpaceChars;
                    
                    f.Cursor.Info = f.Cursor.Info with { 
                        SpaceChars = f.Cursor.Info.SpaceChars
                            .Union(s.Spacing.SpaceChars ?? [])
                            .Except(s.Spacing.NonSpaceChars ?? []) 
                    };
                    
                    //todo: add special binding as well, as spacing is to be reverted
                    //the point of separating out strength is because then we can move the barrier around
                                
                    throw new NotImplementedException();
                }
                
                case IBindStep s:
                {
                    f.Step = s.Left;
                    f.Bindings = f.Bindings.Push(new Binding.Left(s, cell));
                    fibres.Push(f);

                    continue;
                }
                
                case Step.Return(var value) s:
                {
                    var split = f.Cursor.Move();
                    
                    var parsed = Parsing.From(value, split);

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
                                
                                if (f.Cursor.Text.ReadCharsWhile(f.Cursor.Info.SpaceChars.Contains) > 0)
                                {
                                    var text = f.Cursor.Text.Split();
                                    space = new ParsingText<Readable>(text.Readable, text, true);
                                }

                                f.Cursor.SpaceParsable = false;
                            }

                            var next = bind.Right(value)(f.Cursor);

                            f.Bindings = f.Bindings.Push(
                                new Binding.Right(
                                    start,
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
                            { Cell: var cell0, Bind: var bind }, 
                            var leftParsed,
                            var leftStrength
                            ):
                        {
                            Console.WriteLine($"{f} {string.Join("", Enumerable.Repeat(' ', f.Bindings.Count()))}/");
                            
                            maxRealStrength = Strength.Max(maxRealStrength, leftStrength);

                            if (cell0 != null)
                            {
                                Console.WriteLine($"{f} {string.Join("", Enumerable.Repeat(' ', f.Bindings.Count()))}EMIT {cell0.Origin}");
                                cell0?.Emit(f.Cursor.Fork(), maxRealStrength, s);
                            }

                            parsed = Parsing.From(value, [leftParsed, parsed]);
                            
                            goto UnwindBinds;
                        }

                        case Binding.Strength(var strengthStep, var origStrength):
                        {
                            if (strengthStep.IsEnclave)
                            {
                                maxRealStrength = Strength.Empty;
                            }
                            else
                            {
                                maxRealStrength = Strength.Max(maxRealStrength, strengthStep.Strength);
                            }
                            
                            f.Cursor.Strength = origStrength;
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

    abstract record Binding
    {
        public record Left(IBindStep Bind, ParserOps.ContinuationCell? Cell = null) : Binding
        {
            public override string ToString()
                => $"Left({Bind}, {Cell})";
        }

        public record Right(Left Info, Parsing LeftParsed, Bomolochus.Strength LeftStrength) : Binding
        {
            public override string ToString()
                => $"Right({Info.Bind}, {LeftParsed})";
        }

        public record Strength(Step.Barrier Step, Bomolochus.Strength OriginalStrength) : Binding;
    }
    
    private class Fibre(
        ImmutableStack<Binding> bindings,
        ParserOps.Cursor cursor,
        IStep? step
        )
    {
        private static int _nextIndex = 0;
        
        private readonly int _index = _nextIndex++;
        private int _stepIndex = 0;
        
        public ImmutableStack<Binding> Bindings { get; set; } = bindings;
        public ParserOps.Cursor Cursor { get; set; } = cursor;

        public IStep? Step
        {
            get => step;
            set
            {
                step = value;
                _stepIndex++;
            }
        }

        public Fibre Fork(IStep? step = null) 
            => new(Bindings, Cursor.Fork(), step ?? Step);

        public override string ToString() => $"{_index:X2}_{_stepIndex:X2} {Cursor}";
    }
}