using Aprillz.MewUI.Controls;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;
using Aprillz.MewUI.Text.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Document;

public sealed class TextDocument : ITextSource
{
    private readonly EditableTextDocument _document;
    private readonly List<WeakReference<TextAnchor>> _anchors = [];
    private TextSourceVersion _version;
    private ServiceContainer? _services;
    private UndoStack? _undoStack;
    // Records edits made with no editor attached. Created on first such edit, so a document that
    // only ever backs an editor never builds one.
    private TextEditorSession? _session;
    private string? _fileName;
    private int _lastTextLength;
    private int _lastLineCount;

    public TextDocument()
        : this(string.Empty)
    {
    }

    public TextDocument(string? text)
    {
        // Terminators are kept so a file read into the editor comes back out unchanged.
        _document = EditableTextDocument.CreatePreservingLineEndings(text);
        _version = new TextSourceVersion(this, 0);
        _document.Changed += OnChanged;
        CaptureCounts();
    }

    public TextDocument(IEnumerable<char> initialText)
        : this(initialText is null ? null : string.Concat(initialText))
    {
    }

    internal TextDocument(EditableTextDocument document)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _version = new TextSourceVersion(this, 0);
        _document.Changed += OnChanged;
        CaptureCounts();
    }

    private void CaptureCounts()
    {
        _lastTextLength = _document.TextLength;
        _lastLineCount = _document.LineCount;
    }

    internal EditableTextDocument CoreDocument => _document;

    /// <summary>
    /// The whole text. Assigning replaces it through the ordinary edit path, so the replacement is
    /// undoable as in the original, where the setter is a <c>Replace</c> over the document.
    /// </summary>
    public string Text
    {
        get => _document.ToString();
        set => Replace(0, TextLength, value ?? string.Empty);
    }

    public int TextLength => _document.TextLength;
    public int LineCount => _document.LineCount;

    /// <summary>
    /// Checkpoint of the current text. Hold one to carry offsets across later edits; the value the
    /// engine uses to invalidate caches is <see cref="CoreDocument"/>'s own counter.
    /// </summary>
    public ITextSourceVersion Version => _version;

    /// <summary>
    /// Services this document carries. A view that cannot find a service of its own falls through to
    /// here, so a service registered on the document follows it into every view showing it. The
    /// document registers itself.
    /// </summary>
    /// <summary>
    /// The undo and redo history. It belongs to the document rather than to an editor, so several
    /// editors over one document share it.
    /// </summary>
    public UndoStack UndoStack => _undoStack ??= new UndoStack(this);

    /// <summary>Runs the action with its edits grouped into one undo step.</summary>
    public void RunUpdate(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        using var group = UndoStack.OpenUndoGroup();
        action();
    }

    /// <summary>Opens an undo group. Pairs with <see cref="EndUpdate"/>.</summary>
    public void BeginUpdate() => UndoStack.StartUndoGroup();

    /// <summary>Closes the group opened by <see cref="BeginUpdate"/>.</summary>
    public void EndUpdate() => UndoStack.EndUndoGroup();

    /// <summary>Whether an update is open, which is when an edit joins the one before it.</summary>
    public bool IsInUpdate => UndoStack.IsInUndoGroup;

    public ServiceContainer Services
    {
        get
        {
            if (_services is null)
            {
                _services = new ServiceContainer();
                _services.AddService(this);
            }
            return _services;
        }
    }

    /// <summary>An unchanging copy of the whole text.</summary>
    public ITextSource CreateSnapshot() => new StringTextSource(Text);

    /// <summary>An unchanging copy of one range.</summary>
    public ITextSource CreateSnapshot(int offset, int length) => new StringTextSource(GetText(offset, length));

    public event EventHandler<DocumentChangeEventArgs>? Changed;
    public event EventHandler? TextChanged;

    /// <summary>Raised after a change that moved <see cref="TextLength"/>.</summary>
    public event EventHandler? TextLengthChanged;

    /// <summary>Raised after a change that moved <see cref="LineCount"/>.</summary>
    public event EventHandler? LineCountChanged;

    /// <summary>Raised after <see cref="FileName"/> was assigned a different value.</summary>
    public event EventHandler? FileNameChanged;

    /// <summary>Name of the file this document holds. The document itself never reads it.</summary>
    public string? FileName
    {
        get => _fileName;
        set
        {
            if (_fileName == value)
            {
                return;
            }
            _fileName = value;
            FileNameChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public char GetCharAt(int offset) => _document.GetCharAt(offset);
    public string GetText(int offset, int length) => _document.GetText(offset, length);

    public int IndexOf(char value, int startIndex, int count)
        => TextSourceSearch.IndexOf(this, value, startIndex, count);

    public int LastIndexOf(char value, int startIndex, int count)
        => TextSourceSearch.LastIndexOf(this, value, startIndex, count);

    public int IndexOfAny(char[] anyOf, int startIndex, int count)
        => TextSourceSearch.IndexOfAny(this, anyOf, startIndex, count);

    public int IndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
        => TextSourceSearch.IndexOf(this, searchText, startIndex, count, comparisonType);

    public int LastIndexOf(string searchText, int startIndex, int count, StringComparison comparisonType)
        => TextSourceSearch.LastIndexOf(this, searchText, startIndex, count, comparisonType);

    public string GetText(ISegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        return GetText(segment.Offset, segment.Length);
    }

    /// <summary>
    /// Surface this document is shown in, once an editor adopts it. Edits route through it so they
    /// join the undo history; editing the core document directly is unrecorded and drops that history.
    /// </summary>
    internal MultiLineTextBox? Surface { get; set; }

    public void Insert(int offset, string text) => Replace(offset, 0, text);
    public void Remove(int offset, int length) => Replace(offset, length, string.Empty);

    public void Replace(int offset, int length, string? text)
    {
        if (Surface is MultiLineTextBox surface)
        {
            surface.ReplaceRange(offset, length, text);
        }
        else
        {
            // Through a session even with no editor attached: an edit applied to the document
            // directly is unrecorded, and the core drops the whole history when it sees one, so a
            // document edited on its own would lose everything undoable rather than gain a step.
            (_session ??= new TextEditorSession(_document)).ReplaceRange(offset, length, text);
        }
        _undoStack?.NotifyHistoryChanged();
    }

    /// <summary>
    /// Tells the stack that an edit finished recording. The editor calls it after its own editing
    /// state settled, which covers typing and everything else that never goes through
    /// <see cref="Replace(int, int, string)"/>.
    /// </summary>
    internal void NotifyUndoHistoryChanged() => _undoStack?.NotifyHistoryChanged();

    /// <summary>
    /// Counts a change against the original-file marker. Every change reaches it, including the
    /// ones an undo applied, which the stack tells apart itself.
    /// </summary>
    // Through the property, not the field: the stack is created on first use, and counting only
    // once someone happened to touch it would let the edits before that go unrecorded, leaving
    // IsModified false on a document that had already been edited.
    private void CountAgainstOriginalFile() => UndoStack.NotifyDocumentChanged();
    public void Replace(ISegment segment, string? text)
    {
        ArgumentNullException.ThrowIfNull(segment);
        Replace(segment.Offset, segment.Length, text);
    }

    public DocumentLine GetLineByNumber(int lineNumber)
    {
        if (lineNumber <= 0 || lineNumber > LineCount)
        {
            throw new ArgumentOutOfRangeException(nameof(lineNumber));
        }
        return new DocumentLine(this, lineNumber);
    }

    public DocumentLine GetLineByOffset(int offset)
        => new(this, _document.GetLineByOffset(offset).LineNumber + 1);

    /// <summary>
    /// Offset of a one-based line and column. A column outside the line clamps to its start or end,
    /// as in the original; only the line number is validated.
    /// </summary>
    public int GetOffset(int line, int column)
    {
        var documentLine = GetLineByNumber(line);
        if (column <= 0)
        {
            return documentLine.Offset;
        }
        if (column > documentLine.Length)
        {
            return documentLine.EndOffset;
        }
        return documentLine.Offset + column - 1;
    }

    public TextLocation GetLocation(int offset)
    {
        var location = _document.GetLocation(offset);
        return new TextLocation(location.Line + 1, location.Column + 1);
    }

    /// <summary>The document's lines. Read-only, as in the original; mutation throws.</summary>
    public IList<DocumentLine> Lines
        => Enumerable.Range(1, _document.LineCount)
            .Select(lineNumber => new DocumentLine(this, lineNumber))
            .ToArray();

    public override string ToString() => Text;

    /// <summary>
    /// An anchor at <paramref name="offset"/> that moves with the text around it. Anchors are held
    /// weakly, so one nobody keeps a reference to costs the document nothing.
    /// </summary>
    public TextAnchor CreateAnchor(int offset)
    {
        if (offset < 0 || offset > TextLength)
        {
            throw new ArgumentOutOfRangeException(nameof(offset));
        }
        var anchor = new TextAnchor(this, offset);
        _anchors.Add(new WeakReference<TextAnchor>(anchor));
        return anchor;
    }

    private void UpdateAnchors(in OffsetChangeMapEntry change)
    {
        for (int index = _anchors.Count - 1; index >= 0; index--)
        {
            if (_anchors[index].TryGetTarget(out var anchor))
            {
                anchor.Update(in change);
            }
            else
            {
                _anchors.RemoveAt(index);
            }
        }
    }

    private void OnChanged(TextChange change)
    {
        var entry = new OffsetChangeMapEntry(change.Offset, change.RemovedLength, change.InsertedLength);
        UpdateAnchors(in entry);
        _version = _version.Append(entry);
        CountAgainstOriginalFile();
        Changed?.Invoke(this, new DocumentChangeEventArgs(
            change.Offset,
            change.RemovedText,
            _document.GetText(change.Offset, change.InsertedLength),
            change.RemovedLength));
        TextChanged?.Invoke(this, EventArgs.Empty);
        RaiseCountsChanged();
    }

    /// <summary>Each count reports only when it moved, so a same-length replace is quiet.</summary>
    private void RaiseCountsChanged()
    {
        if (_lastTextLength != TextLength)
        {
            _lastTextLength = TextLength;
            TextLengthChanged?.Invoke(this, EventArgs.Empty);
        }
        if (_lastLineCount != LineCount)
        {
            _lastLineCount = LineCount;
            LineCountChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
