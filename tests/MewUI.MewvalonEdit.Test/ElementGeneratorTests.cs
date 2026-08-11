using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class ElementGeneratorTests
{
    /// <summary>Replaces every "@" with a marker, exercising the scan protocol directly.</summary>
    private sealed class MarkerGenerator(string replacement) : VisualLineElementGenerator
    {
        public List<int> AskedFrom { get; } = [];

        public override int GetFirstInterestedOffset(int startOffset)
        {
            AskedFrom.Add(startOffset);
            var line = CurrentContext!.CurrentDocumentLine;
            string text = CurrentContext.Document.GetText(line.Offset, line.Length);
            int index = text.IndexOf('@', Math.Max(0, startOffset - line.Offset));
            return index < 0 ? -1 : line.Offset + index;
        }

        public override VisualLineElement? ConstructElement(int offset)
            => new TextReplacementElement(replacement, 1, CurrentContext!.DefaultStyle);
    }

    private static List<InlineRun> Generate(TextEditor editor)
    {
        var runs = new List<InlineRun>();
        var context = new TextElementScanContext(editor.Document.CoreDocument, 0);
        foreach (var generator in editor.TextArea.TextView.Extensions.ElementGenerators)
        {
            for (int offset = 0; offset < editor.Text.Length;)
            {
                int interested = generator.GetFirstInterestedOffset(in context, offset);
                if (interested < offset) break;
                var element = generator.ConstructElement(in context, interested);
                if (element is not { } value) { offset = interested + 1; continue; }
                // No projection here, so the element occupies its document range; the object covers
                // only the columns it paints, as the layout clamps it.
                int length = Math.Min(value.DocumentLength, value.VisualLength);
                if (value.Object is not null && length > 0)
                {
                    runs.Add(new InlineRun(interested, length, value.Object));
                }
                offset = interested + Math.Max(1, value.DocumentLength);
            }
        }
        return runs;
    }

    private static List<TextPaintSpan> Classify(TextEditor editor)
    {
        var spans = new List<TextPaintSpan>();
        var context = new TextClassificationContext(
            new LogicalTextLine(0, 0, editor.Text.Length, editor.Text.Length),
            editor.Text.AsMemory(),
            IdentityTextOffsetMap.Instance);
        foreach (var classifier in editor.TextArea.TextView.Extensions.Classifiers)
        {
            classifier.Classify(in context, spans);
        }
        return spans;
    }

    [TestMethod]
    public void GeneratedElementsBecomeInlineRuns()
    {
        var editor = new TextEditor { Text = "a@b@c" };
        editor.TextArea.TextView.ElementGenerators.Add(new MarkerGenerator("<at>"));

        var runs = Generate(editor);

        Assert.HasCount(2, runs);
        Assert.AreEqual(1, runs[0].Position);
        Assert.AreEqual(1, runs[0].Length);
        Assert.AreEqual(3, runs[1].Position);
    }

    [TestMethod]
    public void DecliningAGeneratorStillAdvancesTheScan()
    {
        var editor = new TextEditor { Text = "@@@" };
        var generator = new DecliningGenerator();
        editor.TextArea.TextView.ElementGenerators.Add(generator);

        var runs = Generate(editor);

        Assert.IsEmpty(runs);
        Assert.IsLessThanOrEqualTo(4, generator.AskedFrom.Count, "A declined offset must not restart the scan.");
    }

    private sealed class DecliningGenerator : VisualLineElementGenerator
    {
        public List<int> AskedFrom { get; } = [];

        public override int GetFirstInterestedOffset(int startOffset)
        {
            AskedFrom.Add(startOffset);
            var line = CurrentContext!.CurrentDocumentLine;
            return startOffset < line.Offset + line.Length ? startOffset : -1;
        }

        public override VisualLineElement? ConstructElement(int offset) => null;
    }

    [TestMethod]
    public void LinkGeneratorDecoratesUrlsInsteadOfReplacingThem()
    {
        var editor = new TextEditor { Text = "see https://example.com/docs now" };
        editor.TextArea.TextView.ElementGenerators.Add(new LinkElementGenerator());

        var runs = Generate(editor);
        var spans = Classify(editor);

        // An inline run is one indivisible cluster. A link that became one would lose every caret
        // position inside it, so the underline has to arrive as a paint span over the real text.
        Assert.IsEmpty(runs);
        var span = spans.Single();
        Assert.AreEqual(4, span.Range.Start);
        Assert.AreEqual("https://example.com/docs".Length, span.Range.Length);
        Assert.AreEqual(TextDecoration.Underline, span.Decoration);
    }

    [TestMethod]
    public void GeneratorRegistrationInvalidatesTheView()
    {
        var editor = new TextEditor { Text = "x" };
        long before = editor.TextArea.TextView.Extensions.Revision;

        editor.TextArea.TextView.ElementGenerators.Add(new LinkElementGenerator());

        Assert.IsGreaterThan(before, editor.TextArea.TextView.Extensions.Revision);
    }

    [TestMethod]
    public void GenerationContextIsClearedAfterTheLine()
    {
        var editor = new TextEditor { Text = "a@b" };
        var generator = new MarkerGenerator("!");
        editor.TextArea.TextView.ElementGenerators.Add(generator);

        Generate(editor);

        Assert.ThrowsExactly<NullReferenceException>(() => generator.GetFirstInterestedOffset(0),
            "CurrentContext must not outlive generation.");
    }
}
