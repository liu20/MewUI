using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.Text;
using System.Collections.ObjectModel;

namespace Aprillz.MewUI.MewvalonEdit.Folding;

/// <summary>
/// Hides the logical lines a fold element on an earlier line already covers. AvalonEdit keeps that
/// in a <c>CollapsedLineSection</c> inside its height tree; this port answers the question per
/// layout instead, so there is nothing to store or revalidate.
/// </summary>
internal sealed class FoldedLineCollapser(FoldingManager manager) : ITextLineCollapser
{
    public bool IsCollapsed(LogicalTextLine line) => manager.IsLineCollapsed(line);
}

/// <summary>Stores a list of foldings for a specific TextView and TextDocument.</summary>
public sealed class FoldingManager
{
    private readonly TextEditor _editor;
    private readonly TextSegmentCollection<FoldingSection> _foldings;
    private readonly FoldingMargin _margin;
    private readonly FoldingElementGenerator _generator;
    private readonly FoldedLineCollapser _collapser;
    private readonly List<(int Start, int End)> _collapsedRanges = [];
    private bool _isFirstUpdate = true;
    private bool _uninstalled;
    // AvalonEdit's Redraw only marks a view dirty; ours rebuilds it, so a batch that touches every
    // section has to announce itself once rather than per section.
    private int _batchDepth;
    private bool _batchChanged;

    private FoldingManager(TextEditor editor)
    {
        _editor = editor;
        _foldings = new TextSegmentCollection<FoldingSection>(editor.Document);
        editor.Document.Changed += OnDocumentChanged;
        _margin = new FoldingMargin { FoldingManager = this };
        _generator = new FoldingElementGenerator { FoldingManager = this };
        _collapser = new FoldedLineCollapser(this);
        editor.TextArea.LeftMargins.Add(_margin);
        editor.TextArea.TextView.Services.AddService(this);
        editor.TextArea.TextView.ElementGenerators.Insert(0, _generator);
        editor.Surface.Extensions.LineCollapsers.Add(_collapser);
        editor.TextArea.Caret.PositionChanged += OnCaretPositionChanged;
    }

    internal TextDocument Document => _editor.Document;

    /// <summary>
    /// All foldings in this manager, sorted by start offset; for multiple foldings at the same
    /// offset the order is undefined.
    /// </summary>
    public IEnumerable<FoldingSection> AllFoldings => _foldings;

    /// <summary>Raised after the set of foldings or their folded state changed.</summary>
    public event EventHandler? FoldingsChanged;

    /// <summary>
    /// Adds folding support to the editor. The manager is only valid for the editor's current
    /// document and must be uninstalled before the editor is bound to another one.
    /// </summary>
    public static FoldingManager Install(TextEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        return new FoldingManager(editor);
    }

