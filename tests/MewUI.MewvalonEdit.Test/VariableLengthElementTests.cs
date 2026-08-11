using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class VariableLengthElementTests
{
    /// <summary>Replaces every "TODO" with a single-character marker, shrinking the visual line.</summary>
    private sealed class TodoGenerator : VisualLineElementGenerator
    {
        public override int GetFirstInterestedOffset(int startOffset)
        {
            var line = CurrentContext!.CurrentDocumentLine;
            string text = CurrentContext.Document.GetText(line.Offset, line.Length);
            int index = text.IndexOf("TODO", Math.Max(0, startOffset - line.Offset), StringComparison.Ordinal);
            return index < 0 ? -1 : line.Offset + index;
        }

        public override VisualLineElement? ConstructElement(int offset)
            => new TextReplacementElement("…", "TODO".Length, CurrentContext!.DefaultStyle);
    }

    private static (ProjectedText Projected, List<InlineRun> Runs) RunPipeline(TextEditor editor)
    {
        var pipeline = editor.TextArea.TextView.Extensions;
        var logical = new LogicalTextLine(0, 0, editor.Text.Length, editor.Text.Length);
        var projected = new ProjectedText(editor.Text.AsMemory(), IdentityTextOffsetMap.Instance);
        ITextOffsetMap map = IdentityTextOffsetMap.Instance;
        foreach (var projection in pipeline.Projections)
        {
            var next = projection.Project(new TextProjectionContext(logical, projected.Text));
            map = next.OffsetMap is IdentityTextOffsetMap ? map : next.OffsetMap;
            projected = new ProjectedText(next.Text, map);
        }

        var runs = new List<InlineRun>();
        var context = new TextElementScanContext(editor.Document.CoreDocument, logical.Offset);
        foreach (var generator in pipeline.ElementGenerators)
        {
            for (int offset = logical.Offset; offset < logical.Offset + logical.Length;)
            {
                int interested = generator.GetFirstInterestedOffset(in context, offset);
                if (interested < offset)
                {
                    break;
                }
                if (generator.ConstructElement(in context, interested) is not { } element)
                {
                    offset = interested + 1;
                    continue;
                }
                if (element.Object is not null && element.VisualLength > 0)
                {
                    runs.Add(new InlineRun(
                        projected.OffsetMap.MapFromSource(interested - logical.Offset),
                        element.VisualLength,
                        element.Object));
                }
                offset = interested + Math.Max(1, element.DocumentLength);
            }
        }
        return (projected, runs);
    }

    [TestMethod]
    public void ShrinkingElementProjectsItsVisualText()
    {
        var editor = new TextEditor { Text = "a TODO b" };
        editor.TextArea.TextView.ElementGenerators.Add(new TodoGenerator());

        (var projected, var runs) = RunPipeline(editor);

        Assert.AreEqual("a … b", projected.Text.ToString());
        var run = runs.Single();
        Assert.AreEqual(2, run.Position);
        Assert.AreEqual(1, run.Length);
    }

    [TestMethod]
    public void OffsetsAcrossAShrunkElementMapBothWays()
    {
        var editor = new TextEditor { Text = "a TODO b" };
        editor.TextArea.TextView.ElementGenerators.Add(new TodoGenerator());

        (var projected, _) = RunPipeline(editor);
        var map = projected.OffsetMap;

        Assert.AreEqual(0, map.MapFromSource(0));
        Assert.AreEqual(2, map.MapFromSource(2), "The element start keeps its position.");
        Assert.AreEqual(2, map.MapFromSource(4), "An offset inside the element collapses to its start.");
        Assert.AreEqual(3, map.MapFromSource(6), "The first offset after the element follows the marker.");
        Assert.AreEqual(8, map.MapToSource(5), "The line end maps back across the shrunk range.");
        Assert.AreEqual(2, map.MapToSource(2));
    }

    /// <summary>
    /// VisualLine is what ported code converts coordinates through, so its two directions have to
    /// agree with the projection a shrinking element produced.
    /// </summary>
    [TestMethod]
    public void VisualLineConvertsColumnsAcrossAShrunkElement()
    {
        const double WIDTH = 400;
        const double HEIGHT = 200;
        var editor = new TextEditor { Text = "a TODO b" };
        editor.TextArea.TextView.ElementGenerators.Add(new TodoGenerator());
        editor.Measure(new Size(WIDTH, HEIGHT));
        editor.Arrange(new Rect(0, 0, WIDTH, HEIGHT));

        var line = editor.TextArea.TextView.GetVisualLine(1);

        Assert.IsNotNull(line);
        Assert.AreEqual(2, line.GetVisualColumn(2), "The element start keeps its column.");
        Assert.AreEqual(2, line.GetVisualColumn(4), "A column inside the element collapses to its start.");
        Assert.AreEqual(2, line.GetRelativeOffset(2));
        Assert.AreEqual(line.VisualLength, line.ValidateVisualColumn(line.VisualLength + 5));
    }

    [TestMethod]
    public void DecoratingElementsKeepTheIdentityProjectionAndStayOutOfTheRuns()
    {
        var editor = new TextEditor { Text = "see www.example.com now" };
        editor.TextArea.TextView.ElementGenerators.Add(new LinkElementGenerator(LinkElementGenerator.DefaultLinkRegex));

        (var projected, var runs) = RunPipeline(editor);

        // The text is untouched on both counts: the projection leaves it alone and no inline run
        // takes it out of the engine's hands.
        Assert.AreEqual(editor.Text, projected.Text.ToString());
        Assert.IsEmpty(runs);
    }

    [TestMethod]
    public void LinkElementCarriesItsTargetAndAnswersTheCursorQuery()
    {
        var editor = new TextEditor { Text = "see www.example.com now" };
        var generator = new LinkElementGenerator(LinkElementGenerator.DefaultLinkRegex);
        editor.TextArea.TextView.ElementGenerators.Add(generator);

        RunPipeline(editor);
        var scanned = editor.TextArea.TextView.VisualLines;

        // Without a laid-out window there are no visual lines, so construct the element directly.
        generator.StartGeneration(new FakeContext(editor));
        var element = generator.ConstructElement(4);
        generator.FinishGeneration();

        var link = (VisualLineLinkText)element!;
        Assert.AreEqual("http://www.example.com", link.NavigateUri, "www links gain the scheme.");

        var plain = new QueryCursorEventArgs(default, ModifierKeys.None);
        link.OnQueryCursor(plain);
        Assert.IsNull(plain.Cursor, "Without Ctrl the text cursor stays.");

        var withControl = new QueryCursorEventArgs(default, ModifierKeys.Control);
        link.OnQueryCursor(withControl);
        Assert.AreEqual(CursorType.Hand, withControl.Cursor);
        Assert.IsEmpty(scanned);
    }

    private sealed class FakeContext(TextEditor editor) : ITextRunConstructionContext
    {
        public Aprillz.MewUI.MewvalonEdit.Document.TextDocument Document => editor.Document;
        public Aprillz.MewUI.MewvalonEdit.Rendering.TextView TextView => editor.TextArea.TextView;
        public Aprillz.MewUI.MewvalonEdit.Document.DocumentLine CurrentDocumentLine
            => editor.Document.GetLineByNumber(1);
        public TextRunStyle DefaultStyle => new(editor.FontFamily, editor.FontSize, editor.FontWeight);
    }
}
