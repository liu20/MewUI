namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>An element in an xshd rule set.</summary>
public abstract class XshdElement
{
    /// <summary>Line number in the .xshd file, or 0 when the reader did not report one.</summary>
    public int LineNumber { get; set; }

    /// <summary>Column number in the .xshd file, or 0 when the reader did not report one.</summary>
    public int ColumnNumber { get; set; }

    /// <summary>Applies the visitor to this element.</summary>
    public abstract object? AcceptVisitor(IXshdVisitor visitor);
}

/// <summary>A visitor over the xshd element tree.</summary>
public interface IXshdVisitor
{
    object? VisitRuleSet(XshdRuleSet ruleSet);
    object? VisitColor(XshdColor color);
    object? VisitKeywords(XshdKeywords keywords);
    object? VisitSpan(XshdSpan span);
    object? VisitImport(XshdImport import);
    object? VisitRule(XshdRule rule);
}