    /// <inheritdoc cref="Install(TextEditor)"/>
    public static FoldingManager Install(Editing.TextArea textArea)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        return Install(textArea.Editor);
    }

    public static void Uninstall(FoldingManager manager)
    {
        ArgumentNullException.ThrowIfNull(manager);
        if (manager._uninstalled) return;
        manager.Clear();
        manager._uninstalled = true;
        manager._editor.Document.Changed -= manager.OnDocumentChanged;
        manager._editor.TextArea.Caret.PositionChanged -= manager.OnCaretPositionChanged;
        manager._editor.TextArea.LeftMargins.Remove(manager._margin);
        manager._editor.TextArea.TextView.ElementGenerators.Remove(manager._generator);
        manager._editor.Surface.Extensions.LineCollapsers.Remove(manager._collapser);
        manager._editor.TextArea.TextView.Services.RemoveService<FoldingManager>();
        manager._margin.FoldingManager = null;
        manager._generator.FoldingManager = null;
        manager._editor.InvalidateTextView();
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        // The segment collection has already moved the offsets.
        int newEndOffset = e.Offset + e.InsertionLength;
        // extend end offset to the end of the line (including delimiter)
        var endLine = _editor.Document.GetLineByOffset(
            Math.Clamp(newEndOffset, 0, _editor.Document.TextLength));
        newEndOffset = endLine.Offset + endLine.TotalLength;
        _batchDepth++;
        try
        {
            foreach (var affectedFolding in
                _foldings.FindOverlappingSegments(e.Offset, newEndOffset - e.Offset))
            {
                if (affectedFolding.Length == 0)
                {
                    RemoveFolding(affectedFolding);
                }
            }
        }
        finally
        {
            EndBatch();
        }
    }

    /// <summary>Creates a folding for the specified text section.</summary>
    public FoldingSection CreateFolding(int startOffset, int endOffset)
    {
        if (startOffset >= endOffset)
        {
            throw new ArgumentException("startOffset must be less than endOffset", nameof(startOffset));
        }
        if (startOffset < 0 || endOffset > _editor.Document.TextLength)
        {
            throw new ArgumentException("Folding must be within document boundary", nameof(startOffset));
        }
        var section = new FoldingSection(this, startOffset, endOffset);
        _foldings.Add(section);
        Redraw();
        return section;
    }

    /// <summary>Removes a folding section from this manager.</summary>
    public void RemoveFolding(FoldingSection folding)
    {
        ArgumentNullException.ThrowIfNull(folding);
        folding.IsFolded = false;
        _foldings.Remove(folding);
        Redraw();
    }

    /// <summary>Removes all folding sections.</summary>
    public void Clear()
    {
        _batchDepth++;
        try
        {
            foreach (var section in _foldings)
            {
                section.IsFolded = false;
            }
            _foldings.Clear();
            _batchChanged = true;
        }
        finally
        {
            EndBatch();
        }
    }

    /// <summary>
    /// First offset at or after <paramref name="startOffset"/> where a folded folding starts, or -1
    /// when there is no folding after it.
    /// </summary>
    public int GetNextFoldedFoldingStart(int startOffset)
    {
        for (int index = _foldings.FindFirstIndexWithStartAfter(startOffset); index < _foldings.Count; index++)
        {
            if (_foldings[index].IsFolded)
            {
                return _foldings[index].StartOffset;
            }
        }
        return -1;
    }

    /// <summary>
    /// First folding with a start offset at or after <paramref name="startOffset"/>, or null when
    /// there is no folding after it.
    /// </summary>
    public FoldingSection? GetNextFolding(int startOffset)
        => _foldings.FindFirstSegmentWithStartAfter(startOffset);

    /// <summary>All foldings that start exactly at <paramref name="startOffset"/>.</summary>
    public ReadOnlyCollection<FoldingSection> GetFoldingsAt(int startOffset)
    {
        var result = new List<FoldingSection>();
        for (int index = _foldings.FindFirstIndexWithStartAfter(startOffset);
             index < _foldings.Count && _foldings[index].StartOffset == startOffset;
             index++)
        {
            result.Add(_foldings[index]);
        }
        return result.AsReadOnly();
    }

    /// <summary>All foldings that contain <paramref name="offset"/>.</summary>
    public ReadOnlyCollection<FoldingSection> GetFoldingsContaining(int offset)
        => _foldings.FindSegmentsContaining(offset);

    /// <summary>
    /// Updates the foldings using the given new foldings, keeping the folded state of the sections
    /// that correspond to an existing one.
    /// </summary>
    /// <param name="newFoldings">The new set of foldings, sorted by start offset.</param>
    /// <param name="firstErrorOffset">
    /// The first position of a parse error. Existing foldings starting after this offset are kept
    /// even when they do not appear in <paramref name="newFoldings"/>. Use -1 when there were none.
    /// </param>
    public void UpdateFoldings(IEnumerable<NewFolding> newFoldings, int firstErrorOffset)
    {
        ObjectDisposedException.ThrowIf(_uninstalled, this);
        ArgumentNullException.ThrowIfNull(newFoldings);
        _batchDepth++;
        try
        {
            MergeFoldings(newFoldings, firstErrorOffset < 0 ? int.MaxValue : firstErrorOffset);
        }
        finally
        {
            EndBatch();
        }
    }

    private void MergeFoldings(IEnumerable<NewFolding> newFoldings, int firstErrorOffset)
    {
        var oldFoldings = AllFoldings.ToArray();
        int oldFoldingIndex = 0;
        int previousStartOffset = 0;
        // merge new foldings into old foldings so that sections keep being collapsed
        // both oldFoldings and newFoldings are sorted by start offset
        foreach (var newFolding in newFoldings)
        {
            if (newFolding.StartOffset < previousStartOffset)
            {
                throw new ArgumentException("newFoldings must be sorted by start offset", nameof(newFoldings));
            }
            previousStartOffset = newFolding.StartOffset;

            if (newFolding.StartOffset == newFolding.EndOffset)
            {
                continue; // ignore zero-length foldings
            }

            // remove old foldings that were skipped
            while (oldFoldingIndex < oldFoldings.Length &&
                   newFolding.StartOffset > oldFoldings[oldFoldingIndex].StartOffset)
            {
                RemoveFolding(oldFoldings[oldFoldingIndex++]);
            }

            FoldingSection section;
            // reuse current folding if its matching:
            if (oldFoldingIndex < oldFoldings.Length &&
                newFolding.StartOffset == oldFoldings[oldFoldingIndex].StartOffset)
            {
                section = oldFoldings[oldFoldingIndex++];
                section.Length = newFolding.EndOffset - newFolding.StartOffset;
            }
            else
            {
                // no matching current folding; create a new one:
                section = CreateFolding(newFolding.StartOffset, newFolding.EndOffset);
                // auto-close #regions only when opening the document
                if (_isFirstUpdate)
                {
                    section.IsFolded = newFolding.DefaultClosed;
                }
                section.Tag = newFolding;
            }
            section.Title = newFolding.Name;
        }
        _isFirstUpdate = false;

        // remove all outstanding old foldings:
        while (oldFoldingIndex < oldFoldings.Length)
        {
            var oldSection = oldFoldings[oldFoldingIndex++];
            if (oldSection.StartOffset >= firstErrorOffset)
            {
                break;
            }
            RemoveFolding(oldSection);
        }
        _batchChanged = true;
    }

    internal void Redraw()
    {
        if (_uninstalled) return;
        if (_batchDepth > 0)
        {
            _batchChanged = true;
            return;
        }
        RebuildCollapsedIndex();
        FoldingsChanged?.Invoke(this, EventArgs.Empty);
        _editor.InvalidateTextView();
    }

    private void EndBatch()
    {
        _batchDepth--;
        if (_batchDepth > 0 || !_batchChanged) return;
        _batchChanged = false;
        Redraw();
    }

    private void OnCaretPositionChanged(object? sender, EventArgs e)
    {
        // Expand Foldings when Caret is moved into them.
        int caretOffset = _editor.CaretOffset;
        foreach (var section in GetFoldingsContaining(caretOffset))
        {
            if (section.IsFolded && section.StartOffset < caretOffset && caretOffset < section.EndOffset)
            {
                section.IsFolded = false;
            }
        }
    }

    /// <summary>
    /// Whether the fold element on an earlier line already covers this one. The element reaches to
    /// the end of the logical line the fold ends on, so that line is swallowed too even when the
    /// fold stops in the middle of it.
    /// </summary>
    internal bool IsLineCollapsed(LogicalTextLine line)
    {
        int low = 0;
        int high = _collapsedRanges.Count;
        while (low < high)
        {
            int middle = low + ((high - low) / 2);
            if (_collapsedRanges[middle].Start < line.Offset) low = middle + 1;
            else high = middle;
        }
        if (low == 0) return false;
        var range = _collapsedRanges[low - 1];
        return line.Offset > range.Start && line.Offset <= range.End;
    }

    private void RebuildCollapsedIndex()
    {
        _collapsedRanges.Clear();
        foreach (var folding in _foldings.Where(static item => item.IsFolded))
        {
            if (_collapsedRanges.Count > 0 && folding.StartOffset <= _collapsedRanges[^1].End)
            {
                var current = _collapsedRanges[^1];
                _collapsedRanges[^1] = (current.Start, Math.Max(current.End, folding.EndOffset));
            }
            else
            {
                _collapsedRanges.Add((folding.StartOffset, folding.EndOffset));
            }
        }
    }
}
