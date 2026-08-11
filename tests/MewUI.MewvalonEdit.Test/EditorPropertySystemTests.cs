using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// The editor's properties are MewProperties wherever AvalonEdit declares a DependencyProperty, so
/// they can be bound and styled instead of only assigned. Compiling proves nothing about that.
/// </summary>
[TestClass]
public sealed class EditorPropertySystemTests
{
    [TestMethod]
    public void BindingDrivesThePropertyAndTheChangeReachesTheSurface()
    {
        var editor = new TextEditor { Text = "bind me" };
        var wrap = new ObservableValue<bool>(false);

        editor.SetBinding(TextEditor.WordWrapProperty, wrap);
        wrap.Value = true;

        // The binding has to land on the surface, not just in the property store, or wrapping
        // would be a value nobody reads.
        Assert.IsTrue(editor.WordWrap);
        Assert.IsTrue(editor.Surface.Wrap);
    }

    [TestMethod]
    public void AssigningThroughTheDescriptorTakesTheSamePath()
    {
        var editor = new TextEditor { Text = "one\ntwo" };

        editor.SetCurrentValue(TextEditor.ShowLineNumbersProperty, true);

        Assert.IsTrue(editor.ShowLineNumbers);
        Assert.IsTrue(editor.TextArea.LeftMargins[0].IsVisible);
    }

    [TestMethod]
    public void TheDocumentPropertyStillRejectsNull()
    {
        var editor = new TextEditor();

        // The CLR setter is no longer where the null check lives, so the descriptor path has to
        // carry it: SetValue must not be a way around the contract.
        Assert.ThrowsExactly<ArgumentNullException>(() => editor.Document = null!);
        Assert.ThrowsExactly<ArgumentNullException>(
            () => editor.SetCurrentValue(TextEditor.DocumentProperty, null));
        Assert.IsNotNull(editor.Document);
    }

    [TestMethod]
    public void ReplacingTheDocumentMovesTheChangeSubscription()
    {
        var editor = new TextEditor { Text = "first" };
        var replaced = new TextDocument { Text = "second" };
        var first = editor.Document;

        editor.Document = replaced;
        int changes = 0;
        editor.TextChanged += (_, _) => changes++;

        // The old document must be let go, or edits to a detached document would still raise the
        // editor's events.
        first.Text = "edited away";
        Assert.AreEqual(0, changes);

        replaced.Text = "edited here";
        Assert.IsGreaterThan(0, changes);
    }
}
