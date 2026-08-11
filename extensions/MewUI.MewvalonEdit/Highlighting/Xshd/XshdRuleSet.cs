namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>A &lt;RuleSet&gt; element. The nameless one in a definition is its main rule set.</summary>
public sealed class XshdRuleSet : XshdElement
{
    public string? Name { get; set; }

    /// <summary>Whether expressions in this rule set match case-insensitively. Null inherits.</summary>
    public bool? IgnoreCase { get; set; }

    public IList<XshdElement> Elements { get; } = new List<XshdElement>();

    /// <summary>Applies the visitor to every element in this rule set.</summary>
    public void AcceptElements(IXshdVisitor visitor)
    {
        foreach (var element in Elements)
        {
            element.AcceptVisitor(visitor);
        }
    }

    /// <inheritdoc/>
    public override object? AcceptVisitor(IXshdVisitor visitor) => visitor.VisitRuleSet(this);
}

/// <summary>A &lt;Span&gt; element: a region with a start and end pattern and its own rule set.</summary>
public sealed class XshdSpan : XshdElement
{
    public string? BeginRegex { get; set; }
    public XshdRegexType BeginRegexType { get; set; }
    public string? EndRegex { get; set; }
    public XshdRegexType EndRegexType { get; set; }

    /// <summary>Whether the span may cross a line break. A single-line span ends at the line end.</summary>
    public bool Multiline { get; set; }

    public XshdReference<XshdRuleSet> RuleSetReference { get; set; }
    public XshdReference<XshdColor> SpanColorReference { get; set; }
    public XshdReference<XshdColor> BeginColorReference { get; set; }
    public XshdReference<XshdColor> EndColorReference { get; set; }

    /// <inheritdoc/>
    public override object? AcceptVisitor(IXshdVisitor visitor) => visitor.VisitSpan(this);
}

/// <summary>A &lt;SyntaxDefinition&gt; element: one parsed .xshd file.</summary>
public sealed class XshdSyntaxDefinition
{
    public string? Name { get; set; }

    /// <summary>File extensions this definition claims, each with its leading dot.</summary>
    public IList<string> Extensions { get; } = new List<string>();

    public IList<XshdElement> Elements { get; } = new List<XshdElement>();

    /// <summary>Applies the visitor to every top-level element.</summary>
    public void AcceptElements(IXshdVisitor visitor)
    {
        foreach (var element in Elements)
        {
            element.AcceptVisitor(visitor);
        }
    }
}
