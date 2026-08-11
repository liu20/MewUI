using Aprillz.MewUI.Rendering;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>
/// Draws the end-of-line marker past the last character of each line. It cannot be an element: there
/// is no document character at that position to stand in for. The original produces it from the text
/// source once the visual line runs out of elements, which in a push model is a layer.
/// </summary>
internal sealed class EndOfLineMarkerLayer(TextEditorOptions options, TextEditor editor) : ITextViewLayer
{
    private const char END_OF_LINE_MARKER = '¶';

    private static readonly TextParagraphStyle _markerParagraph = new()
    {
        Wrapping = TextWrapping.NoWrap,
        MaxWidth = double.PositiveInfinity
    };

    public void Draw(ITextRenderContext context, Rect viewportBounds)
    {
        if (!options.ShowEndOfLine)
        {
            return;
        }

        var surface = editor.Surface;
        var lines = surface.VisibleTextLines;
        if (lines.Count == 0)
        {
            return;
        }

        var factory = Application.IsRunning ? Application.Current.GraphicsFactory : Application.DefaultGraphicsFactory;
        uint dpi = editor.GetDpi();
        var style = new TextRunStyle(editor.FontFamily, editor.FontSize, editor.FontWeight);
        var drawOptions = new TextDrawOptions(editor.WhitespaceMarkerColor);
        var scroll = surface.ScrollOffset;

        foreach (var line in lines)
        {
            double documentY = line.VisualLines.Count == 0 ? 0 : line.VisualLines[0].Bounds.Y;
            // Past the last character, so the position comes from the caret rather than a cell.
            var caret = line.GetCaretBounds(
                new CharacterHit(line.MapSourceOffsetToProjected(line.LogicalLine.Length), 0));
            Draw(
                context,
                factory,
                style,
                dpi,
                in drawOptions,
                new Point(
                    viewportBounds.X - scroll.X + caret.X,
                    viewportBounds.Y + documentY - scroll.Y + caret.Y));
        }
    }

    private static void Draw(
        ITextRenderContext context,
        IGraphicsFactory factory,
        TextRunStyle style,
        uint dpi,
        in TextDrawOptions drawOptions,
        Point origin)
    {
        var layout = factory.TextEngine.GetOrCreateLayout(
            new TextLayoutRequest
            {
                Text = END_OF_LINE_MARKER.ToString().AsMemory(),
                Dpi = dpi,
                DefaultStyle = style,
                Paragraph = _markerParagraph
            },
            TextLayoutCachePolicy.Content);
        context.Draw(layout, origin, in drawOptions);
    }
}
