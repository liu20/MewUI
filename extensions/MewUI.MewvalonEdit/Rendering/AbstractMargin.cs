using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.Rendering;

namespace Aprillz.MewUI.MewvalonEdit.Rendering;

/// <summary>
/// Base for anything placed beside the text, such as line numbers or a folding gutter. A margin is
/// a control, so it takes input; only the view layers are draw-only.
/// </summary>
public abstract class AbstractMargin : Control, ITextViewConnect
{
    public static readonly MewProperty<TextView?> TextViewProperty =
        MewProperty<TextView?>.Register<AbstractMargin>(nameof(TextView), null,
            MewPropertyOptions.AffectsLayout,
            static (self, oldValue, newValue) => self.AttachToTextView(oldValue, newValue));

    /// <summary>View this margin sits beside. Setting it runs the connect and disconnect hooks.</summary>
    public TextView? TextView
    {
        get => GetValue(TextViewProperty);
        set => SetValue(TextViewProperty, value);
    }

    /// <summary>Document behind <see cref="TextView"/>, or null while unattached.</summary>
    public TextDocument? Document => TextView?.Document;

    private void AttachToTextView(TextView? oldValue, TextView? newValue)
    {
        if (oldValue is not null)
        {
            oldValue.Host.LinesChanged -= OnHostLinesChanged;
            oldValue.Host.ScrollOffsetChanged -= OnHostScrolled;
            RemoveFromTextView(oldValue);
        }

        var oldDocument = oldValue?.Document;
        if (newValue is not null)
        {
            newValue.Host.LinesChanged += OnHostLinesChanged;
            newValue.Host.ScrollOffsetChanged += OnHostScrolled;
            AddToTextView(newValue);
        }

        OnTextViewChanged(oldValue, newValue);
        if (!ReferenceEquals(oldDocument, Document))
        {
            OnDocumentChanged(oldDocument, Document);
        }
        // AffectsLayout covers the measure pass; the repaint is not implied by it.
        InvalidateVisual();
    }

    /// <summary>Called after <see cref="TextView"/> changed.</summary>
    protected virtual void OnTextViewChanged(TextView? oldValue, TextView? newValue)
    {
    }

    /// <summary>Called after the attached view's document changed.</summary>
    protected virtual void OnDocumentChanged(TextDocument? oldValue, TextDocument? newValue)
    {
    }

    void ITextViewConnect.AddToTextView(TextView textView) => AddToTextView(textView);

    void ITextViewConnect.RemoveFromTextView(TextView textView) => RemoveFromTextView(textView);

    /// <summary>Called while attaching, before <see cref="OnTextViewChanged"/>.</summary>
    protected virtual void AddToTextView(TextView textView)
    {
    }

    /// <summary>Called while detaching, before <see cref="OnTextViewChanged"/>.</summary>
    protected virtual void RemoveFromTextView(TextView textView)
    {
    }

    protected sealed override void OnRender(IGraphicsContext context)
    {
        OnRenderMargin(context);
        if (TextView is not TextView view)
        {
            return;
        }

        var textViewport = view.Host.TextViewportBounds;
        var clip = Bounds.Intersect(new Rect(Bounds.X, textViewport.Y, Bounds.Width, textViewport.Height));
        if (clip.IsEmpty)
        {
            return;
        }

        context.Save();
        try
        {
            context.SetClip(LayoutRounding.MakeClipRect(clip, GetDpi() / 96.0));
            OnRenderTextViewport(context, textViewport);
        }
        finally
        {
            context.Restore();
        }
    }

    /// <summary>Draws fixed margin chrome outside the scrolling viewport clip.</summary>
    protected virtual void OnRenderMargin(IGraphicsContext context)
    {
    }

    /// <summary>Draws content clipped to this margin's share of the text viewport.</summary>
    protected abstract void OnRenderTextViewport(IGraphicsContext context, Rect textViewport);

    private void OnHostLinesChanged(Aprillz.MewUI.Text.ITextViewHost host) => InvalidateVisual();

    private void OnHostScrolled(Aprillz.MewUI.Text.ITextViewHost host) => InvalidateVisual();
}
