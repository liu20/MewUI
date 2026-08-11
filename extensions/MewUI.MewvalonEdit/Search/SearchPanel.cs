using System.Text.RegularExpressions;
using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.Text;
using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Search;

public sealed class SearchPanel : ITextClassifier
{
    private readonly TextEditor _editor;
    private readonly List<SearchResult> _results = [];
    private TextDocument _document;
    private string _searchPattern = string.Empty;
    private bool _matchCase;
    private SearchMode _searchMode;
    private bool _wholeWords;
    private ISearchStrategy? _strategy;
    private bool _strategyIsExplicit;
    private SearchPanelView? _view;
    private Adorner? _messageAdorner;
    private bool _uninstalled;
    private bool _suspendDocumentRefresh;

    private SearchPanel(TextEditor editor)
    {
        _editor = editor;
        _document = editor.Document;
        _editor.Surface.Extensions.Classifiers.Add(this);
        _document.Changed += OnDocumentChanged;
        _editor.DocumentChanged += OnEditorDocumentChanged;
    }

    public static SearchPanel Install(TextEditor editor)
    {
        ArgumentNullException.ThrowIfNull(editor);
        var panel = new SearchPanel(editor);
        // Installed with its keys, as the original does: a caller that only asks for a search panel
        // still expects Ctrl+F to reach it. The editor's scope and map cover the whole subtree, so
        // the keys work from the surface, the search box and the margins alike - the area the
        // original's routed commands covered.
        editor.Commands.Register(SearchCommands.Find, panel, static panel => panel.Open());
        editor.Commands.Register(SearchCommands.FindNext, panel,
            static panel => panel.FindNext(), static panel => !panel.IsClosed);
        editor.Commands.Register(SearchCommands.FindPrevious, panel,
            static panel => panel.FindPrevious(), static panel => !panel.IsClosed);
        editor.InputMap.Map(SearchCommands.Find, new KeyGesture(Key.F, ModifierKeys.Primary));
        // An installed panel starts open, so the walk keys start bound as well.
        panel.BindOpenGestures();
        return panel;
    }

    public static SearchPanel Install(Aprillz.MewUI.MewvalonEdit.Editing.TextArea textArea)
    {
        ArgumentNullException.ThrowIfNull(textArea);
        return Install(textArea.Editor);
    }

    /// <summary>Strings the panel shows. Assign a subclass to translate them.</summary>
    public Localization Localization { get; set; } = new();

    /// <summary>
    /// Whether the panel is closed. A closed panel searches nothing and highlights nothing, which
    /// is why closing clears the results rather than only hiding the controls. An installed panel
    /// starts open, so a caller that drives the search from its own interface needs no Open call.
    /// </summary>
    public bool IsClosed { get; private set; }

    /// <summary>Shows the panel's controls, or puts the caret back in them when already shown.</summary>
    public void Open()
    {
        ObjectDisposedException.ThrowIf(_uninstalled, this);
        bool wasClosed = IsClosed;
        IsClosed = false;
        _view ??= new SearchPanelView(this);
        ShowPanel(_view);
        if (wasClosed)
        {
            BindOpenGestures();
            Refresh();
        }
        Reactivate();
    }

    /// <summary>
    /// The walk and close keys are bound only while the panel is open, so a closed panel does not
    /// claim F3 or Escape away from the window: the nearest input map shadows farther ones even
    /// when its command is unavailable, and a closed panel has no business shadowing anything.
    /// </summary>
    private void BindOpenGestures()
    {
        _editor.InputMap.Map(SearchCommands.FindNext, new KeyGesture(Key.F3));
        _editor.InputMap.Map(SearchCommands.FindPrevious, new KeyGesture(Key.F3, ModifierKeys.Shift));
        _editor.InputMap.Map(new KeyGesture(Key.Escape), Close);
    }

    private void UnbindOpenGestures()
    {
        _editor.InputMap.Unmap(SearchCommands.FindNext);
        _editor.InputMap.Unmap(SearchCommands.FindPrevious);
        _editor.InputMap.Unmap(new KeyGesture(Key.Escape));
    }

    /// <summary>Hides the panel and drops its results, so nothing stays highlighted.</summary>
    public void Close()
    {
        if (IsClosed)
        {
            return;
        }
        IsClosed = true;
        UnbindOpenGestures();
        HidePanel();
        _results.Clear();
        _editor.InvalidateTextView();
        // The keyboard was in the panel; closing without this leaves it focused on a hidden box.
        _editor.Focus();
    }

    /// <summary>Puts the caret in the search box and selects what is there.</summary>
    public void Reactivate() => _view?.Reactivate();

