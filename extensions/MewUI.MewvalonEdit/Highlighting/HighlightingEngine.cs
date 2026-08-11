using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <summary>
/// Regex-based highlighting engine. It scans one line at a time and carries a stack of open spans
/// across lines, so a region such as a block comment keeps its colour past a line break.
/// </summary>
public class HighlightingEngine
{
    private static readonly HighlightingRuleSet _emptyRuleSet = new() { Name = "EmptyRuleSet" };
    private static readonly Match?[] _noMatches = [];

    private readonly HighlightingRuleSet _mainRuleSet;
    private ImmutableStack<HighlightingSpan> _spanStack = ImmutableStack<HighlightingSpan>.Empty;

    private string _lineText = string.Empty;
    private int _lineStartOffset;
    private int _position;

    // Where highlighting output goes. Null means only the span state is being updated.
    private HighlightedLine? _highlightedLine;

    private Stack<HighlightedSection?>? _highlightedSectionStack;
    private HighlightedSection? _lastPoppedSection;

    public HighlightingEngine(HighlightingRuleSet mainRuleSet)
        => _mainRuleSet = mainRuleSet ?? throw new ArgumentNullException(nameof(mainRuleSet));

    /// <summary>
    /// Spans open at the current scan position. Set it to the state at the start of a line before
    /// scanning it; afterwards it holds the state at the end of that line.
    /// </summary>
    public ImmutableStack<HighlightingSpan> CurrentSpanStack
    {
        get => _spanStack;
        set => _spanStack = value ?? ImmutableStack<HighlightingSpan>.Empty;
    }

    /// <summary>Highlights one line and advances <see cref="CurrentSpanStack"/> past it.</summary>
    public HighlightedLine HighlightLine(TextDocument document, DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        _lineStartOffset = line.Offset;
        _lineText = document.GetText(line.Offset, line.Length);
        try
        {
            _highlightedLine = new HighlightedLine(document, line);
            HighlightLineInternal();
            return _highlightedLine;
        }
        finally
        {
            _highlightedLine = null;
            _lineText = string.Empty;
            _lineStartOffset = 0;
        }
    }

