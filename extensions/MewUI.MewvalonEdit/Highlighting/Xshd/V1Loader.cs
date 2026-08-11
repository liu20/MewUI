using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Schema;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>Reads .xshd version 1.0 files, which carry no namespace.</summary>
internal sealed class V1Loader
{
    private static XmlSchemaSet? _schemaSet;

    private char _ruleSetEscapeCharacter;

    private static XmlSchemaSet SchemaSet
    {
        get
        {
            _schemaSet ??= HighlightingLoader.LoadSchemaSet(
                XmlReader.Create(HighlightingResources.OpenStream("ModeV1.xsd")));
            return _schemaSet;
        }
    }

    public static XshdSyntaxDefinition LoadDefinition(XmlReader reader, bool skipValidation)
    {
        reader = HighlightingLoader.GetValidatingReader(reader, false, skipValidation ? null : SchemaSet);
        var document = new XmlDocument();
        document.Load(reader);
        return new V1Loader().ParseDefinition(document.DocumentElement!);
    }

    private XshdSyntaxDefinition ParseDefinition(XmlElement syntaxDefinition)
    {
        var definition = new XshdSyntaxDefinition { Name = GetAttributeOrNull(syntaxDefinition, "name") };
        if (syntaxDefinition.HasAttribute("extensions"))
        {
            foreach (string extension in syntaxDefinition.GetAttribute("extensions").Split(';', '|'))
            {
                definition.Extensions.Add(extension);
            }
        }

        XshdRuleSet? mainRuleSetElement = null;
        foreach (XmlElement element in syntaxDefinition.GetElementsByTagName("RuleSet"))
        {
            var ruleSet = ImportRuleSet(element);
            definition.Elements.Add(ruleSet);
            if (ruleSet.Name is null)
            {
                mainRuleSetElement = ruleSet;
            }

            if (syntaxDefinition["Digits"] is XmlElement digits)
            {
                const string OPTIONAL_EXPONENT = @"([eE][+-]?[0-9]+)?";
                const string FLOATING_POINT = @"\.[0-9]+";
                ruleSet.Elements.Add(new XshdRule
                {
                    ColorReference = GetColorReference(digits),
                    RegexType = XshdRegexType.IgnorePatternWhitespace,
                    Regex = @"\b0[xX][0-9a-fA-F]+"
                        + @"|"
                        + @"(\b\d+(" + FLOATING_POINT + ")?"
                        + @"|" + FLOATING_POINT + ")"
                        + OPTIONAL_EXPONENT
                });
            }
        }

        if (syntaxDefinition.HasAttribute("extends") && mainRuleSetElement is not null)
        {
            // extends="HTML" is an import of that definition's main rule set.
            mainRuleSetElement.Elements.Add(new XshdImport
            {
                RuleSetReference = new XshdReference<XshdRuleSet>(
                    syntaxDefinition.GetAttribute("extends"), string.Empty)
            });
        }
        return definition;
    }

    private XshdRuleSet ImportRuleSet(XmlElement element)
    {
        var ruleSet = new XshdRuleSet { Name = GetAttributeOrNull(element, "name") };

        _ruleSetEscapeCharacter = element.HasAttribute("escapecharacter")
            ? element.GetAttribute("escapecharacter")[0]
            : '\0';

        if (element.HasAttribute("reference"))
        {
            ruleSet.Elements.Add(new XshdImport
            {
                RuleSetReference = new XshdReference<XshdRuleSet>(
                    element.GetAttribute("reference"), string.Empty)
            });
        }
        ruleSet.IgnoreCase = GetBoolAttribute(element, "ignorecase");

        foreach (XmlElement keywordElement in element.GetElementsByTagName("KeyWords"))
        {
            var keywords = new XshdKeywords { ColorReference = GetColorReference(keywordElement) };
            // Old definitions contain empty keywords and empty keyword groups.
            foreach (XmlElement node in keywordElement.GetElementsByTagName("Key"))
            {
                string word = node.GetAttribute("word");
                if (!string.IsNullOrEmpty(word))
                {
                    keywords.Words.Add(word);
                }
            }
            if (keywords.Words.Count > 0)
            {
                ruleSet.Elements.Add(keywords);
            }
        }

        foreach (XmlElement span in element.GetElementsByTagName("Span"))
        {
            ruleSet.Elements.Add(ImportSpan(span));
        }
        foreach (XmlElement mark in element.GetElementsByTagName("MarkPrevious"))
        {
            ruleSet.Elements.Add(ImportMarkPrevNext(mark, markFollowing: false));
        }
        foreach (XmlElement mark in element.GetElementsByTagName("MarkFollowing"))
        {
            ruleSet.Elements.Add(ImportMarkPrevNext(mark, markFollowing: true));
        }
        return ruleSet;
    }

