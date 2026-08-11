namespace Aprillz.MewUI.MewvalonEdit.Snippets;

/// <summary>Why the interactive snippet mode ended.</summary>
public enum DeactivateReason
{
    /// <summary>Unknown reason.</summary>
    Unknown,

    /// <summary>The snippet was deleted, undo included.</summary>
    Deleted,

    /// <summary>There were no active elements to stay interactive for.</summary>
    NoActiveElements,

    /// <summary>The input handler was detached, e.g. by another snippet taking over.</summary>
    InputHandlerDetached,

    /// <summary>The user pressed Return.</summary>
    ReturnPressed,

    /// <summary>The user pressed Escape.</summary>
    EscapePressed
}

/// <summary>Carries the <see cref="DeactivateReason"/> of a snippet deactivation.</summary>
public class SnippetEventArgs(DeactivateReason reason) : EventArgs
{
    public DeactivateReason Reason { get; } = reason;
}
