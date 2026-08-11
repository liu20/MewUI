using Aprillz.MewUI;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Editing;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Starting a box selection over again. A vertical walk remembers the x it started at so a run of
/// them keeps one column, but that memory belongs to the walk: once the caret moves any other way,
/// the next box selection starts from where the caret is now.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class BoxSelectionRestartTests
{
    private const string TEXT = "abcdefgh\nijklmnop\nqrstuvwx";

    [TestMethod]
    public void ANewBoxSelectionStartsFromTheCaretNotTheOldEdge()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var (window, editor) = Host();
        // A box out to column 5 and down a line, which is what leaves a remembered x behind.
        for (int step = 0; step < 5; step++)
        {
            Press(window, Key.Right, ModifierKeys.Alt | ModifierKeys.Shift);
        }
        Press(window, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift);
        window.PerformLayout();
        Assert.IsInstanceOfType<RectangleSelection>(editor.TextArea.Selection);

        // Ordinary movement, which the editing surface handles: the rectangle dissolves.
        Press(window, Key.Home);
        window.PerformLayout();
        Assert.AreEqual(0, editor.SelectionLength, "an ordinary move left the rectangle standing");

        Press(window, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift);
        window.PerformLayout();

        var rectangle = (RectangleSelection)editor.TextArea.Selection;
        foreach (var segment in rectangle.Segments)
        {
            Assert.AreEqual(0, segment.Length,
                "the new box selection took the column the previous one ended at");
        }
    }

    /// <summary>A run of vertical box steps still keeps the column it started from.</summary>
    [TestMethod]
    public void AVerticalWalkKeepsItsColumn()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("The GDI backend is Windows-only."); return; }

        var (window, editor) = Host();
        for (int step = 0; step < 5; step++)
        {
            Press(window, Key.Right, ModifierKeys.Alt | ModifierKeys.Shift);
        }

        Press(window, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift);
        Press(window, Key.Down, ModifierKeys.Alt | ModifierKeys.Shift);
        window.PerformLayout();

        var rectangle = (RectangleSelection)editor.TextArea.Selection;
        Assert.HasCount(3, rectangle.Segments.ToArray());
        foreach (var segment in rectangle.Segments)
        {
            Assert.AreEqual(5, segment.Length, "a line of the box lost the column the walk started at");
        }
    }

    private static void Press(Window window, Key key, ModifierKeys modifiers = ModifierKeys.None)
        => WindowInputRouter.KeyDown(window, new KeyEventArgs(key, platformKey: 0, modifiers));

    private static (Window Window, TextEditor Editor) Host()
    {
        var window = ScaledWindow.Create(1.0, 800, 300);
        var editor = new TextEditor
        {
            Text = TEXT,
            SkipViewportCull = true,
            FontFamily = "Consolas",
            FontSize = 13
        };
        window.Content = editor;
        window.PerformLayout();
        editor.Focus();
        editor.CaretOffset = 0;
        window.PerformLayout();
        return (window, editor);
    }
}
