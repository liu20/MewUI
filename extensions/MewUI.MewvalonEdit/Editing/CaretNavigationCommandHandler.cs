using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace Aprillz.MewUI.MewvalonEdit.Editing;

/// <summary>How the caret is asked to move.</summary>
public enum CaretMovementType
{
    None,
    CharLeft,
    CharRight,
    Backspace,
    WordLeft,
    WordRight,
    LineUp,
    LineDown,
    PageUp,
    PageDown,
    LineStart,
    LineEnd,
    DocumentStart,
    DocumentEnd
}

/// <summary>
/// The caret movement the box-selection keys use. The ordinary movement keys live in the editing
/// surface; these run beside them because a rectangle needs virtual space the surface's clamped
/// offsets cannot express.
/// </summary>
internal static class CaretNavigationCommandHandler
{
    /// <summary>
    /// One box-selection step: the selection is converted to a rectangle first, so virtual space is
    /// enabled for the movement itself, then the caret moves and the rectangle follows it.
    /// </summary>
    internal static void MoveCaretBoxSelection(TextArea textArea, CaretMovementType direction)
    {
        if (textArea.Options.EnableRectangularSelection && textArea.Selection is not RectangleSelection)
        {
            textArea.Selection = textArea.Selection.IsEmpty
                ? new RectangleSelection(textArea, textArea.Caret.Position, textArea.Caret.Position)
                : new RectangleSelection(textArea, textArea.Selection.StartPosition, textArea.Caret.Position);
        }
        var oldPosition = textArea.Caret.Position;
        // The caret moves before the rectangle's corner follows it, and that gap must not be read
        // as the caret leaving the rectangle.
        textArea.RunOwningSurface(() => MoveCaret(textArea, direction));
        textArea.Selection = textArea.Selection.StartSelectionOrSetEndpoint(oldPosition, textArea.Caret.Position);
        textArea.Caret.BringCaretToView();
    }

    internal static void MoveCaret(TextArea textArea, CaretMovementType direction)
    {
        double desiredXPos = textArea.Caret.DesiredXPos;
        textArea.Caret.Position = GetNewCaretPosition(
            textArea, textArea.Caret.Position, direction, textArea.Selection.EnableVirtualSpace, ref desiredXPos);
        textArea.Caret.DesiredXPos = desiredXPos;
    }

    internal static TextViewPosition GetNewCaretPosition(
        TextArea textArea, TextViewPosition caretPosition, CaretMovementType direction,
        bool enableVirtualSpace, ref double desiredXPos)
    {
        var document = textArea.Document;
        switch (direction)
        {
            case CaretMovementType.None:
                return caretPosition;
            case CaretMovementType.DocumentStart:
                desiredXPos = double.NaN;
                return new TextViewPosition(1, 1);
            case CaretMovementType.DocumentEnd:
                desiredXPos = double.NaN;
                return new TextViewPosition(document.GetLocation(document.TextLength));
        }
        var caretLine = document.GetLineByNumber(caretPosition.Line);
        var visualLine = textArea.TextView.GetOrConstructVisualLine(caretLine);
        if (visualLine is null)
        {
            return caretPosition;
        }
        var textLine = visualLine.GetTextLine(caretPosition.VisualColumn, caretPosition.IsAtEndOfLine);
        switch (direction)
        {
            case CaretMovementType.CharLeft:
                desiredXPos = double.NaN;
                // Do not move the caret to the previous line in virtual space.
                if (caretPosition.VisualColumn == 0 && enableVirtualSpace)
                {
                    return caretPosition;
                }
                return GetPrevCaretPosition(textArea, caretPosition, visualLine, CaretPositioningMode.Normal, enableVirtualSpace);
            case CaretMovementType.Backspace:
                desiredXPos = double.NaN;
                return GetPrevCaretPosition(textArea, caretPosition, visualLine, CaretPositioningMode.EveryCodepoint, enableVirtualSpace);
            case CaretMovementType.CharRight:
                desiredXPos = double.NaN;
                return GetNextCaretPosition(textArea, caretPosition, visualLine, CaretPositioningMode.Normal, enableVirtualSpace);
            case CaretMovementType.WordLeft:
                desiredXPos = double.NaN;
                return GetPrevCaretPosition(textArea, caretPosition, visualLine, CaretPositioningMode.WordStart, enableVirtualSpace);
            case CaretMovementType.WordRight:
                desiredXPos = double.NaN;
                return GetNextCaretPosition(textArea, caretPosition, visualLine, CaretPositioningMode.WordStart, enableVirtualSpace);
            case CaretMovementType.LineUp:
            case CaretMovementType.LineDown:
                return GetUpDownCaretPosition(textArea, caretPosition, direction, visualLine, textLine, enableVirtualSpace, ref desiredXPos);
            case CaretMovementType.LineStart:
                desiredXPos = double.NaN;
                return GetStartOfLineCaretPosition(caretPosition.VisualColumn, visualLine, textLine, enableVirtualSpace);
            case CaretMovementType.LineEnd:
                desiredXPos = double.NaN;
                return GetEndOfLineCaretPosition(visualLine, textLine);
            default:
                throw new NotSupportedException(direction.ToString());
        }
    }

