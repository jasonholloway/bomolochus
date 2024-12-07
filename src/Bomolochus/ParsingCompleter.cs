using System.Collections.Immutable;
using Bomolochus.Text;

namespace Bomolochus;

using static Extent;

public static class ParsingCompleter
{
    public static Parsed<N> Complete<N>(this Parsing<N> parsing)
        // where N : Parsable
    {
        var ac = Fold(parsing.Val)(FoldAcc.Empty, parsing);

        if (parsing.Val is Annotatable a
            && a.Extract() is {} addenda)
        {
            ac = ac with
            {
                Addenda = ac.Addenda + addenda
            };
        }

        var (left, centre, right) = DistributeExtents(ac.Clutched);
        
        var folded = new Folded<N>(
            left, centre, right,
            ac.Addenda, 
            parsing.Val, 
            ac.Upstreams.ToArray());
        
        folded.Centre.BackLink(folded);

        if (parsing.Val is Parsable p)
        {
            p.BackLink(folded);
        }
        
        return folded;
    }
    
    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    //todo we're not after root below,
    //but after FIRST NON SPACE
    //in both directions
    //with left and right gutters always being absorbed upwards
    //
    //or is this FIRST PARSABLE?
    //parsables are what gets represented
    //all else is spurious clutter
    //!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!
    
    // though, does this work with unaries?
    // a++
    // evidently has '++'
    // which under the above scheme
    // well no actually, as the full lot above would be covered by Root, albeit with potential Space
    
    /* but in '= 3'
     * the equals does not belong to our node!
     * though the three is the root
     * and the equals is on an intermediate
     * it isn't a node though
     *
     * if we're in an intermediate
     * and we find a subnode in situ
     * then - you'd think we must be in root - but we might not be!
     * 
     * I'm imagining now an intermediate that isn't a root, and yet contains non-root subnodes
     * so we're in a parsing, and our value has its minimal expression in a subnode
     * (which makes us an intermediate)
     * and in this state we find another
     * this would only happen if a node was parsed but thrown away I think - which is possible though odd
     * an intersting edge case        
     *
     * think of eg an empty list as a byproduct of parsing (realistic)
     * this list was jetisoned because of its emptiness
     * do we want it then if it doesn't play a part in the semantic value?
     * not really... an opinionated approach here would be to chuck it
     * if we've not claimed it into the semantic tree, it _must_ be fluff
     * and as fluff, it should float off unowned
     * until it finds its maximum home within a covering node
     * fluff should float, and fluff might be a non-space node
     *
     * that's nicely decided then
     * 
     * so, yes, it is about finding root
     * we have our current value, and we want to find our minimal representation
     */

    static (Extent Left, Extent Centre, Extent Right) DistributeExtents(ImmutableArray<Clutch> clutched)
    {
        var left = Empty;
        var centre = Empty;
        var right = Empty;

        bool foundSubVal = false;
        bool foundRoot = false;

        foreach (var clutch in clutched)
        {
            switch (foundSubVal, foundRoot, clutch)
            {
                case (false, false, Clutch.Simple(var e, _)):
                    left += e;
                    break;
                
                case (true, false, Clutch.Simple(var e, _)):
                    centre += e;
                    break;
                
                case (_, true, Clutch.Simple(var e, _)):
                    right += e;
                    break;
                
                
                case (false, false, Clutch.Triple(var l, var c, var r)):
                    left += l;
                    centre += c + r;
                    foundSubVal = true;
                    break;
                
                case (true, false, Clutch.Triple(var l, var c, var r)):
                    centre += l + c + r;
                    foundSubVal = true;
                    break;
                
                case (_, true, Clutch.Triple(var l, var c, var r)):
                    right += l + c + r;
                    foundSubVal = true;
                    break;
                
                
                case (_, _, Clutch.Root(var inner)):
                    var rootRest = new Stack<Clutch>(inner.Length);
                    var rootFoundNonSpace = false;
                    
                    foreach (var clutch2 in inner)
                    {
                        switch (clutch2)
                        {
                            case Clutch.Simple(var e, true) when !rootFoundNonSpace:
                                left += e;
                                break;
                            
                            case Clutch.Simple(var e, _) s:
                                rootRest.Push(s);
                                rootFoundNonSpace = true;
                                break;
                            
                            case Clutch.Triple(var l, var c, var r) t when !rootFoundNonSpace:
                                left += l;
                                rootRest.Push(t with { Left = Empty });
                                rootFoundNonSpace = true;
                                break;
                            
                            case Clutch.Triple(var l, var c, var r) t:
                                rootRest.Push(t);
                                rootFoundNonSpace = true;
                                break;
                            
                            default:
                                throw new NotImplementedException();
                        }
                    }

                    rootFoundNonSpace = false;

                    var rootRight = Empty;
                    var rootCentre = Empty;
                    
                    foreach (var clutch3 in rootRest)
                    {
                        switch (clutch3)
                        {
                            case Clutch.Simple(var e, true) when !rootFoundNonSpace:
                                rootRight = e + rootRight;
                                break;
                            
                            case Clutch.Simple(var e, false) when !rootFoundNonSpace:
                                rootCentre = e;
                                rootFoundNonSpace = true;
                                break;
                            
                            case Clutch.Triple(var l, var c, var r) when !rootFoundNonSpace:
                                rootCentre = l + c;
                                rootRight = r + rootRight;
                                rootFoundNonSpace = true;
                                break;
                            
                            case Clutch.Simple(var e, _):
                                rootCentre = e + rootCentre;
                                break;
                            
                            case Clutch.Triple(var l, var c, var r):
                                rootCentre = l + c + r + rootCentre;
                                break;
                        }
                    }

                    centre += rootCentre;
                    right += rootRight;
                    
                    foundRoot = true;
                    break;
            }
        }

        return (left, centre, right);
    }

