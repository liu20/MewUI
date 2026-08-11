using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Highlighting;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class SelectionAndServiceTests
{
    [TestMethod]
    public void SelectionAppearanceIsUntouchedUntilItIsSet()
    {
        var editor = new TextEditor { Text = "select me" };
        int builtIn = editor.TextArea.TextView.Layers.Count;

        // Reading must not install the replacement, or the theme's selection would be dropped by
        // anyone who merely inspects the property.
        Assert.IsNull(editor.TextArea.SelectionBrush);
        Assert.HasCount(builtIn, editor.TextArea.TextView.Layers);
    }

    [TestMethod]
    public void SettingSelectionBrushReplacesTheHostSelectionLayer()
    {
        var editor = new TextEditor { Text = "select me" };
        int builtIn = editor.TextArea.TextView.Layers.Count;

        editor.TextArea.SelectionBrush = Color.FromRgb(0x30, 0x60, 0xC0);

        // Replacement, not insertion: the count stays and the host stops painting its selection.
        Assert.HasCount(builtIn, editor.TextArea.TextView.Layers);
        Assert.AreEqual(Color.FromRgb(0x30, 0x60, 0xC0), editor.TextArea.SelectionBrush);
    }

    [TestMethod]
    public void ClearingSelectionColorsDoesNotInstallAnEmptyLayer()
    {
        var editor = new TextEditor { Text = "select me" };

        // Assigning null must not replace the host's selection pass with a layer that paints
        // nothing. Replacement keeps the layer count, so the presence of the layer is the check.
        editor.TextArea.SelectionBrush = null;
        editor.TextArea.SelectionForeground = null;
        editor.TextArea.SelectionBorder = null;

        Assert.IsFalse(editor.TextArea.TextView.Layers.Any(static layer => layer is SelectionLayer));
    }

    [TestMethod]
    public void SettingAColorInstallsTheLayerAndClearingItKeepsIt()
    {
        var editor = new TextEditor { Text = "select me" };

        editor.TextArea.SelectionBrush = Color.FromRgb(1, 2, 3);
        Assert.IsTrue(editor.TextArea.TextView.Layers.Any(static layer => layer is SelectionLayer));

        // Once installed it stays, and falls back to the theme rather than painting nothing.
        editor.TextArea.SelectionBrush = null;
        Assert.IsTrue(editor.TextArea.TextView.Layers.Any(static layer => layer is SelectionLayer));
    }

    [TestMethod]
    public void SelectionColoursCanBeBound()
    {
        var editor = new TextEditor { Text = "select me" };
        var colour = new ObservableValue<Color?>(null);

        editor.TextArea.SetBinding(
            Aprillz.MewUI.MewvalonEdit.Editing.TextArea.SelectionBrushProperty, colour);

        // A binding that carries no colour yet must not install the replacement either, for the
        // same reason a read must not: it would drop the theme's selection.
        Assert.IsFalse(editor.TextArea.TextView.Layers.Any(static layer => layer is SelectionLayer));

        colour.Value = Color.FromRgb(0x30, 0x60, 0xC0);

        Assert.AreEqual(Color.FromRgb(0x30, 0x60, 0xC0), editor.TextArea.SelectionBrush);
        Assert.IsTrue(editor.TextArea.TextView.Layers.Any(static layer => layer is SelectionLayer));
    }

    [TestMethod]
    public void SelectionPropertiesShareOneLayer()
    {
        var editor = new TextEditor { Text = "select me" };
        int builtIn = editor.TextArea.TextView.Layers.Count;

        editor.TextArea.SelectionBrush = Color.FromRgb(1, 2, 3);
        editor.TextArea.SelectionForeground = Color.FromRgb(4, 5, 6);
        editor.TextArea.SelectionBorder = Color.FromRgb(7, 8, 9);

        Assert.HasCount(builtIn, editor.TextArea.TextView.Layers);
        Assert.AreEqual(Color.FromRgb(4, 5, 6), editor.TextArea.SelectionForeground);
        Assert.AreEqual(Color.FromRgb(7, 8, 9), editor.TextArea.SelectionBorder);
    }

    [TestMethod]
    public void SyntaxHighlightingRegistersTheHighlighterAsAViewService()
    {
        var editor = new TextEditor { Text = "class A { }" };

        Assert.IsNull(editor.TextArea.TextView.GetService<IHighlighter>());

        editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinition("C#");
        Assert.IsNotNull(editor.TextArea.TextView.Services.GetService<IHighlighter>());

        editor.SyntaxHighlighting = null;
        Assert.IsNull(editor.TextArea.TextView.Services.GetService<IHighlighter>());
    }

    [TestMethod]
    public void MailLinksCarryTheMailtoScheme()
    {
        var editor = new TextEditor { Text = "write to team@example.com now" };
        var generator = new MailLinkElementGenerator();
        editor.TextArea.TextView.ElementGenerators.Add(generator);

        generator.StartGeneration(new Context(editor));
        var element = (VisualLineLinkText)generator.ConstructElement(
            editor.Text.IndexOf("team", StringComparison.Ordinal))!;
        generator.FinishGeneration();

        Assert.AreEqual("mailto:team@example.com", element.NavigateUri);
    }

    private sealed class Context(TextEditor editor) : ITextRunConstructionContext
    {
        public Aprillz.MewUI.MewvalonEdit.Document.TextDocument Document => editor.Document;
        public Aprillz.MewUI.MewvalonEdit.Rendering.TextView TextView => editor.TextArea.TextView;
        public Aprillz.MewUI.MewvalonEdit.Document.DocumentLine CurrentDocumentLine
            => editor.Document.GetLineByNumber(1);
        public TextRunStyle DefaultStyle => new(editor.FontFamily, editor.FontSize, editor.FontWeight);
    }
}
