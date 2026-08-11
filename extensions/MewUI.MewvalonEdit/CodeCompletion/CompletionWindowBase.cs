using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.CodeCompletion;

/// <summary>
/// Base class for completion windows. The window rides a <see cref="Popup"/> owned by the editor
/// rather than an OS window of its own: the keyboard focus stays in the editor, and a stacked input
/// handler feeds the keys to the window - which is also what closes it when any other input handler
/// takes over. It anchors to <see cref="StartOffset"/> and follows it across document changes and
/// scrolling.
/// </summary>
public class CompletionWindowBase
{
    private readonly InputHandler _inputHandler;
    private readonly Popup _popup;
    private TextDocument _document;
    private bool _isOpen;

    public CompletionWindowBase(TextArea textArea)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        TextArea = textArea;
        _document = textArea.Document;
        StartOffset = EndOffset = textArea.Caret.Offset;
        _inputHandler = new InputHandler(this);
        // A bare positioning host: the original base window is styleless and each window carries
        // its own frame - the completion list its list frame, the insight window its tooltip look.
        Root = new Border
        {
            HorizontalAlignment = HorizontalAlignment.Left,
            VerticalAlignment = VerticalAlignment.Top
        };
        Root.WithTheme(static (theme, root) =>
        {
            // The popup resolves inherited values through the editor, whose monospace font would
            // otherwise reach the list.
            root.FontFamily(theme.Metrics.FontFamily).FontSize(theme.Metrics.FontSize);
        });
        // Transient on purpose: a press outside the owner window dismisses it. A press inside the
        // editor counts as owner-related and is left to the caret tracking below, which is what
        // moves the caret first and only then decides the window no longer applies.
        _popup = new Popup { Content = Root };
    }

    /// <summary>The text area the window belongs to.</summary>
    public TextArea TextArea { get; }

    /// <summary>
    /// Start of the text range in which the window stays open. The text from here to the caret is
    /// what selects an entry by typing.
    /// </summary>
    public int StartOffset { get; set; }

    /// <summary>End of the text range in which the window stays open.</summary>
    public int EndOffset { get; set; }

    /// <summary>
    /// Whether the window should expect a single text insertion at the start offset that belongs
    /// before the completion region rather than in it. Reset to false when that insertion occurs.
    /// </summary>
    public bool ExpectInsertionBeforeStart { get; set; }

    /// <summary>Whether the window was placed above the anchor line.</summary>
    protected bool IsUp { get; private set; }

    /// <summary>Where the popup was last placed, in the owner window's coordinates.</summary>
    internal Rect PlacedBounds { get; private set; }

    public bool IsOpen => _isOpen;

    /// <summary>Raised after the window closed, for whichever reason.</summary>
    public event EventHandler? Closed;

    /// <summary>The frame the derived window fills; it is the popup's content.</summary>
    protected internal Border Root { get; }

    protected TextEditor Editor => TextArea.Editor;

    /// <summary>Shows the window anchored at <see cref="StartOffset"/>.</summary>
    public void Show()
    {
        if (_isOpen)
        {
            return;
        }
        _isOpen = true;
        OnShowing();
        AttachEvents();
        _popup.Closed += OnPopupClosed;
        Place();
    }

    /// <summary>
    /// Runs before the window is placed, so a derived window can bring its content up to date with
    /// whatever was configured after construction. Placement measures that content.
    /// </summary>
    protected virtual void OnShowing()
    {
    }

    /// <summary>Closes the window and detaches everything it attached. Closing twice is harmless.</summary>
    public void Close()
    {
        if (!_isOpen)
        {
            return;
        }
        _isOpen = false;
        _popup.Closed -= OnPopupClosed;
        DetachEvents();
        _popup.Close();
        OnClosed(EventArgs.Empty);
    }

    // The core close policy can take the popup down without going through Close (an outside press,
    // or the owner window shutting down), so the window follows its popup rather than the reverse.
    private void OnPopupClosed(object? sender, PopupClosedEventArgs e) => Close();

    protected virtual void OnClosed(EventArgs e) => Closed?.Invoke(this, e);

    /// <summary>
    /// Attaches the window to the text area. The original closes other completion windows of the
    /// same type first, so at most one of a kind is open.
    /// </summary>
    protected virtual void AttachEvents()
    {
        _document = TextArea.Document;
        _document.Changed += OnDocumentChangedForOffsets;
        TextArea.SelectionChanged += OnEditingStateChangedForPosition;
        Editor.DocumentChanged += OnEditorDocumentChanged;
        Editor.Surface.LinesChanged += OnSurfaceLinesChanged;

        foreach (var handler in TextArea.StackedInputHandlers.OfType<InputHandler>().ToArray())
        {
            if (handler.Window.GetType() == GetType())
            {
                TextArea.PopStackedInputHandler(handler);
            }
        }
        TextArea.PushStackedInputHandler(_inputHandler);
    }

    protected virtual void DetachEvents()
    {
        _document.Changed -= OnDocumentChangedForOffsets;
        TextArea.SelectionChanged -= OnEditingStateChangedForPosition;
        Editor.DocumentChanged -= OnEditorDocumentChanged;
        Editor.Surface.LinesChanged -= OnSurfaceLinesChanged;
        TextArea.PopStackedInputHandler(_inputHandler);
    }

    /// <summary>
    /// A stacked handler that feeds keys to the window while the editor keeps the focus. Popping
    /// it - by any other handler taking over - closes the window.
    /// </summary>
    private sealed class InputHandler(CompletionWindowBase window) : TextAreaStackedInputHandler(window.TextArea)
    {
        internal CompletionWindowBase Window { get; } = window;

        public override void Detach()
        {
            base.Detach();
            Window.Close();
        }

        public override void OnPreviewKeyDown(KeyEventArgs e) => Window.OnPreviewKeyDown(e);

        public override void OnPreviewKeyUp(KeyEventArgs e) => Window.OnPreviewKeyUp(e);
    }

    /// <summary>Escape closes; a derived window feeds the remaining keys to its content.</summary>
    protected virtual void OnPreviewKeyDown(KeyEventArgs e)
    {
        if (!e.Handled && e.Key == Key.Escape)
        {
            e.Handled = true;
            Close();
        }
    }

    protected virtual void OnPreviewKeyUp(KeyEventArgs e)
    {
    }

    private void OnEditorDocumentChanged(object? sender, EventArgs e) => Close();

    private void OnSurfaceLinesChanged(Aprillz.MewUI.Text.ITextViewHost host)
    {
        // Scrolling far enough that the anchor leaves the viewport closes the window; any other
        // layout change just moves it along with the anchor.
        var viewport = Editor.Surface.TextViewportBounds;
        var anchor = AnchorRect();
        if (anchor.Bottom < viewport.Y || anchor.Y > viewport.Y + viewport.Height)
        {
            Close();
        }
        else
        {
            UpdatePosition();
        }
    }

    private void OnEditingStateChangedForPosition(object? sender, EventArgs e)
    {
        if (_isOpen)
        {
            UpdatePosition();
        }
    }

    private Rect AnchorRect()
    {
        int anchorOffset = Math.Clamp(
            StartOffset != TextArea.Caret.Offset ? StartOffset : TextArea.Caret.Offset,
            0, _document.TextLength);
        return Editor.Surface.GetCharRectInWindow(anchorOffset);
    }

    /// <summary>
    /// Places the window below the anchor line, or above it when there is no room below. The popup
    /// is placed against the monitor work area, so the editor's bounds do not clip it.
    /// </summary>
    protected void UpdatePosition()
    {
        if (_isOpen)
        {
            Place();
        }
    }

    private void Place()
    {
        var anchor = AnchorRect();
        var placed = _popup.IsOpen ? _popup.MoveTo(anchor) : _popup.ShowAt(Editor, anchor);
        PlacedBounds = placed;
        IsUp = placed.Height > 0 && placed.Y < anchor.Y;
        OnPlaced();
    }

    /// <summary>
    /// Runs after every placement, so a derived window can move whatever it hangs off its own
    /// bounds. <see cref="PlacedBounds"/> is current by then.
    /// </summary>
    protected virtual void OnPlaced()
    {
    }

    /// <summary>
    /// Follows document changes: a removal immediately in front of the completion segment closes
    /// the window (backspace after dot-completion), the start stays put before insertions unless
    /// <see cref="ExpectInsertionBeforeStart"/>, and the end rides after them, so typing grows the
    /// completion region.
    /// </summary>
    private void OnDocumentChangedForOffsets(object? sender, DocumentChangeEventArgs e)
    {
        if (e.Offset + e.RemovalLength == StartOffset && e.RemovalLength > 0)
        {
            Close();
        }
        if (e.Offset == StartOffset && e.RemovalLength == 0 && ExpectInsertionBeforeStart)
        {
            StartOffset = e.GetNewOffset(StartOffset, AnchorMovementType.AfterInsertion);
            ExpectInsertionBeforeStart = false;
        }
        else
        {
            StartOffset = e.GetNewOffset(StartOffset, AnchorMovementType.BeforeInsertion);
        }
        EndOffset = e.GetNewOffset(EndOffset, AnchorMovementType.AfterInsertion);
    }
}
