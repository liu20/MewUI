namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>
/// Either a reference to a named element, possibly in another syntax definition, or an element
/// written inline at the point of use.
/// </summary>
public readonly struct XshdReference<T> : IEquatable<XshdReference<T>> where T : XshdElement
{
    /// <summary>Name of the definition the element lives in, or null for the current one.</summary>
    public string? ReferencedDefinition { get; }

    /// <summary>Name of the referenced element, or null when this reference is inline or empty.</summary>
    public string? ReferencedElement { get; }

    /// <summary>The element written inline, or null when this reference names one.</summary>
    public T? InlineElement { get; }

    public XshdReference(string? referencedDefinition, string referencedElement)
    {
        ArgumentNullException.ThrowIfNull(referencedElement);
        ReferencedDefinition = referencedDefinition;
        ReferencedElement = referencedElement;
        InlineElement = null;
    }

    public XshdReference(T inlineElement)
    {
        ArgumentNullException.ThrowIfNull(inlineElement);
        ReferencedDefinition = null;
        ReferencedElement = null;
        InlineElement = inlineElement;
    }

    /// <summary>Applies the visitor to the inline element, if there is one.</summary>
    public object? AcceptVisitor(IXshdVisitor visitor)
        => InlineElement?.AcceptVisitor(visitor);

    public bool Equals(XshdReference<T> other)
        => ReferencedDefinition == other.ReferencedDefinition
            && ReferencedElement == other.ReferencedElement
            && ReferenceEquals(InlineElement, other.InlineElement);

    public override bool Equals(object? obj) => obj is XshdReference<T> other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(ReferencedDefinition, ReferencedElement, InlineElement);

    public static bool operator ==(XshdReference<T> left, XshdReference<T> right) => left.Equals(right);

    public static bool operator !=(XshdReference<T> left, XshdReference<T> right) => !left.Equals(right);
}
