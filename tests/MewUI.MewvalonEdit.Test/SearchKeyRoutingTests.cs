using Aprillz.MewUI;
using Aprillz.MewUI.Controls;
using Aprillz.MewUI.Input;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Search;
using MewUI.MewvalonEdit.Test.Infrastructure;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The search keys live on the editor's input map, so they answer wherever the focus is inside the
/// editor subtree - the area the original's routed commands covered - and nowhere outside it. The
/// walk and close keys are bound only while the panel is open, which is what keeps a closed panel
/// from shadowing the same gestures elsewhere in the window.
/// </summary>
[TestClass]
[DoNotParallelize]
public sealed class SearchKeyRoutingTests
{
    private const string TEXT = "the cat sat on the category mat\nanother cat came by";

    private static (Window window, TextEditor editor, SearchPanel panel) CreateHost()
    {
        var window = ScaledWindow.Create(1.0);
        var editor = new TextEditor { Text = TEXT };
        window.Content = editor;
        window.PerformLayout();
        return (window, editor, SearchPanel.Install(editor));
    }

    private static void SendKey(Window window, Key key, ModifierKeys modifiers = ModifierKeys.None)
        => WindowInputRouter.KeyDown(window, new KeyEventArgs(key, platformKey: 0, modifiers));

    [TestMethod]
    public void CtrlFOpensThePanelFromTheEditingSurface()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var (window, editor, panel) = CreateHost();
        panel.Close();
        window.FocusManager.SetFocus(editor.Surface);

        SendKey(window, Key.F, ModifierKeys.Control);

        Assert.IsFalse(panel.IsClosed, "Ctrl+F from the surface did not open the panel.");
    }

    [TestMethod]
    public void CtrlFFromTheSearchBoxPutsTheCaretBackInIt()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var (window, editor, panel) = CreateHost();
        panel.Open();
        panel.SearchPattern = "cat";
        Assert.IsInstanceOfType<TextBox>(window.FocusManager.FocusedElement,
            "Opening must put the focus in the search box.");

        SendKey(window, Key.F, ModifierKeys.Control);

        Assert.IsFalse(panel.IsClosed);
        Assert.IsInstanceOfType<TextBox>(window.FocusManager.FocusedElement,
            "Reopening moved the focus away from the search box.");
    }

    [TestMethod]
    public void TheWalkKeysWalkFromTheSearchBox()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var (window, editor, panel) = CreateHost();
        panel.Open();
        panel.SearchPattern = "cat";
        Assert.AreEqual(panel.Results[0].Offset, editor.SelectionStart,
            "Typing the pattern already lands on the first match ahead of the caret.");

        SendKey(window, Key.F3);
        Assert.AreEqual(panel.Results[1].Offset, editor.SelectionStart);

        SendKey(window, Key.F3, ModifierKeys.Shift);
        Assert.AreEqual(panel.Results[0].Offset, editor.SelectionStart);
    }

    [TestMethod]
    public void EscapeClosesAndAClosedPanelDoesNotClaimTheKeys()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var (window, editor, panel) = CreateHost();
        panel.Open();
        panel.SearchPattern = "cat";
        int windowF3Hits = 0;
        window.InputMap.Map(new KeyGesture(Key.F3), () => windowF3Hits++);

        SendKey(window, Key.Escape);
        Assert.IsTrue(panel.IsClosed, "Escape did not close the panel.");

        window.FocusManager.SetFocus(editor.Surface);
        SendKey(window, Key.F3);
        Assert.AreEqual(1, windowF3Hits,
            "A closed panel claimed F3 away from the window's own map.");

        panel.Open();
        SendKey(window, Key.F3);
        Assert.AreEqual(1, windowF3Hits, "An open panel must claim F3 again.");
        Assert.AreEqual(panel.Results[1].Offset, editor.SelectionStart,
            "Reopening leaves the selection alone, so the walk goes on from the match it was on.");
    }

    [TestMethod]
    public void EnterWalksTheMatchesInsideThePanel()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var (window, editor, panel) = CreateHost();
        panel.Open();
        panel.SearchPattern = "cat";

        SendKey(window, Key.Enter);
        Assert.AreEqual(panel.Results[1].Offset, editor.SelectionStart);

        SendKey(window, Key.Enter, ModifierKeys.Shift);
        Assert.AreEqual(panel.Results[0].Offset, editor.SelectionStart);
    }

    [TestMethod]
    public void TheSearchKeysAreTheEditorSubtreesNotTheWindows()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var window = ScaledWindow.Create(1.0);
        var editor = new TextEditor { Text = TEXT };
        var outsider = new Button();
        window.Content = new Grid().Children(editor, outsider);
        window.PerformLayout();
        var panel = SearchPanel.Install(editor);
        panel.Close();
        window.FocusManager.SetFocus(outsider);

        SendKey(window, Key.F, ModifierKeys.Control);

        Assert.IsTrue(panel.IsClosed, "Ctrl+F outside the editor subtree reached the panel.");
    }

    [TestMethod]
    public void UninstallReleasesTheKeys()
    {
        if (!OperatingSystem.IsWindows()) { Assert.Inconclusive("GDI backend is Windows-only."); return; }

        var (window, editor, panel) = CreateHost();
        panel.Open();
        panel.Uninstall();

        int windowFindHits = 0;
        window.InputMap.Map(new KeyGesture(Key.F, ModifierKeys.Primary), () => windowFindHits++);
        window.FocusManager.SetFocus(editor.Surface);

        SendKey(window, Key.F, ModifierKeys.Control);

        Assert.AreEqual(1, windowFindHits, "An uninstalled panel still claimed Ctrl+F.");
    }
}
