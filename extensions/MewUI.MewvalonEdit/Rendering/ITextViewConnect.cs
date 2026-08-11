namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>
/// Implemented by objects that need to know which views they are shown in. A segment collection
/// attaches this way, so it can redraw the views holding it when a segment moves.
/// </summary>
public interface ITextViewConnect
{
    void AddToTextView(TextView textView);

    void RemoveFromTextView(TextView textView);
}
