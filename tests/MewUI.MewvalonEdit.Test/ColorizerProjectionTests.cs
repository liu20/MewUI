using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A transformer names a document range, but a paint span addresses the laid-out text. An element
/// that stands more columns in for the text it covers, such as the tab marker, moves the two apart.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class ColorizerProjectionTests
{
    private sealed class ColorLineTail : DocumentColorizingTransformer
    {
        protected override void ColorizeLine(DocumentLine line)
        {
            ArgumentNullException.ThrowIfNull(line);
            // "abc" of "\tabc": the three characters after the tab.
            ChangeLinePart(line.Offset + 1, line.Offset + 4,
                element => element.TextRunProperties.SetForegroundBrush(Color.FromRgb(0xFF, 0, 0)));
        }
    }

    [TestMethod]
    public void AColouredRangeFollowsTheTabMarkerIntoTheLaidOutText()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = "\tabc", ShowLineNumbers = false, SkipViewportCull = true };
        editor.Options.ShowTabs = true;
        editor.TextArea.TextView.LineTransformers.Add(new ColorLineTail());
        editor.Measure(new Size(360, 200));
        editor.Arrange(new Rect(0, 0, 360, 200));

        var line = editor.TextArea.TextView.Host.VisibleTextLines[0];
        var span = line.PaintSpans.Single(item => item.Foreground == Color.FromRgb(0xFF, 0, 0));

        // The marker occupies two columns where the document has one, so "abc" starts at column 2.
        Assert.AreEqual(2, span.Range.Start, "The colour did not follow the text past the tab marker.");
        Assert.AreEqual(3, span.Range.Length);
    }

    [TestMethod]
    public void AColouredRangeIsUnchangedWithoutAMarker()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = "\tabc", ShowLineNumbers = false, SkipViewportCull = true };
        editor.Options.ShowTabs = false;
        editor.TextArea.TextView.LineTransformers.Add(new ColorLineTail());
        editor.Measure(new Size(360, 200));
        editor.Arrange(new Rect(0, 0, 360, 200));

        var line = editor.TextArea.TextView.Host.VisibleTextLines[0];
        var span = line.PaintSpans.Single(item => item.Foreground == Color.FromRgb(0xFF, 0, 0));

        Assert.AreEqual(1, span.Range.Start);
        Assert.AreEqual(3, span.Range.Length);
    }

    /// <summary>A search result is a document range too, and reaches the view the same way.</summary>
    [TestMethod]
    public void ASearchResultFollowsTheTabMarkerIntoTheLaidOutText()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = "\tabc", ShowLineNumbers = false, SkipViewportCull = true };
        editor.Options.ShowTabs = true;
        var panel = Aprillz.MewUI.MewvalonEdit.Search.SearchPanel.Install(editor);
        panel.SearchPattern = "abc";
        editor.Measure(new Size(360, 200));
        editor.Arrange(new Rect(0, 0, 360, 200));

        var line = editor.TextArea.TextView.Host.VisibleTextLines[0];
        var span = line.PaintSpans.Single(item => item.Background.HasValue);

        Assert.AreEqual(2, span.Range.Start, "The marker did not follow the text past the tab marker.");
        Assert.AreEqual(3, span.Range.Length);
    }
}
