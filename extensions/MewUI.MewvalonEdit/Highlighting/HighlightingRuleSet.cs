using System.Text.RegularExpressions;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <summary>The spans and rules that are valid at one point in the document.</summary>
public class HighlightingRuleSet
{
    public string? Name { get; set; }

    public IList<HighlightingSpan> Spans { get; } = new List<HighlightingSpan>();

    public IList<HighlightingRule> Rules { get; } = new List<HighlightingRule>();

    /// <inheritdoc/>
    public override string ToString() => "[" + GetType().Name + " " + Name + "]";
}

/// <summary>One pattern and the colour the text it matches is drawn in.</summary>
public class HighlightingRule
{
    public Regex Regex { get; set; } = null!;
    public HighlightingColor? Color { get; set; }

    /// <inheritdoc/>
    public override string ToString() => "[" + GetType().Name + " " + Regex + "]";
}

/// <summary>
/// A region with a start and end pattern, which may cross line breaks. While it is open its
/// <see cref="RuleSet"/> replaces the enclosing one.
/// </summary>
public class HighlightingSpan
{
    public Regex StartExpression { get; set; } = null!;
    public Regex EndExpression { get; set; } = null!;

    /// <summary>The rule set that applies inside the span, or null for no rules at all.</summary>
    public HighlightingRuleSet? RuleSet { get; set; }

    /// <summary>Colour for the text the start expression matched.</summary>
    public HighlightingColor? StartColor { get; set; }

    /// <summary>Colour for the text between start and end.</summary>
    public HighlightingColor? SpanColor { get; set; }

    /// <summary>Colour for the text the end expression matched.</summary>
    public HighlightingColor? EndColor { get; set; }

    /// <summary>Whether <see cref="SpanColor"/> also covers the start delimiter.</summary>
    public bool SpanColorIncludesStart { get; set; }

    /// <summary>Whether <see cref="SpanColor"/> also covers the end delimiter.</summary>
    public bool SpanColorIncludesEnd { get; set; }

    /// <inheritdoc/>
    public override string ToString()
        => "[" + GetType().Name + " Start=" + StartExpression + ", End=" + EndExpression + "]";
}

/// <summary>A syntax definition: its main rule set plus the elements it names.</summary>
public interface IHighlightingDefinition
{
    string Name { get; }

    HighlightingRuleSet MainRuleSet { get; }

    /// <summary>The rule set registered under <paramref name="name"/>, or null if there is none.</summary>
    HighlightingRuleSet? GetNamedRuleSet(string name);

    /// <summary>The colour registered under <paramref name="name"/>, or null if there is none.</summary>
    HighlightingColor? GetNamedColor(string name);

    IEnumerable<HighlightingColor> NamedHighlightingColors { get; }

    IDictionary<string, string> Properties { get; }
}

/// <summary>Resolves a reference from one syntax definition to another by name.</summary>
public interface IHighlightingDefinitionReferenceResolver
{
    /// <summary>The definition registered under <paramref name="name"/>, or null if there is none.</summary>
    IHighlightingDefinition? GetDefinition(string name);
}