    /// <summary>
    /// The controls go on the editor's overlay, which keeps them inside its input scope so the walk
    /// and close keys stay the editor's. Only the message rides an adorner: it hangs below the panel
    /// and must not be clipped by the editor's frame, and it can appear and go without resizing the
    /// controls the reader is using.
    /// </summary>
    private void ShowPanel(SearchPanelView view)
    {
        _editor.ShowOverlay(view.Root);
        _messageAdorner ??= new BelowPanelAdorner(_editor, view.Root, view.MessageRoot);
        _editor.ShowAdorner(_messageAdorner);
    }

    private void HidePanel()
    {
        if (_messageAdorner is Adorner message)
        {
            _editor.HideAdorner(message);
            _messageAdorner = null;
        }
        if (_view is SearchPanelView view)
        {
            _editor.HideOverlay(view.Root);
        }
    }

    /// <summary>
    /// Places its child directly under the panel rather than over the editor it adorns, which is
    /// where the original puts the message it shows beside the search box.
    /// </summary>
    private sealed class BelowPanelAdorner(UIElement adorned, UIElement panel, UIElement child)
        : Adorner(adorned, child)
    {
        protected override void ArrangeContent(Rect bounds)
        {
            var above = panel.Bounds;
            var slot = above.Height > 0
                ? new Rect(bounds.X, above.Bottom, bounds.Width, Math.Max(0, bounds.Bottom - above.Bottom))
                : bounds;
            for (int index = 0; index < Children.Count; index++)
            {
                Children[index].Arrange(slot);
            }
        }
    }

    /// <summary>Detaches the panel from the editor. Calling it twice is harmless.</summary>
    public void Uninstall()
    {
        if (_uninstalled)
        {
            return;
        }
        Close();
        _uninstalled = true;
        _editor.InputMap.Unmap(SearchCommands.Find);
        _editor.Commands.Unregister(SearchCommands.Find);
        _editor.Commands.Unregister(SearchCommands.FindNext);
        _editor.Commands.Unregister(SearchCommands.FindPrevious);
        _editor.Surface.Extensions.Classifiers.Remove(this);
        _document.Changed -= OnDocumentChanged;
        _editor.DocumentChanged -= OnEditorDocumentChanged;
        _editor.InvalidateTextView();
    }

    public string SearchPattern
    {
        get => _searchPattern;
        set
        {
            value ??= string.Empty;
            if (_searchPattern == value) return;
            _searchPattern = value;
            Refresh(changeSelection: true);
        }
    }

    /// <summary>
    /// Throws when the pattern cannot be searched with under the current options. The box a reader
    /// types into validates through this, so an unfinished regular expression puts it in its invalid
    /// state; the original reaches the same state through a validation rule on that box's binding.
    /// </summary>
    /// <exception cref="SearchPatternException">The pattern cannot be searched with.</exception>
    public void ValidatePattern(string? pattern)
    {
        if (_strategyIsExplicit || string.IsNullOrEmpty(pattern))
        {
            return;
        }
        SearchStrategyFactory.Create(pattern, !MatchCase, WholeWords, SearchMode);
    }

    public bool MatchCase
    {
        get => _matchCase;
        set { if (_matchCase != value) { _matchCase = value; Refresh(changeSelection: true); } }
    }

    /// <summary>Shorthand for <see cref="SearchMode"/>, which carries the wildcard mode as well.</summary>
    public bool UseRegex
    {
        get => SearchMode == SearchMode.RegEx;
        set => SearchMode = value ? SearchMode.RegEx : SearchMode.Normal;
    }

    /// <summary>How the pattern is read. Changing it rebuilds the strategy.</summary>
    public SearchMode SearchMode
    {
        get => _searchMode;
        set { if (_searchMode != value) { _searchMode = value; Refresh(changeSelection: true); } }
    }

    public bool WholeWords
    {
        get => _wholeWords;
        set { if (_wholeWords != value) { _wholeWords = value; Refresh(changeSelection: true); } }
    }

    /// <summary>
    /// The algorithm behind the search. Assigning one takes the pattern and options out of play,
    /// which is how a caller substitutes its own matching; null returns to the built-in strategy.
    /// </summary>
    public ISearchStrategy? SearchStrategy
    {
        get => _strategy;
        set { _strategy = value; _strategyIsExplicit = value is not null; Refresh(changeSelection: true); }
    }

    public Color MarkerBrush { get; set; } = Color.FromArgb(150, 255, 215, 0);
    public IReadOnlyList<SearchResult> Results => _results;

    /// <summary>
    /// Why the current pattern found nothing to search with, or null when it is usable. A pattern
    /// being typed is invalid most of the way in, so this is recorded rather than announced; the
    /// panel shows it once the reader asks to search.
    /// </summary>
    public string? PatternError { get; private set; }

