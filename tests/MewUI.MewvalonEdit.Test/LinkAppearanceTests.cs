using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit;
using Aprillz.MewUI.MewvalonEdit.Rendering;
using Aprillz.MewUI.Text;

namespace MewUI.MewvalonEdit.Test;

/// <summary>
/// Link appearance belongs to the view, not to the generator that happened to build the element,
/// so every link in the document follows one setting.
/// </summary>
[TestClass]
public sealed class LinkAppearanceTests
{
    private const string TEXT = "see https://example.com now";

    [TestMethod]
    public void TheViewSuppliesTheLinkColour()
    {
        var editor = BuildEditor();
        editor.TextArea.TextView.LinkTextForegroundBrush = Color.FromRgb(0x10, 0x20, 0x30);

        var span = Classify(editor).Single();

        Assert.AreEqual(Color.FromRgb(0x10, 0x20, 0x30), span.Foreground);
    }

    [TestMethod]
    public void ChangingTheColourAfterAPaintTakesEffect()
    {
        var editor = BuildEditor();
        editor.TextArea.TextView.LinkTextForegroundBrush = Color.FromRgb(1, 1, 1);
        _ = Classify(editor);

        // The scan cache is keyed on the document version, which a colour change does not move.
        editor.TextArea.TextView.LinkTextForegroundBrush = Color.FromRgb(2, 2, 2);

        Assert.AreEqual(Color.FromRgb(2, 2, 2), Classify(editor).Single().Foreground);
    }

    [TestMethod]
    public void ClearingTheUnderlineLeavesTheLinkColoured()
    {
        var editor = BuildEditor();
        editor.TextArea.TextView.LinkTextForegroundBrush = Color.FromRgb(9, 9, 9);
        editor.TextArea.TextView.LinkTextUnderline = false;

        var span = Classify(editor).Single();

        Assert.AreEqual(TextDecoration.None, span.Decoration);
        Assert.AreEqual(Color.FromRgb(9, 9, 9), span.Foreground);
    }

    [TestMethod]
    public void EveryGeneratorFollowsTheSameViewSetting()
    {
        var editor = new TextEditor { Text = "see https://example.com or mail team@example.com" };
        editor.TextArea.TextView.ElementGenerators.Add(new LinkElementGenerator());
        editor.TextArea.TextView.ElementGenerators.Add(new MailLinkElementGenerator());
        editor.TextArea.TextView.LinkTextForegroundBrush = Color.FromRgb(8, 8, 8);

        var spans = Classify(editor);

        Assert.HasCount(2, spans);
        Assert.IsTrue(spans.TrueForAll(span => span.Foreground == Color.FromRgb(8, 8, 8)));
    }

    [TestMethod]
    public void ViewAppearanceIsBindable()
    {
        var editor = BuildEditor();
        var colour = new ObservableValue<Color?>(Color.FromRgb(4, 4, 4));

        editor.TextArea.TextView.SetBinding(TextView.LinkTextForegroundBrushProperty, colour);
        colour.Value = Color.FromRgb(5, 5, 5);

        Assert.AreEqual(Color.FromRgb(5, 5, 5), Classify(editor).Single().Foreground);
    }

    [TestMethod]
    public void TheViewOwnsTheNonPrintableCharacterColour()
    {
        var editor = BuildEditor();
        editor.TextArea.TextView.NonPrintableCharacterBrush = Color.FromRgb(6, 6, 6);

        Assert.AreEqual(Color.FromRgb(6, 6, 6), editor.WhitespaceMarkerColor);
    }

    private static TextEditor BuildEditor()
    {
        var editor = new TextEditor { Text = TEXT };
        editor.TextArea.TextView.ElementGenerators.Add(new LinkElementGenerator());
        return editor;
    }

    private static List<TextPaintSpan> Classify(TextEditor editor)
    {
        var spans = new List<TextPaintSpan>();
        var context = new TextClassificationContext(
            new LogicalTextLine(0, 0, editor.Text.Length, editor.Text.Length),
            editor.Text.AsMemory(),
            IdentityTextOffsetMap.Instance);
        foreach (var classifier in editor.TextArea.TextView.Extensions.Classifiers)
        {
            classifier.Classify(in context, spans);
        }
        return spans;
    }
}