    private static TextViewPosition GetStartOfLineCaretPosition(
        int oldVisualColumn, VisualLine visualLine, VisualTextLine textLine, bool enableVirtualSpace)
    {
        int newVisualColumn = visualLine.GetTextLineVisualStartColumn(textLine);
        if (newVisualColumn == 0)
        {
            newVisualColumn = visualLine.GetNextCaretPosition(
                newVisualColumn - 1, LogicalDirection.Forward, CaretPositioningMode.WordStart, enableVirtualSpace);
        }
        if (newVisualColumn < 0)
        {
            newVisualColumn = 0;
        }
        // When the caret is already at the start of the text, jump to the start before the whitespace.
        if (newVisualColumn == oldVisualColumn)
        {
            newVisualColumn = 0;
        }
        return visualLine.GetTextViewPosition(newVisualColumn);
    }

    private static TextViewPosition GetEndOfLineCaretPosition(VisualLine visualLine, VisualTextLine textLine)
    {
        int newVisualColumn = textLine.LogicalStart + textLine.LogicalLength;
        return visualLine.GetTextViewPosition(newVisualColumn) with { IsAtEndOfLine = true };
    }

    private static TextViewPosition GetNextCaretPosition(
        TextArea textArea, TextViewPosition caretPosition, VisualLine visualLine,
        CaretPositioningMode mode, bool enableVirtualSpace)
    {
        int pos = visualLine.GetNextCaretPosition(
            caretPosition.VisualColumn, LogicalDirection.Forward, mode, enableVirtualSpace);
        if (pos >= 0)
        {
            return visualLine.GetTextViewPosition(pos);
        }
        // Move to the start of the next line.
        var document = textArea.Document;
        int nextLineNumber = visualLine.FirstDocumentLine.LineNumber + 1;
        if (nextLineNumber <= document.LineCount)
        {
            var nextLine = textArea.TextView.GetOrConstructVisualLine(document.GetLineByNumber(nextLineNumber));
            if (nextLine is null)
            {
                return caretPosition;
            }
            pos = nextLine.GetNextCaretPosition(-1, LogicalDirection.Forward, mode, enableVirtualSpace);
            return pos < 0 ? caretPosition : nextLine.GetTextViewPosition(pos);
        }
        return new TextViewPosition(document.GetLocation(document.TextLength));
    }

