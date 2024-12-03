using System.Reactive.Linq;
using Bomolochus.Example;
using Bomolochus.Runner;
using Bomolochus.Text;

namespace Bomolochus.LanguageServer;

public static class Diagnoser
{
    public static IObservable<Document> Diagnose(Uri uri, IObservable<string> texts) =>
        texts
            .Select(text => ExampleParser.RunRules.Parse(text))   
            .Select((parsed, version) =>
            {
                var doc = new ParsedDoc(Extent.Combine(parsed.Left, Extent.Combine(parsed.Centre, parsed.Right)), parsed);
                
                return new Document(
                    uri,
                    version,
                    doc,
                    parsed?
                        .EnumerateAll()
                        .SelectMany(p => p.Addenda.Notes.Select(n => (Parsed: p, Note: n)))
                        .Select(tup => new Diagnostic(doc.Extent.GetBoundsOf(tup.Parsed.Centre).ToRange(), tup.Note))
                        .ToArray() ?? []
                );
            });
}

/* TODO
 * we need to have the concept of a Cursor
 * as an empty but substantial extent
 * it would still exist in the tree as a floating point within the document
 */



