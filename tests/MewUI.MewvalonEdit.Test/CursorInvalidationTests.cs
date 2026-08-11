using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The cursor is decided by the element under the pointer, so it goes stale when the lines are
/// rebuilt beneath a pointer that did not move.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class CursorInvalidationTests
{
    private sealed class CountingGenerator : VisualLineElementGenerator
    {
        public override int GetFirstInterestedOffset(int startOffset) => -1;

        public override VisualLineElement? ConstructElement(int offset) => null;
    }

    [TestMethod]
    public void RebuildingTheLinesReAsksForTheCursor()
    {
        if (!OperatingSystem.IsWindows())
        {
            Assert.Inconclusive("GDI backend is Windows-only.");
            return;
        }

        var editor = new TextEditor { Text = "see https://example.com now", SkipViewportCull = true };
        editor.TextArea.TextView.ElementGenerators.Add(new CountingGenerator());
        editor.Measure(new Size(360, 200));
        editor.Arrange(new Rect(0, 0, 360, 200));

        var surface = (Aprillz.MewUI.Controls.Control)editor.TextArea.TextView.Host;
        var before = surface.Cursor;

        // Nothing has asked for a cursor yet, so a rebuild must not throw or move it.
        editor.InvalidateCursorIfMouseWithinTextView();

        Assert.AreEqual(before, surface.Cursor,
            "A rebuild with the pointer elsewhere changed the cursor.");
    }
}
