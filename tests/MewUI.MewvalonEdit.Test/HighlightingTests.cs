using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Highlighting;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
[DoNotParallelize]
public sealed class HighlightingTests
{
    [TestMethod]
    public void BuiltInCSharpDefinitionClassifiesKeywordsAndStrings()
    {
        var definition = HighlightingManager.Instance.GetDefinition("C#");
        Assert.IsNotNull(definition);

        var elements = HighlightingTestHost.Colorize(definition, "public string Value = \"text\";");

        Assert.IsTrue(elements.Any(element => element.RelativeTextOffset == 0 && element.DocumentLength == 6));
        Assert.IsTrue(elements.Any(element => element.RelativeTextOffset == 7 && element.DocumentLength == 6));
        Assert.IsTrue(elements.Any(element => element.DocumentLength == 6 && element.RelativeTextOffset > 7));
    }

    /// <summary>
    /// Most definitions write fontWeight without naming a family. Writing an empty family in its
    /// place leaves the run with no font, and the text engine rejects the style outright.
    /// </summary>
    [TestMethod]
    public void ABoldScopeWithNoFontFamilyLeavesTheFamilyInherited()
    {
        var definition = HighlightingManager.Instance.GetDefinition("C#");
        Assert.IsNotNull(definition);

        var elements = HighlightingTestHost.Colorize(definition, "public");

        var keyword = elements.First(element => element.RelativeTextOffset == 0);
        Assert.AreEqual(FontWeight.Bold, keyword.TextRunProperties.FontWeight);
        Assert.IsNull(keyword.TextRunProperties.FontFamily);
    }
}

/// <summary>
/// Runs a colorizer over a single-line document through the transformer contract. Dark or light
/// reaches the colorizer through the view's theme, so the host flips the default variant; there is
/// no other seam, which is the point: the colorizer takes no theme input of its own.
/// </summary>
internal static class HighlightingTestHost
{
    public static List<VisualLineElement> Colorize(
        IHighlightingDefinition definition,
        string text,
        bool isDarkTheme = true)
        => Colorize(new HighlightingColorizer(definition), text, isDarkTheme);

    public static List<VisualLineElement> Colorize(
        HighlightingColorizer colorizer,
        string text,
        bool isDarkTheme)
    {
        var previous = ThemeManager.Default;
        ThemeManager.Default = isDarkTheme ? ThemeVariant.Dark : ThemeVariant.Light;
        try
        {
            var editor = new TextEditor { Text = text };
            var elements = new List<VisualLineElement>();
            colorizer.Transform(new SingleLineContext(editor), elements);
            return elements;
        }
        finally
        {
            ThemeManager.Default = previous;
        }
    }

    private sealed class SingleLineContext(TextEditor editor) : ITextRunConstructionContext
    {
        public TextDocument Document => editor.Document;
        public Aprillz.MewUI.MewvalonEdit.Rendering.TextView TextView => editor.TextArea.TextView;
        public DocumentLine CurrentDocumentLine => editor.Document.GetLineByNumber(1);
        public TextRunStyle DefaultStyle => TextRunStyle.Default;
    }
}
