using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The ported TextView and TextArea forward to the core host contract. These check the translation
/// (line numbering, anchor mapping, coordinate space), not the core behavior behind it.
/// </summary>
[TestClass]
public sealed class TextViewFacadeTests
{
    private sealed class CountingLayer : ITextViewLayer
    {
        public void Draw(ITextRenderContext context, Rect viewportBounds)
        {
        }
    }

    [TestMethod]
    public void LineCoordinatesTranslateBetweenOneAndZeroBasedNumbering()
    {
        var editor = new TextEditor { Text = "one\ntwo\nthree" };
        var view = editor.TextArea.TextView;

        // Document lines are one-based in the port and zero-based in the core host.
        Assert.AreEqual(0.0, view.GetVisualTopByDocumentLine(1), 0.01);
        Assert.AreEqual(1, view.GetDocumentLineByVisualTop(0));
        Assert.IsGreaterThan(0, view.GetDocumentLineByVisualTop(view.DefaultLineHeight * 1.5));
    }

    [TestMethod]
    public void MetricsComeFromTheHostAndWideSpaceIsMeasuredLocally()
    {
        var editor = new TextEditor { Text = "x" };
        var view = editor.TextArea.TextView;

        Assert.IsGreaterThan(0.0, view.DefaultLineHeight);
        Assert.IsGreaterThan(0.0, view.DefaultBaseline);
        Assert.IsLessThanOrEqualTo(view.DefaultLineHeight, view.DefaultBaseline);
        // Deliberately not the core tab unit, which is defined on the space advance.
        Assert.IsGreaterThan(0.0, view.WideSpaceWidth);
    }

    [TestMethod]
    public void InsertingALayerPlacesItInTheDrawOrder()
    {
        var editor = new TextEditor { Text = "x" };
        var view = editor.TextArea.TextView;
        int before = view.Layers.Count;
        var layer = new CountingLayer();

        view.InsertLayer(layer, KnownLayer.Text, LayerInsertionPosition.Above);

        Assert.HasCount(before + 1, view.Layers);
        Assert.Contains(layer, view.Layers);
    }

    [TestMethod]
    public void RangeRedrawAcceptsOffsetsAndSegments()
    {
        var editor = new TextEditor { Text = "one\ntwo\nthree" };
        var view = editor.TextArea.TextView;

        // The core defers a rebuild requested mid-construction, so these must not throw either way.
        view.Redraw(0, 3);
        view.Redraw(new SimpleSegment(4, 3));
        view.InvalidateLayer(KnownLayer.Selection);
    }

    [TestMethod]
    public void ScrollOffsetAndDocumentHeightForwardToTheHost()
    {
        var editor = new TextEditor { Text = string.Join("\n", Enumerable.Range(0, 50).Select(line => $"line {line}")) };
        var view = editor.TextArea.TextView;

        Assert.AreEqual(view.Host.ScrollOffset.X, view.HorizontalOffset, 0.01);
        Assert.AreEqual(view.Host.ScrollOffset.Y, view.VerticalOffset, 0.01);
        Assert.AreEqual(view.Host.ScrollOffset, view.ScrollOffset);
        Assert.AreEqual(view.Host.ExtentHeight, view.DocumentHeight, 0.01);
    }

    [TestMethod]
    public void TextAreaExposesTheReadOnlySectionHook()
    {
        var editor = new TextEditor { Text = "locked" };
        var provider = new AllReadOnly();

        editor.TextArea.ReadOnlySectionProvider = provider;

        Assert.AreSame(provider, editor.TextArea.ReadOnlySectionProvider);
        editor.TextArea.PerformTextInput("x");
        Assert.AreEqual("locked", editor.Text, "A fully read-only document must reject typed text.");
    }

    [TestMethod]
    public void ClearSelectionCollapsesToTheCaret()
    {
        var editor = new TextEditor { Text = "select me" };
        editor.Select(0, 6);

        editor.TextArea.ClearSelection();

        Assert.AreEqual(0, editor.SelectionLength);
    }

    private sealed class AllReadOnly : IReadOnlySectionProvider
    {
        public bool CanInsert(int offset) => false;

        public IEnumerable<ISegment> GetDeletableSegments(ISegment segment) => [];
    }
}
