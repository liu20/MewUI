using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.CodeCompletion;

public interface ICompletionData
{
    string Text { get; }
    object? Content { get; }
    object? Description { get; }

    /// <summary>The icon shown beside the entry, or null for none.</summary>
    IImageSource? Image => null;

    double Priority { get; }
    void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs);
}

public class CompletionData : ICompletionData
{
    public CompletionData(string text, object? description = null, double priority = 0, IImageSource? image = null)
    {
        Text = text ?? throw new ArgumentNullException(nameof(text));
        Description = description;
        Priority = priority;
        Image = image;
    }

    public string Text { get; }
    public virtual object Content => Text;
    public object? Description { get; }
    public IImageSource? Image { get; }
    public double Priority { get; }

    public virtual void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        ArgumentNullException.ThrowIfNull(completionSegment);
        textArea.Document.Replace(completionSegment.Offset, completionSegment.Length, Text);
        textArea.Editor.Select(completionSegment.Offset + Text.Length, 0);
    }
}