    static Func<FoldAcc, Parsing, FoldAcc> Fold(object rootVal)//   Parsable rootVal)
        => (ac, p) =>
        {
            switch (p)
            {
                //we've got a node, but it's not our current one
                //so we start a spanking new territorial fold
                case Parsing<object> parsing when !ReferenceEquals(parsing.Val, rootVal):
                {
                    var parsed = Complete(parsing);

                    return ac with
                    {
                        Addenda = ac.Addenda with { Certainty = ac.Addenda.Certainty * parsed.Addenda.Certainty },
                        Upstreams = ac.Upstreams.Add(parsed),
                        Clutched = [..ac.Clutched, new Clutch.Triple(parsed.Left, parsed.Centre, parsed.Right)]
                    };
                }
                
                //must be a parsing of our root, but not actual tokens
                //so we fold through upstreams
                case ParsingGroup pg and Parsing<Parsable> pp when ReferenceEquals(pp.Val, rootVal):
                {
                    var inner = pg.Upstreams.Aggregate(FoldAcc.Empty, Fold(rootVal));

                    ac = (ac.FoundRoot, inner.FoundRoot) switch
                    {
                        (false, false) =>
                            //WE ARE ROOT
                            //actually - we could have found something ephemeral here
                            inner with
                            {
                                FoundRoot = true,
                                Clutched = [..ac.Clutched, new Clutch.Root(inner.Clutched)]
                            },
                        (false, true) =>
                            //ROOT IS WITHIN
                            inner with { Clutched = [..ac.Clutched, ..inner.Clutched] },
                        (true, _) =>
                            //WE HAVE ALREADY PASSED ROOT
                            inner with 
                            { 
                                FoundRoot = true, 
                                Clutched = [..ac.Clutched, ..inner.Clutched] 
                            },
                    };

                    return ac with
                    {
                        Addenda = ac.Addenda + inner.Addenda + pg.Addenda,
                    };
                }
                
                case ParsingGroup pg:
                    ac = pg.Upstreams.Aggregate(ac, Fold(rootVal));

                    return ac with
                    {
                        Addenda = ac.Addenda + pg.Addenda,
                    };
                
                case ParsingText { Text: var text, Addenda: var addenda, IsSpace: var isSpace }:
                {
                    var extent = From(text.Readable);

                    return ac with
                    {
                        Addenda = ac.Addenda + addenda,
                        Clutched = [..ac.Clutched, new Clutch.Simple(extent, isSpace)]
                    };
                }

                default:
                    throw new NotImplementedException();
            }
        };

    /* the above is flattening the parse tree into a series of Clutches 
     * but also aggregating various bits attached into the parse tree
     * 
     * currently, we have an intermediate grouping of bits
     * we should just be able to flatten them except for the fact
     * that some bits might have left/centre/rights
     * though if they did, they would only be ephemeral anyway
     * as the root value is not here!
     */
    
    record FoldAcc(bool FoundRoot, ImmutableArray<Clutch> Clutched, Addenda Addenda, ImmutableArray<Parsed> Upstreams)
    {
        public static readonly FoldAcc Empty = new(false, [], Addenda.Empty, []);
    }

    record Clutch
    {
        public record Simple(Extent Extent, bool IsSpace) : Clutch;
        public record Triple(Extent Left, Extent Centre, Extent Right) : Clutch;
        public record Root(ImmutableArray<Clutch> Clutched) : Clutch;
    }
    
    class Folded<N>(Extent left, Extent centre, Extent right, Addenda addenda, N value, Parsed[] upstreams) : Parsed<N> 
        // where N: Parsable
    {
        public Extent Left => left;
        public Extent Centre => centre;
        public Extent Right => right;
        
        public Addenda Addenda => addenda;
        public N Value => value;
        public IEnumerable<Parsed> Upstreams => upstreams;

        public override string ToString()
            => $"({left}, {centre}, {right})";
    }
}