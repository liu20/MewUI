using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Text.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Document;

/// <summary>
/// The document's undo and redo history. Grouping is what it is mostly used for: a routine that
/// edits line by line, such as indenting a block, would otherwise cost one undo step per line.
/// </summary>
/// <remarks>
/// The original raises <c>PropertyChanged</c> for <c>CanUndo</c> and <c>CanRedo</c>. MewUI has no
/// such interface, so each is its own event. The document's own change event is no substitute: it
/// runs while the edit is still being recorded, so the history has not changed yet when it fires.
/// </remarks>
public sealed class UndoStack
{
    private readonly EditableTextDocument _document;
    private IDisposable? _group;
    private int _groupDepth;
    private bool _lastCanUndo;
    private bool _lastCanRedo;
    // Undo steps between here and the state marked as original. Negative puts the marker in the
    // redo direction; int.MinValue means it can no longer be reached, which is what a new edit made
    // after undoing past it does, since that edit throws the redo branch away.
    private int _stepsToOriginal;
    private bool _isOriginalFile = true;
    private bool _replaying;
    private bool _countedThisGroup;

    internal UndoStack(TextDocument document) => _document = document.CoreDocument;

    /// <summary>Whether there is an edit to undo.</summary>
    public bool CanUndo => _document.CanUndo;

    /// <summary>Whether there is an undone edit to redo.</summary>
    public bool CanRedo => _document.CanRedo;

    /// <summary>Raised after <see cref="CanUndo"/> changed.</summary>
    public event EventHandler? CanUndoChanged;

    /// <summary>Raised after <see cref="CanRedo"/> changed.</summary>
    public event EventHandler? CanRedoChanged;

    /// <summary>Raised after <see cref="SizeLimit"/> changed.</summary>
    public event EventHandler? SizeLimitChanged;

    /// <summary>
    /// Whether the document is in the state last marked as original. A file view shows its dirty
    /// marker off this, and undoing back to the marked state turns it on again.
    /// </summary>
    public bool IsOriginalFile => _isOriginalFile;

    /// <summary>Raised after <see cref="IsOriginalFile"/> changed.</summary>
    public event EventHandler? IsOriginalFileChanged;

    /// <summary>Marks the current state as original, discarding any previous marker.</summary>
    public void MarkAsOriginalFile()
    {
        _stepsToOriginal = 0;
        RecalculateIsOriginalFile();
    }

    /// <summary>Drops the marker, so no state counts as original until one is marked again.</summary>
    public void DiscardOriginalFileMarker()
    {
        _stepsToOriginal = int.MinValue;
        RecalculateIsOriginalFile();
    }

    private void RecalculateIsOriginalFile()
    {
        bool isOriginal = _stepsToOriginal == 0;
        if (isOriginal == _isOriginalFile)
        {
            return;
        }
        _isOriginalFile = isOriginal;
        IsOriginalFileChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Counts an edit that reached the document. Called for every change, so it ignores the ones an
    /// undo or a redo applied, and counts a group once however many edits it holds: a group is one
    /// step, and undoing it once has to reach the state before it.
    /// </summary>
    internal void NotifyDocumentChanged()
    {
        if (_replaying)
        {
            return;
        }
        if (IsInUndoGroup)
        {
            if (_countedThisGroup)
            {
                return;
            }
            _countedThisGroup = true;
        }
        if (_stepsToOriginal < 0)
        {
            // The marker sat in the redo branch this edit just threw away.
            _stepsToOriginal = int.MinValue;
        }
        else if (_stepsToOriginal != int.MinValue)
        {
            _stepsToOriginal++;
        }
        RecalculateIsOriginalFile();
    }

    /// <summary>
    /// Raises the events whose value changed. Called once an edit, an undo or a redo has finished
    /// recording, which is the first moment the history answers for what just happened.
    /// </summary>
    internal void NotifyHistoryChanged()
    {
        bool canUndo = _document.CanUndo;
        if (canUndo != _lastCanUndo)
        {
            _lastCanUndo = canUndo;
            CanUndoChanged?.Invoke(this, EventArgs.Empty);
        }
        bool canRedo = _document.CanRedo;
        if (canRedo != _lastCanRedo)
        {
            _lastCanRedo = canRedo;
            CanRedoChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>
    /// Most recent edits to keep, oldest dropped past it. Negative keeps every edit; zero records
    /// none. A group counts as the edits it holds, and the limit never leaves half of one behind.
    /// </summary>
    public int SizeLimit
    {
        get => _document.UndoSizeLimit;
        set
        {
            if (_document.UndoSizeLimit == value)
            {
                return;
            }
            _document.UndoSizeLimit = value;
            SizeLimitChanged?.Invoke(this, EventArgs.Empty);
            NotifyHistoryChanged();
        }
    }

    /// <summary>Whether a group is open, which is when an edit joins the one before it.</summary>
    public bool IsInUndoGroup => _groupDepth > 0;

    /// <summary>
    /// Opens a group; every edit until the matching <see cref="EndUndoGroup"/> undoes as one step.
    /// Nesting extends the outermost group rather than starting another.
    /// </summary>
    public void StartUndoGroup()
    {
        if (_groupDepth++ == 0)
        {
            _group = _document.BeginUndoGroup();
        }
    }

    /// <summary>Closes the group opened by <see cref="StartUndoGroup"/>.</summary>
    /// <exception cref="InvalidOperationException">No group is open.</exception>
    public void EndUndoGroup()
    {
        if (_groupDepth == 0)
        {
            throw new InvalidOperationException("No undo group is open.");
        }
        if (--_groupDepth == 0)
        {
            _group?.Dispose();
            _group = null;
            _countedThisGroup = false;
        }
    }

    /// <summary>
    /// A group that closes when the returned object is disposed, which is the shape a caller wants
    /// when the edits are made inside one scope.
    /// </summary>
    public IDisposable OpenUndoGroup()
    {
        StartUndoGroup();
        return new GroupScope(this);
    }

    /// <summary>Undoes one step. False when there was nothing to undo.</summary>
    public bool Undo() => Replay(_document.Undo, -1);

    /// <summary>Redoes one step. False when there was nothing to redo.</summary>
    public bool Redo() => Replay(_document.Redo, 1);

    private bool Replay(Func<bool> replay, int steps)
    {
        _replaying = true;
        bool moved;
        try
        {
            moved = replay();
        }
        finally
        {
            _replaying = false;
        }
        if (moved && _stepsToOriginal != int.MinValue)
        {
            _stepsToOriginal += steps;
            RecalculateIsOriginalFile();
        }
        NotifyHistoryChanged();
        return moved;
    }

    /// <summary>
    /// Drops both stacks, so what is in the document becomes the starting point. The original
    /// marker goes with them: no step remains that could reach it.
    /// </summary>
    public void ClearAll()
    {
        _document.ClearUndoHistory();
        if (_stepsToOriginal != 0)
        {
            _stepsToOriginal = int.MinValue;
            RecalculateIsOriginalFile();
        }
        NotifyHistoryChanged();
    }

    private sealed class GroupScope(UndoStack stack) : IDisposable
    {
        private bool _ended;

        public void Dispose()
        {
            if (_ended)
            {
                return;
            }
            _ended = true;
            stack.EndUndoGroup();
        }
    }
}
