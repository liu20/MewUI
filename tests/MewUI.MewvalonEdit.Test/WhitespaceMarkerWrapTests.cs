using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// A space marker stands in the place of a space. Showing it must not change where the text wraps,
/// because the marker is an appearance option and the reader did not ask for a different layout.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class WhitespaceMarkerWrapTests
{
    private const string TEXT =
        "the quick brown fox jumps over the lazy dog and then runs back again to where it started";

    [TestMethod]
    public void ShowingSpacesDoesNotChangeWhereTheTextWraps()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        int[] without = RowStarts(showSpaces: false);
        int[] with = RowStarts(showSpaces: true);

        Assert.IsGreaterThan(1, without.Length, "The sample text did not wrap.");
        // Every row but the first begins right after a space while the marker is off, which is what
        // has to stay true with it on.
        foreach (int start in without.Skip(1))
        {
            Assert.IsTrue(char.IsWhiteSpace(TEXT[start - 1]), $"Row at {start} did not start after a space.");
        }
        CollectionAssert.AreEqual(without, with, "Turning the space marker on moved the wrap positions.");
    }

    private static int[] RowStarts(bool showSpaces)
        => Build(showSpaces).TextArea.TextView.Host.VisibleTextLines[0]
            .VisualLines.Select(static row => row.LogicalStart).ToArray();

    /// <summary>Guards the case above: without markers on the line it proves nothing.</summary>
    [TestMethod]
    public void ShowingSpacesPutsMarkersOnTheLine()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = Build(showSpaces: true);

        Assert.IsGreaterThan(0, editor.TextArea.TextView.VisualLines[0].Elements.Count,
            "The space marker generator produced no element.");
    }

    private static int CountRows(bool showSpaces)
        => Build(showSpaces).TextArea.TextView.Host.VisibleTextLines[0].VisualLines.Count;

    private static TextEditor Build(bool showSpaces)
    {
        var editor = new TextEditor { Text = TEXT, ShowLineNumbers = false, SkipViewportCull = true };
        editor.WordWrap = true;
        editor.Options.ShowSpaces = showSpaces;
        editor.Measure(new Size(220, 300));
        editor.Arrange(new Rect(0, 0, 220, 300));
        return editor;
    }
}
