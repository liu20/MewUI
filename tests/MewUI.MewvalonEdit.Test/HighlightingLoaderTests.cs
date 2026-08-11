using Aprillz.MewUI;
using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.MewvalonEdit.Highlighting;
using Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class HighlightingLoaderTests
{
    private const string SAMPLE = """
        <?xml version="1.0"?>
        <SyntaxDefinition name="Mini" extensions=".mini" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
          <Color name="Comment" foreground="Green" />
          <Color name="String" foreground="#A31515" />
          <RuleSet>
            <Span color="Comment" multiline="true">
              <Begin>/\*</Begin>
              <End>\*/</End>
            </Span>
            <Span color="Comment">
              <Begin>//</Begin>
            </Span>
            <Keywords fontWeight="bold" foreground="Blue">
              <Word>if</Word>
              <Word>else</Word>
            </Keywords>
            <Rule color="String">"[^"]*"</Rule>
          </RuleSet>
        </SyntaxDefinition>
        """;

    [TestMethod]
    public void LoadsNameColorsRulesAndSpans()
    {
        var definition = HighlightingLoader.Load(SAMPLE);

        Assert.AreEqual("Mini", definition.Name);
        Assert.AreEqual(Color.FromRgb(0, 128, 0), definition.GetNamedColor("Comment")?.Foreground);
        Assert.AreEqual(Color.FromHex("#A31515"), definition.GetNamedColor("String")?.Foreground);
        // Two spans: the block comment, and the line comment, whose missing End becomes "$".
        Assert.HasCount(2, definition.MainRuleSet.Spans);
        Assert.HasCount(2, definition.MainRuleSet.Rules);
    }

    [TestMethod]
    public void LoadedSpansCarryAcrossLines()
    {
        var definition = HighlightingLoader.Load(SAMPLE);
        var document = new TextDocument("/* start\ninside\n*/ if");
        using var highlighter = new DocumentHighlighter(document, definition);

        var second = highlighter.HighlightLine(2);

        var section = second.Sections.Single();
        Assert.AreEqual(document.GetLineByNumber(2).Offset, section.Offset);
        Assert.AreEqual("inside".Length, section.Length);
        Assert.AreEqual(Color.FromRgb(0, 128, 0), section.Color.Foreground);
    }

    [TestMethod]
    public void KeywordListsBecomeWordBoundedRules()
    {
        var definition = HighlightingLoader.Load(SAMPLE);
        var document = new TextDocument("if x");
        using var highlighter = new DocumentHighlighter(document, definition);

        var line = highlighter.HighlightLine(1);

        var keyword = line.Sections.Single(section => section.Offset == 0);
        Assert.AreEqual(2, keyword.Length);
        Assert.AreEqual(FontWeight.Bold, keyword.Color.FontWeight);
    }

    /// <summary>
    /// The definitions AvalonEdit ships write element-body patterns free-form with '#' comments,
    /// which only parse under IgnorePatternWhitespace.
    /// </summary>
    [TestMethod]
    public void ElementBodyPatternsAllowWhitespaceAndComments()
    {
        const string SOURCE = """
            <?xml version="1.0"?>
            <SyntaxDefinition name="Spaced" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="Ident" foreground="Blue" />
              <RuleSet>
                <Rule color="Ident">
                  \b
                  [\d\w_]+  # an identifier
                  (?=\s*\()  # followed by an opening parenthesis
                </Rule>
              </RuleSet>
            </SyntaxDefinition>
            """;

        var definition = HighlightingLoader.Load(SOURCE);
        var document = new TextDocument("value Compute()");
        using var highlighter = new DocumentHighlighter(document, definition);

        var line = highlighter.HighlightLine(1);

        var call = line.Sections.Single();
        Assert.AreEqual(6, call.Offset);
        Assert.AreEqual("Compute".Length, call.Length);
    }

    /// <summary>
    /// A rule set written inside a Span belongs to that span: it colors what it matches within the
    /// span and leaves identical text outside it alone.
    /// </summary>
    [TestMethod]
    public void NestedRuleSetsApplyOnlyInsideTheirSpan()
    {
        const string SOURCE = """
            <?xml version="1.0"?>
            <SyntaxDefinition name="Nested" xmlns="http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008">
              <Color name="String" foreground="Maroon" />
              <Color name="Escape" foreground="Teal" />
              <RuleSet>
                <Span color="String">
                  <Begin>"</Begin>
                  <End>"</End>
                  <RuleSet>
                    <Rule color="Escape">\\.</Rule>
                  </RuleSet>
                </Span>
              </RuleSet>
            </SyntaxDefinition>
            """;

        var definition = HighlightingLoader.Load(SOURCE);
        Assert.IsNotNull(definition.MainRuleSet.Spans.Single().RuleSet, "The span must own the nested set.");
        Assert.IsEmpty(definition.MainRuleSet.Rules, "The nested set must not leak into the enclosing one.");

        var document = new TextDocument("""a\nb "c\nd" """);
        using var highlighter = new DocumentHighlighter(document, definition);

        var line = highlighter.HighlightLine(1);

        var escapes = line.Sections
            .Where(section => section.Color.Foreground == Color.FromRgb(0, 128, 128))
            .ToArray();
        var escape = escapes.Single();
        // The text holds the same backslash pair twice; only the one between the quotes is an escape.
        Assert.AreEqual(document.Text.LastIndexOf('\\'), escape.Offset);
        Assert.AreEqual(2, escape.Length);
    }

    [TestMethod]
    public void InvalidXmlIsReportedAsADefinitionError()
        => Assert.ThrowsExactly<HighlightingDefinitionInvalidException>(
            () => HighlightingLoader.Load("<SyntaxDefinition"));

    /// <summary>
    /// The shipped definitions are parsed on first use, so nothing else would notice a file that
    /// fails to load or a cross-definition reference that cannot be resolved.
    /// </summary>
    [TestMethod]
    public void EveryBuiltInDefinitionLoadsAndResolvesItsReferences()
    {
        foreach (var definition in HighlightingManager.Instance.HighlightingDefinitions)
        {
            Assert.IsNotNull(definition.MainRuleSet, definition.Name);
        }
    }

    [TestMethod]
    public void RegisteredDefinitionsResolveByExtension()
    {
        var definition = HighlightingLoader.Load(SAMPLE);
        HighlightingManager.Instance.RegisterHighlighting("Mini", [".mini"], definition);

        Assert.AreSame(definition, HighlightingManager.Instance.GetDefinitionByExtension(".mini"));
    }
}