    /// <summary>
    /// Selects the match after <paramref name="startOffset"/>, wrapping to the first one. Negative
    /// takes one past the start of the current selection, so the match a reader is already on is
    /// stepped over rather than found again.
    /// </summary>
    public SearchResult? FindNext(int startOffset = -1)
    {
        if (_results.Count == 0) return null;
        if (startOffset < 0) startOffset = _editor.SelectionStart + 1;
        int index = LowerBoundByOffset(startOffset);
        var result = index < _results.Count ? _results[index] : _results[0];
        SelectResult(result);
        return result;
    }

    /// <summary>
    /// Selects the match before <paramref name="startOffset"/>, wrapping to the last one. Negative
    /// takes the start of the current selection.
    /// </summary>
    public SearchResult? FindPrevious(int startOffset = -1)
    {
        if (_results.Count == 0) return null;
        if (startOffset < 0) startOffset = _editor.SelectionStart;
        int index = LowerBoundByOffset(startOffset) - 1;
        var result = index >= 0 ? _results[index] : _results[^1];
        SelectResult(result);
        return result;
    }

    /// <summary>A found match is selected and brought on screen, or finding it changed nothing.</summary>
    private void SelectResult(SearchResult result)
    {
        // The caret ends at the start of the match, as the original leaves it. Selecting forwards
        // would leave it at the end, and the walk keys off where the match begins.
        _editor.Select(result.EndOffset, 0);
        _editor.MoveCaret(result.Offset, extendSelection: true);
        _editor.TextArea.Caret.BringCaretToView();
        // The reader is typing in the search box, so the editor does not hold the keyboard.
        _editor.TextArea.Caret.Show();
    }

    public int ReplaceAll(string? replacement)
    {
        replacement ??= string.Empty;
        int count = _results.Count;
        _suspendDocumentRefresh = true;
        try
        {
            for (int index = _results.Count - 1; index >= 0; index--)
            {
                var result = _results[index];
                _editor.Document.Replace(result.Offset, result.Length, replacement);
            }
        }
        finally
        {
            _suspendDocumentRefresh = false;
        }
        Refresh();
        return count;
    }

    /// <summary>Rescans the document, leaving the selection where it is.</summary>
    public void Refresh() => Refresh(changeSelection: false);

    /// <summary>
    /// Rescans the document. With <paramref name="changeSelection"/> the first match at or after the
    /// current selection is also selected and brought on screen, which is what makes typing into the
    /// box walk the reader through the document. Nothing stays selected when no match lies at or
    /// after that point: only asking for the next match wraps.
    /// </summary>
    private void Refresh(bool changeSelection)
    {
        ObjectDisposedException.ThrowIf(_uninstalled, this);
        int anchor = _editor.SelectionStart;
        _results.Clear();
        if (IsClosed)
        {
            // A closed panel highlights nothing, so there is nothing to look for.
            _editor.InvalidateTextView();
            return;
        }
        if (!_strategyIsExplicit)
        {
            // Rebuilt from the current options; a caller-supplied one is left alone.
            _strategy = null;
        }
        if (string.IsNullOrEmpty(_searchPattern) && !_strategyIsExplicit)
        {
            _editor.InvalidateTextView();
            return;
        }

        if (changeSelection)
        {
            _editor.Select(anchor, 0);
        }
        PatternError = null;
        try
        {
            _strategy ??= SearchStrategyFactory.Create(_searchPattern, !MatchCase, WholeWords, SearchMode);
            foreach (var match in _strategy.FindAll(_editor.Document, 0, _editor.Document.TextLength))
            {
                if (match.Length > 0)
                {
                    _results.Add(new SearchResult(match.Offset, match.Length));
                }
            }
        }
        catch (SearchPatternException exception)
        {
            // An incomplete interactive pattern has no results until it becomes usable. The reason
            // is kept rather than raised, so the panel can say it once the reader asks to search.
            PatternError = exception.Message;
        }
        catch (RegexMatchTimeoutException)
        {
            // An expensive interactive expression yields no results instead of blocking input.
            _results.Clear();
        }
        if (changeSelection)
        {
            int index = LowerBoundByOffset(anchor);
            if (index < _results.Count)
            {
                SelectResult(_results[index]);
            }
        }
        _editor.InvalidateTextView();
    }

