using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Editing;

/// <summary>
/// Draws the caret in place of the editing surface's own. Taking the anchor is what lets the caret
/// be coloured, hidden, and widened for overstrike; the blink itself stays the surface's, so both
/// agree on when the caret is in its visible phase.
/// </summary>
internal sealed class CaretLayer(TextArea textArea) : ITextViewLayer
{
    // The overwritten character has to stay readable under the caret covering it.
    private const byte OVERSTRIKE_ALPHA = 100;
    private const double MINIMUM_WIDTH = 1;

    // A rectangle selection types into every line it crosses, which is worth saying in the caret
    // itself: Visual Studio colours its carets only while several are live, and leaves the ordinary
    // one alone. The active corner and the lines following it are told apart the same way.
    private static readonly Color _defaultPrimaryCaret = Color.FromRgb(214, 64, 64);
    private static readonly Color _defaultSecondaryCaret = Color.FromRgb(64, 132, 214);

    public void Draw(ITextRenderContext context, Rect viewportBounds)
    {
        ArgumentNullException.ThrowIfNull(context);
        var surface = textArea.Editor.Surface;
        // Focus is not asked about here: the caret follows Show/Hide, and those follow focus on their
        // own. That is what lets a search put the caret on its match while the reader is still typing
        // in the search box.
        if (!textArea.Caret.IsVisible || !surface.CaretVisible)
        {
            return;
        }

        var caret = textArea.Caret;
        bool several = textArea.Selection is RectangleSelection;
        var color = several
            ? caret.PrimaryCaretBrush ?? caret.CaretBrush ?? _defaultPrimaryCaret
            : caret.CaretBrush ?? textArea.Editor.Foreground;
        var secondary = caret.SecondaryCaretBrush ?? _defaultSecondaryCaret;
        if (textArea.OverstrikeMode)
        {
            color = Color.FromArgb(OVERSTRIKE_ALPHA, color.R, color.G, color.B);
            secondary = Color.FromArgb(OVERSTRIKE_ALPHA, secondary.R, secondary.G, secondary.B);
        }
        double dpiScale = textArea.TextView.DpiScale;
        foreach ((var rectangle, bool primary) in GetCaretRectangles(surface))
        {
            if (!rectangle.IsEmpty)
            {
                context.Graphics.FillRectangle(SnapToPixels(rectangle, dpiScale), primary ? color : secondary);
            }
        }
    }

    /// <summary>
    /// Every caret to draw, and which one the reader is driving. A rectangle selection edits each
    /// line it crosses, so each of them shows where typing will land; the original draws its one
    /// caret and leaves the rest to the box.
    /// </summary>
    internal IEnumerable<(Rect Rectangle, bool Primary)> GetCaretRectangles(Controls.MultiLineTextBox surface)
    {
        if (textArea.Selection is RectangleSelection rectangle)
        {
            int caretOffset = textArea.Caret.Offset;
            foreach ((int offset, int visualColumn) in rectangle.CaretEdges())
            {
                yield return (GetCaretRectangle(surface, offset, visualColumn), offset == caretOffset);
            }
            yield break;
        }
        yield return (GetCaretRectangle(surface), true);
    }

    /// <summary>
    /// A window-coordinate rectangle: one column wide normally, and as wide as the character it
    /// would overwrite in overstrike mode.
    /// </summary>
    internal Rect GetCaretRectangle(Controls.MultiLineTextBox surface)
        => GetCaretRectangle(surface, textArea.Caret.Offset, textArea.Caret.Position.VisualColumn);

    private Rect GetCaretRectangle(Controls.MultiLineTextBox surface, int offset, int visualColumn)
    {
        var caret = surface.GetCharRectInWindow(offset);
        if (caret.IsEmpty)
        {
            return Rect.Empty;
        }
        double x = caret.X + GetVirtualSpaceWidth(offset, visualColumn);

        double width = MINIMUM_WIDTH;
        if (textArea.OverstrikeMode)
        {
            var line = textArea.Document.GetLineByOffset(offset);
            // Nothing to overwrite past the last character, so the caret stays a thin one there.
            if (offset < line.EndOffset)
            {
                var next = surface.GetCharRectInWindow(offset + 1);
                if (next.Y == caret.Y)
                {
                    width = Math.Max(MINIMUM_WIDTH, next.X - caret.X);
                }
            }
        }
        return new Rect(x, caret.Y, width, Math.Max(MINIMUM_WIDTH, caret.Height));
    }

    /// <summary>
    /// How far past the end of its line a caret stands. Columns in virtual space carry no
    /// characters, so the document offset the surface is asked about is the line's end and points at
    /// the wrong place; each column past the end is one wide space, as the original measures them.
    /// </summary>
    private double GetVirtualSpaceWidth(int offset, int visualColumn)
    {
        var line = textArea.TextView.GetOrConstructVisualLine(textArea.Document.GetLineByOffset(offset));
        if (line is null || visualColumn <= line.VisualLength)
        {
            return 0;
        }
        return (visualColumn - line.VisualLength) * textArea.TextView.WideSpaceWidth;
    }

    /// <summary>The caret band on whole device pixels, never thinner than one.</summary>
    private static Rect SnapToPixels(Rect rectangle, double dpiScale)
    {
        // Snapped edges alone would round a one-DIP caret away wherever a scale makes it land
        // inside a pixel, so the width is taken as a thickness instead.
        var snapped = LayoutRounding.SnapBoundsRectToPixels(rectangle, dpiScale);
        return new Rect(
            snapped.X,
            snapped.Y,
            LayoutRounding.SnapThicknessToPixels(rectangle.Width, dpiScale, 1),
            LayoutRounding.SnapThicknessToPixels(rectangle.Height, dpiScale, 1));
    }
}