    /// <summary>
    /// Advances <see cref="CurrentSpanStack"/> past one line without producing any sections.
    /// </summary>
    public void ScanLine(TextDocument document, DocumentLine line)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(line);
        _lineText = document.GetText(line.Offset, line.Length);
        try
        {
            HighlightLineInternal();
        }
        finally
        {
            _lineText = string.Empty;
        }
    }

    private HighlightingRuleSet CurrentRuleSet
        => _spanStack.IsEmpty ? _mainRuleSet : _spanStack.Peek().RuleSet ?? _emptyRuleSet;

    private void HighlightLineInternal()
    {
        _position = 0;
        ResetColorStack();
        var currentRuleSet = CurrentRuleSet;
        var storedMatchArrays = new Stack<Match?[]>();
        var matches = AllocateMatchArray(currentRuleSet.Spans.Count);
        Match? endSpanMatch = null;

        while (true)
        {
            for (int i = 0; i < matches.Length; i++)
            {
                if (matches[i] is not Match previous || (previous.Success && previous.Index < _position))
                {
                    matches[i] = currentRuleSet.Spans[i].StartExpression.Match(_lineText, _position);
                }
            }
            if (endSpanMatch is null && !_spanStack.IsEmpty)
            {
                endSpanMatch = _spanStack.Peek().EndExpression.Match(_lineText, _position);
            }

            var firstMatch = Minimum(matches, endSpanMatch);
            if (firstMatch is null)
            {
                break;
            }

            HighlightNonSpans(firstMatch.Index);

            if (ReferenceEquals(firstMatch, endSpanMatch))
            {
                var poppedSpan = _spanStack.Peek();
                if (!poppedSpan.SpanColorIncludesEnd) PopColor();
                PushColor(poppedSpan.EndColor);
                _position = firstMatch.Index + firstMatch.Length;
                PopColor();
                if (poppedSpan.SpanColorIncludesEnd) PopColor();
                _spanStack = _spanStack.Pop();
                currentRuleSet = CurrentRuleSet;
                if (storedMatchArrays.Count > 0)
                {
                    matches = storedMatchArrays.Pop();
                    int index = currentRuleSet.Spans.IndexOf(poppedSpan);
                    if (index >= 0 && index < matches.Length && matches[index] is Match reopened && reopened.Index == _position)
                    {
                        throw new InvalidOperationException(
                            "A highlighting span matched 0 characters, which would cause an endless loop.\n"
                            + "Change the highlighting definition so that either the start or the end regex matches at least one character.\n"
                            + "Start regex: " + poppedSpan.StartExpression + "\n"
                            + "End regex: " + poppedSpan.EndExpression);
                    }
                }
                else
                {
                    matches = AllocateMatchArray(currentRuleSet.Spans.Count);
                }
            }
            else
            {
                var newSpan = currentRuleSet.Spans[Array.IndexOf(matches, firstMatch)];
                _spanStack = _spanStack.Push(newSpan);
                currentRuleSet = CurrentRuleSet;
                storedMatchArrays.Push(matches);
                matches = AllocateMatchArray(currentRuleSet.Spans.Count);
                if (newSpan.SpanColorIncludesStart) PushColor(newSpan.SpanColor);
                PushColor(newSpan.StartColor);
                _position = firstMatch.Index + firstMatch.Length;
                PopColor();
                if (!newSpan.SpanColorIncludesStart) PushColor(newSpan.SpanColor);
            }
            endSpanMatch = null;
        }

        HighlightNonSpans(_lineText.Length);
        PopAllColors();
    }

    /// <summary>Applies the current rule set to the text between the scan position and <paramref name="until"/>.</summary>
    private void HighlightNonSpans(int until)
    {
        if (_position == until)
        {
            return;
        }
        if (_highlightedLine is not null)
        {
            var rules = CurrentRuleSet.Rules;
            var matches = AllocateMatchArray(rules.Count);
            while (true)
            {
                for (int i = 0; i < matches.Length; i++)
                {
                    if (matches[i] is not Match previous || (previous.Success && previous.Index < _position))
                    {
                        matches[i] = rules[i].Regex.Match(_lineText, _position, until - _position);
                    }
                }
                var firstMatch = Minimum(matches, null);
                if (firstMatch is null)
                {
                    break;
                }
                _position = firstMatch.Index;
                int ruleIndex = Array.IndexOf(matches, firstMatch);
                if (firstMatch.Length == 0)
                {
                    throw new InvalidOperationException(
                        "A highlighting rule matched 0 characters, which would cause an endless loop.\n"
                        + "Change the highlighting definition so that the rule matches at least one character.\n"
                        + "Regex: " + rules[ruleIndex].Regex);
                }
                PushColor(rules[ruleIndex].Color);
                _position = firstMatch.Index + firstMatch.Length;
                PopColor();
            }
        }
        _position = until;
    }

    /// <summary>
    /// Opens the sections for the spans already on the stack, so a line inside a multi-line span
    /// starts out wearing that span's colour.
    /// </summary>
    private void ResetColorStack()
    {
        _lastPoppedSection = null;
        if (_highlightedLine is null)
        {
            _highlightedSectionStack = null;
        }
        else
        {
            _highlightedSectionStack = new Stack<HighlightedSection?>();
            foreach (var span in _spanStack.Reverse())
            {
                PushColor(span.SpanColor);
            }
        }
    }

    private void PushColor(HighlightingColor? color)
    {
        if (_highlightedLine is null || _highlightedSectionStack is null)
        {
            return;
        }
        if (color is null)
        {
            _highlightedSectionStack.Push(null);
        }
        else if (_lastPoppedSection is HighlightedSection last
            && last.Color == color
            && last.EndOffset == _position + _lineStartOffset)
        {
            // Reopen the section that just closed here rather than starting an identical one, so
            // the same colour applied twice in a row stays a single run.
            _highlightedSectionStack.Push(last);
            _lastPoppedSection = null;
        }
        else
        {
            var section = new HighlightedSection
            {
                Offset = _position + _lineStartOffset,
                Color = color
            };
            _highlightedLine.Sections.Add(section);
            _highlightedSectionStack.Push(section);
            _lastPoppedSection = null;
        }
    }

    private void PopColor()
    {
        if (_highlightedLine is null || _highlightedSectionStack is null)
        {
            return;
        }
        if (_highlightedSectionStack.Pop() is HighlightedSection section)
        {
            section.Length = _position + _lineStartOffset - section.Offset;
            if (section.Length == 0)
            {
                _highlightedLine.Sections.Remove(section);
            }
            else
            {
                _lastPoppedSection = section;
            }
        }
    }

    private void PopAllColors()
    {
        while (_highlightedSectionStack is not null && _highlightedSectionStack.Count > 0)
        {
            PopColor();
        }
    }

    /// <summary>The earliest successful match in <paramref name="candidates"/> or <paramref name="endSpanMatch"/>.</summary>
    private static Match? Minimum(Match?[] candidates, Match? endSpanMatch)
    {
        Match? min = null;
        foreach (var candidate in candidates)
        {
            if (candidate is not null && candidate.Success && (min is null || candidate.Index < min.Index))
            {
                min = candidate;
            }
        }
        if (endSpanMatch is not null && endSpanMatch.Success && (min is null || endSpanMatch.Index < min.Index))
        {
            return endSpanMatch;
        }
        return min;
    }

    private static Match?[] AllocateMatchArray(int count) => count == 0 ? _noMatches : new Match?[count];
}