    private static TextViewPosition GetPrevCaretPosition(
        TextArea textArea, TextViewPosition caretPosition, VisualLine visualLine,
        CaretPositioningMode mode, bool enableVirtualSpace)
    {
        int pos = visualLine.GetNextCaretPosition(
            caretPosition.VisualColumn, LogicalDirection.Backward, mode, enableVirtualSpace);
        if (pos >= 0)
        {
            return visualLine.GetTextViewPosition(pos);
        }
        // Move to the end of the previous line.
        var document = textArea.Document;
        int previousLineNumber = visualLine.FirstDocumentLine.LineNumber - 1;
        if (previousLineNumber >= 1)
        {
            var previousLine = textArea.TextView.GetOrConstructVisualLine(document.GetLineByNumber(previousLineNumber));
            if (previousLine is null)
            {
                return caretPosition;
            }
            pos = previousLine.GetNextCaretPosition(
                previousLine.VisualLength + 1, LogicalDirection.Backward, mode, enableVirtualSpace);
            return pos < 0 ? caretPosition : previousLine.GetTextViewPosition(pos);
        }
        return new TextViewPosition(1, 1);
    }

    private static TextViewPosition GetUpDownCaretPosition(
        TextArea textArea, TextViewPosition caretPosition, CaretMovementType direction,
        VisualLine visualLine, VisualTextLine textLine, bool enableVirtualSpace, ref double xPos)
    {
        // Moving up and down happens at the desired visual x position, kept across lines.
        if (double.IsNaN(xPos))
        {
            xPos = visualLine.GetTextLineVisualXPosition(textLine, caretPosition.VisualColumn);
        }
        var document = textArea.Document;
        var targetVisualLine = visualLine;
        VisualTextLine? targetLine = null;
        int textLineIndex = IndexOfRow(visualLine, textLine);
        if (direction == CaretMovementType.LineUp)
        {
            // The previous row of the same visual line, or the last row of the previous one.
            int previousLineNumber = visualLine.FirstDocumentLine.LineNumber - 1;
            if (textLineIndex > 0)
            {
                targetLine = visualLine.TextLines[textLineIndex - 1];
            }
            else if (previousLineNumber >= 1)
            {
                var candidate = textArea.TextView.GetOrConstructVisualLine(document.GetLineByNumber(previousLineNumber));
                if (candidate is not null)
                {
                    targetVisualLine = candidate;
                    targetLine = candidate.TextLines[^1];
                }
            }
        }
        else
        {
            // The next row of the same visual line, or the first row of the next one.
            int nextLineNumber = visualLine.FirstDocumentLine.LineNumber + 1;
            if (textLineIndex < visualLine.TextLines.Count - 1)
            {
                targetLine = visualLine.TextLines[textLineIndex + 1];
            }
            else if (nextLineNumber <= document.LineCount)
            {
                var candidate = textArea.TextView.GetOrConstructVisualLine(document.GetLineByNumber(nextLineNumber));
                if (candidate is not null)
                {
                    targetVisualLine = candidate;
                    targetLine = candidate.TextLines[0];
                }
            }
        }
        if (targetLine is null)
        {
            return caretPosition;
        }
        double yPos = targetLine.Bounds.Y + targetLine.Bounds.Height / 2;
        int newVisualColumn = targetVisualLine.GetVisualColumn(new Point(xPos, yPos), enableVirtualSpace);

        // Prevent falling past the row into the next one when the x lies beyond the row's text.
        int targetRowEnd = targetLine.LogicalStart + targetLine.LogicalLength;
        if (newVisualColumn >= targetRowEnd && newVisualColumn <= targetVisualLine.VisualLength)
        {
            newVisualColumn = Math.Max(targetLine.LogicalStart, targetRowEnd - 1);
        }
        return targetVisualLine.GetTextViewPosition(newVisualColumn);
    }

    private static int IndexOfRow(VisualLine visualLine, VisualTextLine row)
    {
        for (int index = 0; index < visualLine.TextLines.Count; index++)
        {
            if (ReferenceEquals(visualLine.TextLines[index], row))
            {
                return index;
            }
        }
        return 0;
    }
}
