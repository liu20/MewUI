using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Highlighting;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class DocumentHighlighterTests
{
    private static IHighlightingDefinition CSharp()
    {
        var definition = HighlightingManager.Instance.GetDefinition("C#");
        Assert.IsNotNull(definition);
        return definition;
    }

    /// <summary>
    /// A span carries its own rule set, so the same quoted text is a field name inside an object
    /// and a plain string inside an expression. Flattening the sets into one would make both the
    /// same colour.
    /// </summary>
    [TestMethod]
    public void NestedRuleSetsTellAJsonKeyFromAStringValue()
    {
        var definition = HighlightingManager.Instance.GetDefinition("Json");
        Assert.IsNotNull(definition);
        var document = new TextDocument("""{ "name": "value" }""");
        using var highlighter = new DocumentHighlighter(document, definition);

        var line = highlighter.HighlightLine(1);

        var key = line.Sections.First(section => section.Color.Name == "FieldName");
        var value = line.Sections.First(section => section.Color.Name == "String");
        Assert.AreEqual(2, key.Offset);
        Assert.AreEqual(10, value.Offset);
        Assert.AreNotEqual(key.Color.Foreground, value.Color.Foreground);
    }

    [TestMethod]
    public void BlockCommentKeepsItsColorAcrossLines()
    {
        var document = new TextDocument("int a; /* open\nstill comment\nend */ int b;");
        using var highlighter = new DocumentHighlighter(document, CSharp());

        var middle = highlighter.HighlightLine(2);

        var section = middle.Sections.Single();
        Assert.AreEqual(document.GetLineByNumber(2).Offset, section.Offset);
        Assert.AreEqual("still comment".Length, section.Length);
        Assert.AreEqual("Comment", section.Color.Name);
    }

    [TestMethod]
    public void TextAfterTheBlockCommentClosesIsHighlightedNormally()
    {
        var document = new TextDocument("/* comment\n*/ int value;");
        using var highlighter = new DocumentHighlighter(document, CSharp());

        var second = highlighter.HighlightLine(2);

        Assert.IsTrue(second.Sections.Any(section =>
            section.Color.Name == "ValueTypeKeywords" && section.Length == 3),
            "The type keyword after the closing delimiter should be colored by the main rule set.");
    }

    [TestMethod]
    public void VerbatimStringSpansLines()
    {
        var document = new TextDocument("var path = @\"C:\\one\nstill string\";");
        using var highlighter = new DocumentHighlighter(document, CSharp());

        var second = highlighter.HighlightLine(2);

        int lineStart = document.GetLineByNumber(2).Offset;
        Assert.IsTrue(second.Sections.Any(section => section.Offset == lineStart),
            "The continuation line of a verbatim string must be colored.");
    }

    [TestMethod]
    public void ClosingACommentInvalidatesTheFollowingLines()
    {
        var document = new TextDocument("/* open\nbody\ntail");
        using var highlighter = new DocumentHighlighter(document, CSharp());
        int changedFrom = 0;
        highlighter.HighlightingStateChanged += (from, _) => changedFrom = from;
        highlighter.HighlightLine(3);

        // Close the comment on line 1; lines below must be rescanned.
        document.Insert(7, " */");
        highlighter.HighlightLine(1);

        Assert.AreEqual(2, changedFrom);
    }

    [TestMethod]
    public void DefinitionsWithoutSpansStillHighlight()
    {
        var document = new TextDocument("public int value = 3;");
        using var highlighter = new DocumentHighlighter(document, CSharp());

        var line = highlighter.HighlightLine(1);

        Assert.IsTrue(line.Sections.Any(section => section.Offset == 0 && section.Length == 6));
    }
}
