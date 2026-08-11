using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>Represents the context of a snippet insertion.</summary>
public class InsertionContext
{
    private enum Status
    {
        Insertion,
        RaisingInsertionCompleted,
        Interactive,
        RaisingDeactivated,
        Deactivated
    }

    private Status _currentStatus = Status.Insertion;
    private readonly int _startPosition;
    private AnchorSegment? _wholeSnippetAnchor;
    private bool _deactivateIfSnippetEmpty;
    private readonly Dictionary<SnippetElement, IActiveElement> _elementMap = [];
    private readonly List<IActiveElement> _registeredElements = [];
    private SnippetInputHandler? _inputHandler;

    public InsertionContext(TextArea textArea, int insertionPosition)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        TextArea = textArea;
        Document = textArea.Document;
        SelectedText = textArea.Selection.GetText();
        InsertionPosition = insertionPosition;
        _startPosition = insertionPosition;

        var startLine = Document.GetLineByOffset(insertionPosition);
        var indentation = TextUtilities.GetWhitespaceAfter(Document, startLine.Offset);
        Indentation = Document.GetText(
            indentation.Offset, Math.Min(indentation.EndOffset, insertionPosition) - indentation.Offset);
        Tab = textArea.Options.IndentationString;
        LineTerminator = TextUtilities.GetNewLineFromDocument(Document, startLine.LineNumber);
    }

    public TextArea TextArea { get; }

    public TextDocument Document { get; }

    /// <summary>The text that was selected before the insertion of the snippet.</summary>
    public string SelectedText { get; }

    /// <summary>The indentation at the insertion position.</summary>
    public string Indentation { get; }

    /// <summary>The indentation string for a single indentation level.</summary>
    public string Tab { get; }

    /// <summary>The line terminator at the insertion position.</summary>
    public string LineTerminator { get; }

    /// <summary>The insertion position, advanced by every element that inserts.</summary>
    public int InsertionPosition { get; set; }

    /// <summary>The start position of the snippet insertion.</summary>
    public int StartPosition => _wholeSnippetAnchor?.Offset ?? _startPosition;

    /// <summary>Occurs when all snippet elements have been inserted.</summary>
    public event EventHandler? InsertionCompleted;

    /// <summary>Occurs when the interactive mode is deactivated.</summary>
    public event EventHandler<SnippetEventArgs>? Deactivated;

    /// <summary>
    /// Inserts text at the insertion position and advances it. The current indentation is added
    /// to every line and newlines are replaced with the document's expected terminator.
    /// </summary>
    public void InsertText(string text)
    {
        ArgumentNullException.ThrowIfNull(text);
        if (_currentStatus != Status.Insertion)
        {
            throw new InvalidOperationException();
        }

        text = text.Replace("\t", Tab);

        Document.RunUpdate(() =>
        {
            int textOffset = 0;
            int newline;
            while ((newline = NextNewLine(text, textOffset, out int newlineLength)) >= 0)
            {
                string insertString = string.Concat(
                    text.AsSpan(textOffset, newline - textOffset), LineTerminator, Indentation);
                Document.Insert(InsertionPosition, insertString);
                InsertionPosition += insertString.Length;
                textOffset = newline + newlineLength;
            }
            string remaining = text[textOffset..];
            Document.Insert(InsertionPosition, remaining);
            InsertionPosition += remaining.Length;
        });
    }

    private static int NextNewLine(string text, int startIndex, out int length)
    {
        for (int index = startIndex; index < text.Length; index++)
        {
            char character = text[index];
            if (character == '\r')
            {
                length = index + 1 < text.Length && text[index + 1] == '\n' ? 2 : 1;
                return index;
            }
            if (character == '\n')
            {
                length = 1;
                return index;
            }
        }
        length = 0;
        return -1;
    }

    /// <summary>
    /// Registers an active element. Elements register during insertion and are called back when
    /// insertion has completed.
    /// </summary>
    public void RegisterActiveElement(SnippetElement owner, IActiveElement element)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(element);
        if (_currentStatus != Status.Insertion)
        {
            throw new InvalidOperationException();
        }
        _elementMap.Add(owner, element);
        _registeredElements.Add(element);
    }

    /// <summary>The active element created by the snippet element, or null.</summary>
    public IActiveElement? GetActiveElement(SnippetElement owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        return _elementMap.TryGetValue(owner, out var element) ? element : null;
    }

    /// <summary>The registered active elements, in insertion order - which is the Tab order.</summary>
    public IEnumerable<IActiveElement> ActiveElements => _registeredElements;

    /// <summary>
    /// Calls <see cref="IActiveElement.OnInsertionCompleted"/> on all registered elements, raises
    /// <see cref="InsertionCompleted"/>, and enters interactive mode when there is anything to
    /// interact with.
    /// </summary>
    public void RaiseInsertionCompleted(EventArgs? e)
    {
        if (_currentStatus != Status.Insertion)
        {
            throw new InvalidOperationException();
        }
        e ??= EventArgs.Empty;

        _currentStatus = Status.RaisingInsertionCompleted;
        int endPosition = InsertionPosition;
        _wholeSnippetAnchor = new AnchorSegment(Document, _startPosition, endPosition - _startPosition);
        // The original listens for the end of a document update batch; the port's TextChanged
        // fires per change, so a batch that empties and refills the snippet region could
        // deactivate early. Accepted: such batches are rare, and undo still exits cleanly.
        Document.TextChanged += OnDocumentTextChanged;
        // A snippet that was empty to begin with must not count as deleted.
        _deactivateIfSnippetEmpty = endPosition != _startPosition;

        foreach (var element in _registeredElements)
        {
            element.OnInsertionCompleted();
        }
        InsertionCompleted?.Invoke(this, e);
        _currentStatus = Status.Interactive;
        if (_registeredElements.Count == 0)
        {
            // Deactivate immediately if there are no interactive elements.
            Deactivate(new SnippetEventArgs(DeactivateReason.NoActiveElements));
        }
        else
        {
            _inputHandler = new SnippetInputHandler(this);
            // Disable existing snippet input handlers - there can be only one active snippet.
            foreach (var handler in TextArea.StackedInputHandlers.OfType<SnippetInputHandler>().ToArray())
            {
                TextArea.PopStackedInputHandler(handler);
            }
            TextArea.PushStackedInputHandler(_inputHandler);
        }
    }

    /// <summary>Calls <see cref="IActiveElement.Deactivate"/> on all registered elements.</summary>
    public void Deactivate(SnippetEventArgs? e)
    {
        if (_currentStatus == Status.Deactivated || _currentStatus == Status.RaisingDeactivated)
        {
            return;
        }
        if (_currentStatus != Status.Interactive)
        {
            throw new InvalidOperationException("Cannot call Deactivate() until RaiseInsertionCompleted() has finished.");
        }
        e ??= new SnippetEventArgs(DeactivateReason.Unknown);

        Document.TextChanged -= OnDocumentTextChanged;
        _currentStatus = Status.RaisingDeactivated;
        if (_inputHandler is SnippetInputHandler handler)
        {
            TextArea.PopStackedInputHandler(handler);
        }
        foreach (var element in _registeredElements)
        {
            element.Deactivate(e);
        }
        Deactivated?.Invoke(this, e);
        _currentStatus = Status.Deactivated;
    }

    private void OnDocumentTextChanged(object? sender, EventArgs e)
    {
        // Deactivate if the snippet is deleted. This is what leaves interactive mode correctly
        // when Undo is pressed after a snippet insertion.
        if (_wholeSnippetAnchor is AnchorSegment anchor && anchor.Length == 0 && _deactivateIfSnippetEmpty)
        {
            Deactivate(new SnippetEventArgs(DeactivateReason.Deleted));
        }
    }

    /// <summary>Adds existing segments as snippet elements, for rename-style linked editing.</summary>
    public void Link(ISegment mainElement, ISegment[] boundElements)
    {
        ArgumentNullException.ThrowIfNull(mainElement);
        ArgumentNullException.ThrowIfNull(boundElements);
        var main = new SnippetReplaceableTextElement { Text = Document.GetText(mainElement) };
        RegisterActiveElement(main, new ReplaceableActiveElement(this, mainElement.Offset, mainElement.EndOffset));
        foreach (var boundElement in boundElements)
        {
            var bound = new SnippetBoundElement { TargetElement = main };
            var start = Document.CreateAnchor(boundElement.Offset);
            start.MovementType = AnchorMovementType.BeforeInsertion;
            start.SurviveDeletion = true;
            var end = Document.CreateAnchor(boundElement.EndOffset);
            end.MovementType = AnchorMovementType.BeforeInsertion;
            end.SurviveDeletion = true;
            RegisterActiveElement(bound, new BoundActiveElement(this, main, bound, new AnchorSegment(start, end)));
        }
    }
}
