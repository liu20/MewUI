using Aprillz.MewUI.Controls;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.CodeCompletion;

/// <summary>
/// A popup-like window attached to a text segment, such as a parameter hint. It closes when the
/// caret leaves the segment; unlike the completion window it neither filters nor takes the
/// movement keys, so typing and navigation stay entirely the editor's.
/// </summary>
public class InsightWindow : CompletionWindowBase
{
    public InsightWindow(TextArea textArea) : base(textArea)
    {
        // The tooltip look is this window's own, as its template is in the original; the base
        // host carries no frame.
        Root.Padding = new Thickness(1, 1, 3, 1);
        Root.WithTheme(static (theme, root) =>
        {
            root.BorderThickness = theme.Metrics.ControlBorderThickness;
            root.CornerRadius = theme.Metrics.ControlCornerRadius;
            root.Background = theme.Palette.ContainerBackground;
            root.BorderBrush = theme.Palette.ControlBorder;
        });
    }

    /// <summary>Whether the window closes when the caret leaves the segment. The default is true.</summary>
    public bool CloseAutomatically { get; set; } = true;

    /// <summary>The element shown in the window.</summary>
    public FrameworkElement? Content
    {
        get => Root.Child as FrameworkElement;
        set => Root.Child = value;
    }

    protected override void AttachEvents()
    {
        base.AttachEvents();
        TextArea.Caret.PositionChanged += OnCaretPositionChanged;
    }

    protected override void DetachEvents()
    {
        TextArea.Caret.PositionChanged -= OnCaretPositionChanged;
        base.DetachEvents();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        if (CloseAutomatically)
        {
            int offset = TextArea.Caret.Offset;
            if (offset < StartOffset || offset > EndOffset)
            {
                Close();
            }
        }
    }
}
