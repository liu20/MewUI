using System.Collections.Immutable;
using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <summary>Signature of <see cref="IHighlighter.HighlightingStateChanged"/>.</summary>
public delegate void HighlightingStateChangedEventHandler(int fromLineNumber, int toLineNumber);

/// <summary>A document that can be highlighted line by line.</summary>
/// <remarks>
/// The colorizer registers the highlighter as a view service under this interface, so a host can
/// replace regex highlighting with its own without touching the rendering side.
/// </remarks>
public interface IHighlighter : IDisposable
{
    TextDocument Document { get; }

    /// <summary>
    /// The colours of the spans open at the end of <paramref name="lineNumber"/>, innermost first.
    /// Line 0 is valid and yields nothing.
    /// </summary>
    IEnumerable<HighlightingColor> GetColorStack(int lineNumber);

    HighlightedLine HighlightLine(int lineNumber);

    /// <summary>Brings the highlighting state up to date through <paramref name="lineNumber"/>.</summary>
    void UpdateHighlightingState(int lineNumber);

    /// <summary>
    /// Raised for the lines whose starting state changed, both bounds inclusive. Highlighting line
    /// X raises it for X+1 when the state at the end of X is no longer what it was.
    /// </summary>
    /// <remarks>
    /// Implementers must hold to: equal input state plus unchanged line text gives equal output
    /// state. The colorizer redraws on this event, and would loop if it fired for an unchanged line.
    /// </remarks>
    event HighlightingStateChangedEventHandler? HighlightingStateChanged;

    /// <summary>Opens a group of <see cref="HighlightLine"/> calls. Groups do not nest.</summary>
    void BeginHighlighting();

    /// <summary>Closes the group opened by <see cref="BeginHighlighting"/>.</summary>
    void EndHighlighting();

    HighlightingColor? GetNamedColor(string name);

    /// <summary>Colour for text no rule matched, or null to leave it to the view.</summary>
    HighlightingColor? DefaultTextColor { get; }
}

/// <summary>
/// Highlights a whole document, invalidating itself as the document changes. The span stack at the
/// end of each line is stored, so a line is rescanned only when the state it starts from changed.
/// </summary>
public class DocumentHighlighter : IHighlighter
{
    // Index 0 is the state at the start of the document; index i is the state after line i. A null
    // entry means the state is unknown and the line has to be scanned again.
    private readonly List<ImmutableStack<HighlightingSpan>?> _storedSpanStacks = [];
    private readonly List<bool> _isValid = [];

    private readonly TextDocument _document;
    private readonly IHighlightingDefinition _definition;
    private readonly HighlightingEngine _engine;

    private ImmutableStack<HighlightingSpan> _initialSpanStack = ImmutableStack<HighlightingSpan>.Empty;
    private int _firstInvalidLine;
    private bool _isHighlighting;
    private int _pendingStateChangeFrom;
    private int _pendingStateChangeTo;
    private bool _isInHighlightingGroup;
    private bool _isDisposed;

