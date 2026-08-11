using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Ports the shape of AvalonEdit's TextMarkerService sample: a background renderer plus a document
/// colorizing transformer registered through TextView. It is the acceptance case for the
/// compatibility surface.
/// </summary>
[TestClass]
public sealed class CompatibilitySurfaceTests
{
    private sealed class TextMarker(int offset, int length, Color color) : ISegment
    {
        public int Offset => offset;
        public int Length => length;
        public int EndOffset => offset + length;
        public Color Color => color;
    }

    private sealed class TextMarkerService(TextMarker marker)
        : DocumentColorizingTransformer, IBackgroundRenderer
    {
        public KnownLayer Layer => KnownLayer.Selection;
        public List<Rect> DrawnRects { get; } = [];
        public int DrawCount { get; private set; }

        public void Draw(TextView textView, IGraphicsContext context)
        {
            DrawCount++;
            foreach (var rect in BackgroundGeometryBuilder.GetRectsForSegment(textView, marker))
            {
                DrawnRects.Add(rect);
                // The surface is absent in this test; a ported service fills here.
                context?.FillRectangle(rect, marker.Color);
            }
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            int start = Math.Max(marker.Offset, line.Offset);
            int end = Math.Min(marker.EndOffset, line.Offset + line.Length);
            if (end <= start) return;
            ChangeLinePart(start, end, element =>
            {
                element.TextRunProperties.SetForegroundBrush(marker.Color);
                element.TextRunProperties.SetTextDecorations(TextDecoration.Underline);
            });
        }
    }

    [TestMethod]
    public void ColorizingTransformerBecomesPaintSpans()
    {
        var editor = new TextEditor { Text = "hello world" };
        var service = new TextMarkerService(new TextMarker(6, 5, Color.FromRgb(200, 0, 0)));
        editor.TextArea.TextView.LineTransformers.Add(service);

        var spans = ClassifyFirstLine(editor);

        var marker = spans.Single(span => span.Range.Start == 6);
        Assert.AreEqual(5, marker.Range.Length);
        Assert.AreEqual(Color.FromRgb(200, 0, 0), marker.Foreground);
        Assert.AreEqual(TextDecoration.Underline, marker.Decoration);
    }

    [TestMethod]
    public void TypefaceOverrideBecomesAGeometryRun()
    {
        var editor = new TextEditor { Text = "bold text" };
        editor.TextArea.TextView.LineTransformers.Add(new BoldingTransformer());

        var runs = new List<GeometryStyleRun>();
        var inlines = new List<InlineRun>();
        var defaultStyle = new TextRunStyle("Segoe UI", 14);
        foreach (var transformer in editor.TextArea.TextView.Extensions.Transformers)
        {
            transformer.Transform(
                new TextLineTransformContext(
                    new LogicalTextLine(0, 0, editor.Text.Length, editor.Text.Length),
                    editor.Text.AsMemory(),
                    defaultStyle,
                    IdentityTextOffsetMap.Instance),
                runs,
                inlines);
        }

        var run = runs.Single();
        Assert.AreEqual(0, run.Start);
        Assert.AreEqual(4, run.Length);
        Assert.AreEqual(FontWeight.Bold, run.Style.Weight);
        Assert.AreEqual("Consolas", run.Style.FontFamily);
    }

    private sealed class BoldingTransformer : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
            => ChangeLinePart(line.Offset, line.Offset + 4,
                element => element.TextRunProperties.SetTypeface(new Typeface("Consolas", FontWeight.Bold)));
    }

    [TestMethod]
    public void BackgroundRendererRunsOnceOnTheMappedLayer()
    {
        var editor = new TextEditor { Text = "hello world" };
        var service = new TextMarkerService(new TextMarker(0, 5, Color.FromRgb(0, 0, 200)));
        editor.TextArea.TextView.BackgroundRenderers.Add(service);

        // One bridge layer per known anchor; only the mapped one may reach the renderer.
        var layers = editor.TextArea.TextView.Host.Layers.Layers;
        Assert.IsGreaterThanOrEqualTo(4, layers.Count);

        var context = new StubRenderContext();
        foreach (var layer in layers)
        {
            layer.Draw(context, new Rect(0, 0, 100, 100));
        }

        Assert.AreEqual(1, service.DrawCount, "The renderer must run once per frame, on its own layer only.");
    }

    private sealed class StubRenderContext : ITextRenderContext
    {
        public IGraphicsContext Graphics => null!;
        public void Draw(ITextLayout layout, Point origin, in TextDrawOptions options) { }
        public void DrawBackground(ITextLayout layout, Point origin, in TextDrawOptions options) { }
        public void DrawForeground(ITextLayout layout, Point origin, in TextDrawOptions options) { }
    }

    [TestMethod]
    public void TransformerRegistrationInvalidatesTheView()
    {
        var editor = new TextEditor { Text = "hello" };
        long before = editor.TextArea.TextView.Extensions.Revision;

        editor.TextArea.TextView.LineTransformers.Add(new BoldingTransformer());

        Assert.IsGreaterThan(before, editor.TextArea.TextView.Extensions.Revision);
    }

    /// <summary>
    /// The guard TextMarkerService and ILSpy's BookmarkMargin open their Draw with, so a renderer
    /// ported from either compiles and skips the pass while the view has no lines yet.
    /// </summary>
    [TestMethod]
    public void VisualLinesValidReportsWhetherTheViewHasLines()
    {
        var editor = new TextEditor { Text = "hello", SkipViewportCull = true };
        var view = editor.TextArea.TextView;

        Assert.IsFalse(view.VisualLinesValid, "Nothing is laid out before the first measure.");

        editor.Measure(new Size(240, 60));
        editor.Arrange(new Rect(0, 0, 240, 60));

        Assert.IsTrue(view.VisualLinesValid);
        Assert.IsNotEmpty(view.VisualLines);
    }

    private static List<TextPaintSpan> ClassifyFirstLine(TextEditor editor)
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
}
