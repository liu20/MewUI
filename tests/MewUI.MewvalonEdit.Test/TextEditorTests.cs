using Aprillz.MewUI.Controls;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Folding;
using Aprillz.MewUI.MewvalonEdit.Highlighting;

namespace Aprillz.MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class TextEditorTests
{
    [TestMethod]
    public void EditorConsumesDocumentAndExtensionPipeline()
    {
        var document = new TextDocument("class C\n{\n}\n");
        var editor = new TextEditor
        {
            Document = document,
            SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#"),
            ShowLineNumbers = true
        };
        var folding = FoldingManager.Install(editor);
        folding.UpdateFoldings([new NewFolding(7, document.TextLength - 1) { DefaultClosed = true }], -1);

        Assert.AreSame(document, editor.Document);
        Assert.AreEqual(document.Text, editor.Text);
        Assert.IsTrue(editor.ShowLineNumbers);
        Assert.IsTrue(folding.AllFoldings.Single().IsFolded);

        document.Insert(document.TextLength, "// end");
        Assert.EndsWith("// end", document.Text);
        FoldingManager.Uninstall(folding);
    }

    /// <summary>
    /// Without a byte order mark the reader has nothing to detect, so the encoding the caller set is
    /// what decodes the bytes. Hardcoding UTF-8 there turns every other encoding into question marks.
    /// </summary>
    [TestMethod]
    public void LoadDecodesWithTheEncodingItWasGiven()
    {
        byte[] latin1 = System.Text.Encoding.Latin1.GetBytes("café");
        var editor = new TextEditor { Encoding = System.Text.Encoding.Latin1 };

        editor.Load(new MemoryStream(latin1));

        Assert.AreEqual("café", editor.Text);
    }

    [TestMethod]
    public void LoadPrefersAByteOrderMarkOverTheEncodingItWasGiven()
    {
        var utf8WithMark = new System.Text.UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        byte[] bytes = [.. utf8WithMark.GetPreamble(), .. System.Text.Encoding.UTF8.GetBytes("café")];
        var editor = new TextEditor { Encoding = System.Text.Encoding.Latin1 };

        editor.Load(new MemoryStream(bytes));

        Assert.AreEqual("café", editor.Text);
        Assert.AreEqual(System.Text.Encoding.UTF8.CodePage, editor.Encoding?.CodePage,
            "The encoding ends up at what was actually read.");
    }

    /// <summary>A document saved with no encoding set carries no byte order mark, as in the original.</summary>
    [TestMethod]
    public void SaveWithoutAnEncodingWritesNoByteOrderMark()
    {
        var editor = new TextEditor { Text = "café" };
        var stream = new MemoryStream();

        editor.Save(stream);

        Assert.HasCount(System.Text.Encoding.UTF8.GetBytes("café").Length, stream.ToArray());
    }

    /// <summary>Undo and redo report whether there was anything to do, as the original's do.</summary>
    [TestMethod]
    public void UndoAndRedoReportWhetherTheyHappened()
    {
        var editor = new TextEditor();

        Assert.IsFalse(editor.Undo(), "There was nothing to undo yet.");
        Assert.IsFalse(editor.Redo(), "There was nothing to redo yet.");

        editor.Document.Insert(0, "text");
        Assert.IsTrue(editor.Undo());
        Assert.AreEqual(string.Empty, editor.Text);
        Assert.IsTrue(editor.Redo());
        Assert.AreEqual("text", editor.Text);
    }

    [TestMethod]
    public void AReadOnlyEditorUndoesNothing()
    {
        var editor = new TextEditor();
        editor.Document.Insert(0, "text");
        editor.IsReadOnly = true;

        Assert.IsFalse(editor.Undo());
        Assert.AreEqual("text", editor.Text);
    }

    /// <summary>
    /// The options are subclassable, which is how the original lets a host force a value: every
    /// member is virtual and the change notification runs through one overridable method.
    /// </summary>
    [TestMethod]
    public void OptionsCanBeSubclassed()
    {
        var options = new PinnedTabOptions();

        options.ShowTabs = false;
        Assert.IsTrue(options.ShowTabs, "The override did not win.");

        options.ShowSpaces = true;
        Assert.AreEqual(1, options.Notifications, "The change did not run through the overridable raise.");
    }

    /// <summary>A change names the option that moved, so a listener can tell them apart.</summary>
    [TestMethod]
    public void AnOptionChangeCarriesTheOptionThatChanged()
    {
        var options = new TextEditorOptions();
        var raised = new List<MewProperty>();
        options.OptionChanged += (_, option) => raised.Add(option);

        options.ConvertTabsToSpaces = true;
        options.ShowSpaces = true;

        CollectionAssert.AreEqual(
            new[] { TextEditorOptions.ConvertTabsToSpacesProperty, TextEditorOptions.ShowSpacesProperty },
            raised);
    }

    private sealed class PinnedTabOptions : TextEditorOptions
    {
        public int Notifications { get; private set; }

        public override bool ShowTabs
        {
            get => true;
            set => base.ShowTabs = value;
        }

        protected override void OnMewPropertyChanged(MewProperty property)
        {
            Notifications++;
            base.OnMewPropertyChanged(property);
        }
    }
}
