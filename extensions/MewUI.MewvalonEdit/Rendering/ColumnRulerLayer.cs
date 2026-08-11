using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>Draws the vertical rule at the configured column.</summary>
internal sealed class ColumnRulerLayer(TextEditorOptions options, TextEditor editor) : ITextViewLayer
{
    public void Draw(ITextRenderContext context, Rect viewportBounds)
    {
        if (!options.ShowColumnRuler || options.ColumnRulerPosition <= 0)
        {
            return;
        }

        var view = editor.TextArea.TextView;
        double x = viewportBounds.X + options.ColumnRulerPosition * view.WideSpaceWidth
            - editor.Surface.ScrollOffset.X;
        if (x < viewportBounds.X || x > viewportBounds.Right)
        {
            return;
        }

        double scale = editor.EditorDpi / 96.0;
        var pen = view.ResolvedColumnRulerPen.SnapThickness(scale);
        x = pen.SnapStrokeCenter(x, scale);

        context.Graphics.DrawLine(
            new Point(x, viewportBounds.Y), new Point(x, viewportBounds.Bottom), pen.ToPen());
    }
}

/// <summary>Paints the line holding the caret, under the text.</summary>
internal sealed class CurrentLineLayer(TextEditorOptions options, TextEditor editor) : ITextViewLayer
{
    public void Draw(ITextRenderContext context, Rect viewportBounds)
    {
        if (!options.HighlightCurrentLine)
        {
            return;
        }

        var surface = editor.Surface;
        int caretLine = editor.Document.GetLocation(editor.CaretOffset).Line;
        var view = editor.TextArea.TextView;
        double scale = editor.EditorDpi / 96.0;
        foreach (var line in surface.VisibleTextLines)
        {
            if (line.LogicalLine.LineNumber + 1 != caretLine)
            {
                continue;
            }

            // Snapped edges, or the band changes height by a pixel from row to row and the
            // highlight appears to jump as the caret moves.
            var bounds = LayoutRounding.SnapRectEdgesToPixels(
                new Rect(
                    viewportBounds.X,
                    viewportBounds.Y + line.DocumentY - surface.ScrollOffset.Y,
                    viewportBounds.Width,
                    line.Height),
                scale);
            var pen = view.CurrentLineBorder.SnapThickness(scale);
            context.Graphics.FillRectangle(bounds, view.CurrentLineBackground);
            // Inset by half the stroke, which is centred on the edge it is given: on the snapped
            // edge itself it would cover half a pixel on each side and blur as rows change height.
            context.Graphics.DrawRectangle(
                bounds.Deflate(new Thickness(pen.Thickness / 2)), pen.ToPen());
            return;
        }
    }
}
