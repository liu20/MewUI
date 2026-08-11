namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>How the regex text in an xshd file is to be compiled.</summary>
public enum XshdRegexType
{
    /// <summary>Plain regex, as written in an attribute.</summary>
    Default,

    /// <summary>Whitespace and '#' comments are ignored, as written in an element body.</summary>
    IgnorePatternWhitespace
}

/// <summary>A &lt;Rule&gt; element.</summary>
public sealed class XshdRule : XshdElement
{
    public string? Regex { get; set; }
    public XshdRegexType RegexType { get; set; }
    public XshdReference<XshdColor> ColorReference { get; set; }

    /// <inheritdoc/>
    public override object? AcceptVisitor(IXshdVisitor visitor) => visitor.VisitRule(this);
}

/// <summary>A &lt;Keywords&gt; element: a word list sharing one colour.</summary>
public sealed class XshdKeywords : XshdElement
{
    public XshdReference<XshdColor> ColorReference { get; set; }

    public IList<string> Words { get; } = new List<string>();

    /// <inheritdoc/>
    public override object? AcceptVisitor(IXshdVisitor visitor) => visitor.VisitKeywords(this);
}

/// <summary>An &lt;Import&gt; element: pulls another rule set's rules and spans into this one.</summary>
public sealed class XshdImport : XshdElement
{
    public XshdReference<XshdRuleSet> RuleSetReference { get; set; }

    /// <inheritdoc/>
    public override object? AcceptVisitor(IXshdVisitor visitor) => visitor.VisitImport(this);
}

/// <summary>A &lt;Property&gt; element: a name/value pair a host can read off the definition.</summary>
public sealed class XshdProperty : XshdElement
{
    public string? Name { get; set; }
    public string? Value { get; set; }

    /// <inheritdoc/>
    public override object? AcceptVisitor(IXshdVisitor visitor) => null;
}
