using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.CodeCompletion;

/// <summary>Insight window that shows an <see cref="OverloadViewer"/>.</summary>
public class OverloadInsightWindow : InsightWindow
{
    private readonly OverloadViewer _overloadViewer = new();

    public OverloadInsightWindow(TextArea textArea) : base(textArea)
    {
        _overloadViewer.Margin = new Thickness(2, 0, 0, 0);
        Content = _overloadViewer;
    }

    /// <summary>The item provider.</summary>
    public IOverloadProvider? Provider
    {
        get => _overloadViewer.Provider;
        set => _overloadViewer.Provider = value;
    }

    /// <summary>
    /// Up and Down walk the overloads while more than one is offered; with a single overload the
    /// keys stay the editor's.
    /// </summary>
    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);
        if (!e.Handled && Provider is IOverloadProvider provider && provider.Count > 1)
        {
            switch (e.Key)
            {
                case Key.Up:
                    e.Handled = true;
                    _overloadViewer.ChangeIndex(-1);
                    break;
                case Key.Down:
                    e.Handled = true;
                    _overloadViewer.ChangeIndex(+1);
                    break;
            }
            if (e.Handled)
            {
                UpdatePosition();
            }
        }
    }
}