    public DocumentHighlighter(TextDocument document, IHighlightingDefinition definition)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _engine = new HighlightingEngine(definition.MainRuleSet);
        _document.Changed += OnDocumentChanged;
        InvalidateSpanStacks();
    }

    public TextDocument Document => _document;

    /// <inheritdoc/>
    public event HighlightingStateChangedEventHandler? HighlightingStateChanged;

    /// <summary>
    /// State the first line starts from. A host embedding one language in another sets it so the
    /// fragment is highlighted as if it sat inside the enclosing span.
    /// </summary>
    public ImmutableStack<HighlightingSpan> InitialSpanStack
    {
        get => _initialSpanStack;
        set
        {
            _initialSpanStack = value ?? ImmutableStack<HighlightingSpan>.Empty;
            InvalidateHighlighting();
        }
    }

    /// <inheritdoc/>
    public HighlightingColor? DefaultTextColor => null;

    public void Dispose()
    {
        _document.Changed -= OnDocumentChanged;
        _isDisposed = true;
        GC.SuppressFinalize(this);
    }

    /// <summary>
    /// Drops all stored highlighting state and asks for a redraw. Document changes do this on their
    /// own; call it after the rule set itself changed.
    /// </summary>
    public void InvalidateHighlighting()
    {
        InvalidateSpanStacks();
        RaiseHighlightingStateChanged(1, _document.LineCount);
    }

    /// <summary>
    /// Raises <see cref="HighlightingStateChanged"/>, holding it back while a highlighting
    /// operation runs: a listener's repaint rebuilds lines synchronously, and rebuilding would
    /// re-enter the highlighter. Held notifications merge and fire when the operation completes.
    /// </summary>
    private void RaiseHighlightingStateChanged(int fromLineNumber, int toLineNumber)
    {
        if (_isHighlighting)
        {
            if (_pendingStateChangeTo == 0)
            {
                _pendingStateChangeFrom = fromLineNumber;
                _pendingStateChangeTo = toLineNumber;
            }
            else
            {
                _pendingStateChangeFrom = Math.Min(_pendingStateChangeFrom, fromLineNumber);
                _pendingStateChangeTo = Math.Max(_pendingStateChangeTo, toLineNumber);
            }
            return;
        }
        HighlightingStateChanged?.Invoke(fromLineNumber, toLineNumber);
    }

    private void FlushPendingStateChanged()
    {
        while (_pendingStateChangeTo != 0 && !_isHighlighting)
        {
            int fromLineNumber = _pendingStateChangeFrom;
            int toLineNumber = _pendingStateChangeTo;
            _pendingStateChangeTo = 0;
            HighlightingStateChanged?.Invoke(fromLineNumber, toLineNumber);
        }
    }

    /// <inheritdoc/>
    public HighlightedLine HighlightLine(int lineNumber)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(lineNumber, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lineNumber, _document.LineCount);
        CheckIsHighlighting();
        _isHighlighting = true;
        try
        {
            HighlightUpTo(lineNumber - 1);
            var line = _document.GetLineByNumber(lineNumber);
            var result = _engine.HighlightLine(_document, line);
            UpdateStoredState(lineNumber);
            return result;
        }
        finally
        {
            _isHighlighting = false;
            FlushPendingStateChanged();
        }
    }

    /// <summary>
    /// The spans open at the end of <paramref name="lineNumber"/>, innermost first. Line 0 is valid
    /// and yields <see cref="InitialSpanStack"/>.
    /// </summary>
    public ImmutableStack<HighlightingSpan> GetSpanStack(int lineNumber)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(lineNumber);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(lineNumber, _document.LineCount);
        if (_firstInvalidLine <= lineNumber)
        {
            UpdateHighlightingState(lineNumber);
        }
        return _storedSpanStacks[lineNumber] ?? ImmutableStack<HighlightingSpan>.Empty;
    }

    /// <inheritdoc/>
    public IEnumerable<HighlightingColor> GetColorStack(int lineNumber)
        => GetSpanStack(lineNumber).Select(span => span.SpanColor).OfType<HighlightingColor>();

    /// <inheritdoc/>
    public void UpdateHighlightingState(int lineNumber)
    {
        CheckIsHighlighting();
        _isHighlighting = true;
        try
        {
            HighlightUpTo(lineNumber);
        }
        finally
        {
            _isHighlighting = false;
            FlushPendingStateChanged();
        }
    }

    /// <inheritdoc/>
    public void BeginHighlighting()
    {
        if (_isInHighlightingGroup)
        {
            throw new InvalidOperationException("Highlighting group is already open");
        }
        _isInHighlightingGroup = true;
    }

    /// <inheritdoc/>
    public void EndHighlighting()
    {
        if (!_isInHighlightingGroup)
        {
            throw new InvalidOperationException("Highlighting group is not open");
        }
        _isInHighlightingGroup = false;
    }

    /// <inheritdoc/>
    public HighlightingColor? GetNamedColor(string name) => _definition.GetNamedColor(name);

    private void CheckIsHighlighting()
    {
        ObjectDisposedException.ThrowIf(_isDisposed, this);
        if (_isHighlighting)
        {
            throw new InvalidOperationException("Invalid call - a highlighting operation is currently running.");
        }
    }

    private void InvalidateSpanStacks()
    {
        CheckIsHighlighting();
        _storedSpanStacks.Clear();
        _isValid.Clear();
        _storedSpanStacks.Add(_initialSpanStack);
        _isValid.Add(true);
        for (int line = 1; line <= _document.LineCount; line++)
        {
            _storedSpanStacks.Add(null);
            _isValid.Add(false);
        }
        _firstInvalidLine = 1;
    }

    /// <summary>Scans forward until the state after <paramref name="targetLineNumber"/> is known.</summary>
    private void HighlightUpTo(int targetLineNumber)
    {
        for (int currentLine = 0; currentLine <= targetLineNumber; currentLine++)
        {
            if (_firstInvalidLine > currentLine)
            {
                if (_firstInvalidLine <= targetLineNumber)
                {
                    // Skip the valid lines and resume at the first one that is not.
                    _engine.CurrentSpanStack = _storedSpanStacks[_firstInvalidLine - 1] ?? _initialSpanStack;
                    currentLine = _firstInvalidLine;
                }
                else
                {
                    _engine.CurrentSpanStack = _storedSpanStacks[targetLineNumber] ?? _initialSpanStack;
                    break;
                }
            }
            _engine.ScanLine(_document, _document.GetLineByNumber(currentLine));
            UpdateStoredState(currentLine);
        }
    }

    private void UpdateStoredState(int lineNumber)
    {
        if (!EqualSpanStacks(_engine.CurrentSpanStack, _storedSpanStacks[lineNumber]))
        {
            _isValid[lineNumber] = true;
            _storedSpanStacks[lineNumber] = _engine.CurrentSpanStack;
            if (lineNumber + 1 < _isValid.Count)
            {
                _isValid[lineNumber + 1] = false;
                _firstInvalidLine = lineNumber + 1;
            }
            else
            {
                _firstInvalidLine = int.MaxValue;
            }
            if (lineNumber + 1 <= _document.LineCount)
            {
                RaiseHighlightingStateChanged(lineNumber + 1, lineNumber + 1);
            }
        }
        else if (_firstInvalidLine == lineNumber)
        {
            _isValid[lineNumber] = true;
            int next = _isValid.IndexOf(false);
            _firstInvalidLine = next < 0 ? int.MaxValue : next;
        }
    }

    /// <summary>
    /// Compares stacks by value. The colorizer relies on equal input state plus unchanged text
    /// giving equal output state, which reference equality alone would not deliver.
    /// </summary>
    private static bool EqualSpanStacks(
        ImmutableStack<HighlightingSpan>? left,
        ImmutableStack<HighlightingSpan>? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (left is null || right is null)
        {
            return false;
        }
        while (!left.IsEmpty && !right.IsEmpty)
        {
            if (!ReferenceEquals(left.Peek(), right.Peek()))
            {
                return false;
            }
            left = left.Pop();
            right = right.Pop();
            if (ReferenceEquals(left, right))
            {
                return true;
            }
        }
        return left.IsEmpty && right.IsEmpty;
    }

    /// <summary>
    /// Discards the stored state from the changed line onwards. The lines after an insertion or a
    /// removal shift, so their stored state no longer belongs to them; leaving it in place would
    /// let a shifted stack compare equal and swallow the redraw the view needs.
    /// </summary>
    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        int firstAffected = _document.GetLocation(Math.Min(e.Offset, _document.TextLength)).Line;
        int lineCount = _document.LineCount;
        while (_storedSpanStacks.Count > lineCount + 1)
        {
            _storedSpanStacks.RemoveAt(_storedSpanStacks.Count - 1);
            _isValid.RemoveAt(_isValid.Count - 1);
        }
        while (_storedSpanStacks.Count < lineCount + 1)
        {
            _storedSpanStacks.Add(null);
            _isValid.Add(false);
        }
        for (int line = firstAffected; line < _storedSpanStacks.Count; line++)
        {
            _storedSpanStacks[line] = null;
            _isValid[line] = false;
        }
        _firstInvalidLine = Math.Min(_firstInvalidLine, firstAffected);
    }
}
