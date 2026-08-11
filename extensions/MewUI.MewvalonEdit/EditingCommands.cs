namespace Aprillz.MewUI.MewvalonEdit;

/// <summary>
/// The semantic identities of the editing operations the editor answers beyond what the editing
/// surface already handles, so a host can present them in menus and toolbars. <see cref="TextEditor"/>
/// binds their handlers and gestures; the commands carry no behavior of their own.
/// </summary>
public static class EditingCommands
{
    /// <summary>
    /// Runs the indentation strategy over the selected lines, or the whole document when nothing is
    /// selected. The default strategy reindents nothing, so this answers only where a host supplied
    /// a strategy that reads the language.
    /// </summary>
    public static readonly Command IndentSelection = new("editor.indentSelection", "Indent Selection");
}