    void ITextClassifier.Classify(in TextClassificationContext context, IList<TextPaintSpan> output)
    {
        int lineStart = context.LogicalLine.Offset;
        int lineEnd = lineStart + context.LogicalLine.Length;
        int index = LowerBoundByEnd(lineStart);
        for (; index < _results.Count; index++)
        {
            var result = _results[index];
            if (result.Offset >= lineEnd) break;
            int start = Math.Max(lineStart, result.Offset);
            int end = Math.Min(lineEnd, result.EndOffset);
            // A paint span addresses the laid-out text, which an element standing more columns in
            // for the text it covers, such as the tab marker, has moved away from the document.
            int projectedStart = context.OffsetMap.MapFromSource(start - lineStart);
            int projectedEnd = context.OffsetMap.MapFromSource(end - lineStart);
            if (projectedEnd > projectedStart)
            {
                output.Add(new TextPaintSpan(
                    new TextRange(projectedStart, projectedEnd - projectedStart),
                    Background: MarkerBrush));
            }
        }
    }

    private void OnDocumentChanged(object? sender, DocumentChangeEventArgs e)
    {
        if (_suspendDocumentRefresh) return;
        // Only a literal pattern can be rescanned around the edit; every other mode, and any
        // strategy the caller supplied, has to see the whole document again.
        if (SearchMode != SearchMode.Normal || _strategyIsExplicit || string.IsNullOrEmpty(_searchPattern))
        {
            Refresh();
            return;
        }

        int delta = e.InsertionLength - e.RemovalLength;
        int scanPadding = _searchPattern.Length + (WholeWords ? 1 : 0);
        int scanStart = Math.Max(0, e.Offset - scanPadding);
        int scanEnd = Math.Min(_editor.Document.TextLength, e.Offset + e.InsertionLength + scanPadding);
        var retained = new List<SearchResult>(_results.Count);
        foreach (var result in _results)
        {
            SearchResult adjusted;
            if (result.EndOffset <= e.Offset)
            {
                adjusted = result;
            }
            else if (result.Offset >= e.Offset + e.RemovalLength)
            {
                adjusted = result with { Offset = result.Offset + delta };
            }
            else
            {
                continue;
            }

            if (adjusted.EndOffset <= scanStart || adjusted.Offset >= scanEnd)
            {
                retained.Add(adjusted);
            }
        }

        FindPlainTextMatches(scanStart, scanEnd, retained);
        retained.Sort(static (left, right) => left.Offset.CompareTo(right.Offset));
        _results.Clear();
        _results.AddRange(retained);
        _editor.InvalidateTextView();
    }

    private void FindPlainTextMatches(int scanStart, int scanEnd, List<SearchResult> output)
    {
        const int BlockSize = 64 * 1024;
        int patternLength = _searchPattern.Length;
        int documentLength = _editor.Document.TextLength;
        int lastStart = Math.Min(documentLength - patternLength, scanEnd);
        int blockStart = Math.Max(0, scanStart);
        while (blockStart <= lastStart)
        {
            int primaryLength = Math.Min(BlockSize, lastStart - blockStart + 1);
            int textLength = Math.Min(documentLength - blockStart, primaryLength + patternLength - 1);
            string block = _editor.Document.GetText(blockStart, textLength);
            for (int localOffset = 0; localOffset < primaryLength; localOffset++)
            {
                int offset = blockStart + localOffset;
                if (!MatchesAt(block, localOffset)) continue;
                if (WholeWords &&
                    ((offset > 0 && IsWordCharacter(_editor.Document.GetCharAt(offset - 1))) ||
                     (offset + patternLength < documentLength &&
                      IsWordCharacter(_editor.Document.GetCharAt(offset + patternLength)))))
                {
                    continue;
                }
                output.Add(new SearchResult(offset, patternLength));
                localOffset += Math.Max(0, patternLength - 1);
            }
            blockStart += primaryLength;
        }
    }

    private bool MatchesAt(string block, int offset)
    {
        for (int index = 0; index < _searchPattern.Length; index++)
        {
            char actual = block[offset + index];
            char expected = _searchPattern[index];
            if (MatchCase ? actual != expected : char.ToUpperInvariant(actual) != char.ToUpperInvariant(expected))
            {
                return false;
            }
        }
        return true;
    }

    private int LowerBoundByOffset(int offset)
    {
        int low = 0;
        int high = _results.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (_results[middle].Offset < offset) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private int LowerBoundByEnd(int offset)
    {
        int low = 0;
        int high = _results.Count;
        while (low < high)
        {
            int middle = low + (high - low) / 2;
            if (_results[middle].EndOffset <= offset) low = middle + 1;
            else high = middle;
        }
        return low;
    }

    private static bool IsWordCharacter(char value) => char.IsLetterOrDigit(value) || value == '_';

    private void OnEditorDocumentChanged(object? sender, EventArgs e)
    {
        _document.Changed -= OnDocumentChanged;
        _document = _editor.Document;
        _document.Changed += OnDocumentChanged;
        Refresh();
    }
}