    private XshdSpan ImportSpan(XmlElement element)
    {
        var span = new XshdSpan();
        if (element.HasAttribute("rule"))
        {
            span.RuleSetReference = new XshdReference<XshdRuleSet>(null, element.GetAttribute("rule"));
        }
        char escapeCharacter = element.HasAttribute("escapecharacter")
            ? element.GetAttribute("escapecharacter")[0]
            : _ruleSetEscapeCharacter;
        span.Multiline = !(GetBoolAttribute(element, "stopateol") ?? false);
        span.SpanColorReference = GetColorReference(element);

        var begin = element["Begin"]!;
        span.BeginRegexType = XshdRegexType.IgnorePatternWhitespace;
        span.BeginRegex = ImportRegex(
            begin.InnerText, GetBoolAttribute(begin, "singleword") ?? false, GetBoolAttribute(begin, "startofline"));
        span.BeginColorReference = GetColorReference(begin);

        string endElementText = string.Empty;
        if (element["End"] is XmlElement end)
        {
            span.EndRegexType = XshdRegexType.IgnorePatternWhitespace;
            endElementText = end.InnerText;
            span.EndRegex = ImportRegex(endElementText, GetBoolAttribute(end, "singleword") ?? false, null);
            span.EndColorReference = GetColorReference(end);
        }

        if (escapeCharacter != '\0')
        {
            var ruleSet = new XshdRuleSet();
            if (endElementText.Length == 1 && endElementText[0] == escapeCharacter)
            {
                // ""-style escape.
                ruleSet.Elements.Add(new XshdSpan
                {
                    BeginRegex = Regex.Escape(endElementText + endElementText),
                    EndRegex = string.Empty
                });
            }
            else
            {
                // \"-style escape.
                ruleSet.Elements.Add(new XshdSpan
                {
                    BeginRegex = Regex.Escape(escapeCharacter.ToString()),
                    EndRegex = "."
                });
            }
            if (span.RuleSetReference.ReferencedElement is not null)
            {
                ruleSet.Elements.Add(new XshdImport { RuleSetReference = span.RuleSetReference });
            }
            span.RuleSetReference = new XshdReference<XshdRuleSet>(ruleSet);
        }
        return span;
    }

    private static XshdRule ImportMarkPrevNext(XmlElement element, bool markFollowing)
    {
        bool markMarker = GetBoolAttribute(element, "markmarker") ?? false;
        string what = Regex.Escape(element.InnerText);
        const string IDENTIFIER = @"[\d\w_]+";
        const string WHITESPACE = @"\s*";

        string regex;
        if (markFollowing)
        {
            regex = markMarker
                ? what + WHITESPACE + IDENTIFIER
                : "(?<=(" + what + WHITESPACE + "))" + IDENTIFIER;
        }
        else
        {
            regex = markMarker
                ? IDENTIFIER + WHITESPACE + what
                : IDENTIFIER + "(?=(" + WHITESPACE + what + "))";
        }
        return new XshdRule
        {
            ColorReference = GetColorReference(element),
            Regex = regex,
            RegexType = XshdRegexType.IgnorePatternWhitespace
        };
    }

    /// <summary>
    /// Translates a version 1 pattern, which escapes its literals and spells lookaround with '@'
    /// sequences, into a plain regex.
    /// </summary>
    private static string ImportRegex(string expression, bool singleWord, bool? startOfLine)
    {
        var pattern = new StringBuilder();
        if (startOfLine is bool atStart)
        {
            pattern.Append(atStart ? @"(?<=(^\s*))" : @"(?<!(^\s*))");
        }
        else if (singleWord)
        {
            pattern.Append(@"\b");
        }

        for (int i = 0; i < expression.Length; i++)
        {
            char current = expression[i];
            if (current != '@')
            {
                pattern.Append(Regex.Escape(current.ToString()));
                continue;
            }
            i++;
            if (i == expression.Length)
            {
                throw new HighlightingDefinitionInvalidException(
                    "Unexpected end of @ sequence, use @@ to look for a single @.");
            }
            switch (expression[i])
            {
                case 'C':
                    pattern.Append(@"[^\w\d_]");
                    break;
                case '!':
                    pattern.Append("(?!(").Append(Regex.Escape(ReadUntilAt(expression, ref i))).Append("))");
                    break;
                case '-':
                    pattern.Append("(?<!(").Append(Regex.Escape(ReadUntilAt(expression, ref i))).Append("))");
                    break;
                case '@':
                    pattern.Append('@');
                    break;
                default:
                    throw new HighlightingDefinitionInvalidException("Unknown character in @ sequence.");
            }
        }
        if (singleWord)
        {
            pattern.Append(@"\b");
        }
        return pattern.ToString();
    }

    private static string ReadUntilAt(string expression, ref int index)
    {
        var text = new StringBuilder();
        index++;
        while (index < expression.Length && expression[index] != '@')
        {
            text.Append(expression[index++]);
        }
        return text.ToString();
    }

    private static XshdColor? GetColorFromElement(XmlElement element)
    {
        if (!element.HasAttribute("bold") && !element.HasAttribute("italic")
            && !element.HasAttribute("color") && !element.HasAttribute("bgcolor"))
        {
            return null;
        }
        var color = new XshdColor();
        if (element.HasAttribute("bold"))
        {
            color.FontWeight = XmlConvert.ToBoolean(element.GetAttribute("bold"))
                ? FontWeight.Bold
                : FontWeight.Normal;
        }
        if (element.HasAttribute("italic"))
        {
            color.Italic = XmlConvert.ToBoolean(element.GetAttribute("italic"));
        }
        if (element.HasAttribute("color"))
        {
            color.Foreground = XshdColorParser.ParseColor(element.GetAttribute("color"));
        }
        if (element.HasAttribute("bgcolor"))
        {
            color.Background = XshdColorParser.ParseColor(element.GetAttribute("bgcolor"));
        }
        return color;
    }

    private static XshdReference<XshdColor> GetColorReference(XmlElement element)
        => GetColorFromElement(element) is XshdColor color
            ? new XshdReference<XshdColor>(color)
            : default;

    private static string? GetAttributeOrNull(XmlElement element, string name)
        => element.HasAttribute(name) ? element.GetAttribute(name) : null;

    private static bool? GetBoolAttribute(XmlElement element, string name)
        => element.HasAttribute(name) ? XmlConvert.ToBoolean(element.GetAttribute(name)) : null;
}
