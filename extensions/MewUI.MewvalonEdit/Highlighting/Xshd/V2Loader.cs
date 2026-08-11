using System.Xml;
using System.Xml.Schema;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>Reads .xshd version 2.0 files, recognised by their namespace.</summary>
internal static class V2Loader
{
    public const string NAMESPACE = "http://icsharpcode.net/sharpdevelop/syntaxdefinition/2008";

    private static XmlSchemaSet? _schemaSet;

    private static XmlSchemaSet SchemaSet
    {
        get
        {
            _schemaSet ??= HighlightingLoader.LoadSchemaSet(
                XmlReader.Create(HighlightingResources.OpenStream("ModeV2.xsd")));
            return _schemaSet;
        }
    }

    public static XshdSyntaxDefinition LoadDefinition(XmlReader reader, bool skipValidation)
    {
        reader = HighlightingLoader.GetValidatingReader(reader, true, skipValidation ? null : SchemaSet);
        reader.Read();
        return ParseDefinition(reader);
    }

    private static XshdSyntaxDefinition ParseDefinition(XmlReader reader)
    {
        var definition = new XshdSyntaxDefinition { Name = reader.GetAttribute("name") };
        if (reader.GetAttribute("extensions") is string extensions)
        {
            foreach (string extension in extensions.Split(';'))
            {
                definition.Extensions.Add(extension);
            }
        }
        ParseElements(definition.Elements, reader);
        return definition;
    }

    private static void ParseElements(ICollection<XshdElement> target, XmlReader reader)
    {
        if (reader.IsEmptyElement)
        {
            return;
        }
        while (reader.Read() && reader.NodeType != XmlNodeType.EndElement)
        {
            if (reader.NamespaceURI != NAMESPACE)
            {
                if (!reader.IsEmptyElement)
                {
                    reader.Skip();
                }
                continue;
            }
            switch (reader.Name)
            {
                case "RuleSet": target.Add(ParseRuleSet(reader)); break;
                case "Property": target.Add(ParseProperty(reader)); break;
                case "Color": target.Add(ParseNamedColor(reader)); break;
                case "Keywords": target.Add(ParseKeywords(reader)); break;
                case "Span": target.Add(ParseSpan(reader)); break;
                case "Import": target.Add(ParseImport(reader)); break;
                case "Rule": target.Add(ParseRule(reader)); break;
                default: throw new NotSupportedException("Unknown element " + reader.Name);
            }
        }
    }

    private static XshdElement ParseProperty(XmlReader reader)
    {
        var property = new XshdProperty
        {
            Name = reader.GetAttribute("name"),
            Value = reader.GetAttribute("value")
        };
        SetPosition(property, reader);
        return property;
    }

    private static XshdRuleSet ParseRuleSet(XmlReader reader)
    {
        var ruleSet = new XshdRuleSet
        {
            Name = reader.GetAttribute("name"),
            IgnoreCase = GetBoolAttribute(reader, "ignoreCase")
        };
        SetPosition(ruleSet, reader);
        CheckElementName(reader, ruleSet.Name);
        ParseElements(ruleSet.Elements, reader);
        return ruleSet;
    }

    private static XshdRule ParseRule(XmlReader reader)
    {
        var rule = new XshdRule { ColorReference = ParseColorReference(reader) };
        SetPosition(rule, reader);
        if (!reader.IsEmptyElement)
        {
            reader.Read();
            if (reader.NodeType == XmlNodeType.Text)
            {
                rule.Regex = reader.ReadContentAsString();
                rule.RegexType = XshdRegexType.IgnorePatternWhitespace;
            }
        }
        return rule;
    }

    private static XshdKeywords ParseKeywords(XmlReader reader)
    {
        var keywords = new XshdKeywords { ColorReference = ParseColorReference(reader) };
        SetPosition(keywords, reader);
        reader.Read();
        while (reader.NodeType != XmlNodeType.EndElement)
        {
            keywords.Words.Add(reader.ReadElementContentAsString());
        }
        return keywords;
    }

    private static XshdImport ParseImport(XmlReader reader)
    {
        var import = new XshdImport { RuleSetReference = ParseRuleSetReference(reader) };
        SetPosition(import, reader);
        if (!reader.IsEmptyElement)
        {
            reader.Skip();
        }
        return import;
    }

    private static XshdSpan ParseSpan(XmlReader reader)
    {
        var span = new XshdSpan
        {
            BeginRegex = reader.GetAttribute("begin"),
            EndRegex = reader.GetAttribute("end"),
            Multiline = GetBoolAttribute(reader, "multiline") ?? false,
            SpanColorReference = ParseColorReference(reader),
            RuleSetReference = ParseRuleSetReference(reader)
        };
        SetPosition(span, reader);
        if (reader.IsEmptyElement)
        {
            return span;
        }
        reader.Read();
        while (reader.NodeType != XmlNodeType.EndElement)
        {
            switch (reader.Name)
            {
                case "Begin":
                    if (span.BeginRegex is not null)
                    {
                        throw Error(reader, "Duplicate Begin regex");
                    }
                    span.BeginColorReference = ParseColorReference(reader);
                    span.BeginRegex = reader.ReadElementContentAsString();
                    span.BeginRegexType = XshdRegexType.IgnorePatternWhitespace;
                    break;
                case "End":
                    if (span.EndRegex is not null)
                    {
                        throw Error(reader, "Duplicate End regex");
                    }
                    span.EndColorReference = ParseColorReference(reader);
                    span.EndRegex = reader.ReadElementContentAsString();
                    span.EndRegexType = XshdRegexType.IgnorePatternWhitespace;
                    break;
                case "RuleSet":
                    if (span.RuleSetReference.ReferencedElement is not null)
                    {
                        throw Error(reader, "Cannot specify both inline RuleSet and RuleSet reference");
                    }
                    span.RuleSetReference = new XshdReference<XshdRuleSet>(ParseRuleSet(reader));
                    reader.Read();
                    break;
                default:
                    throw new NotSupportedException("Unknown element " + reader.Name);
            }
        }
        return span;
    }

    private static XshdColor ParseNamedColor(XmlReader reader)
    {
        var color = ParseColorAttributes(reader);
        color.Name = reader.GetAttribute("name");
        CheckElementName(reader, color.Name);
        color.ExampleText = reader.GetAttribute("exampleText");
        return color;
    }

    private static XshdReference<XshdColor> ParseColorReference(XmlReader reader)
    {
        if (reader.GetAttribute("color") is not string color)
        {
            return new XshdReference<XshdColor>(ParseColorAttributes(reader));
        }
        // A slash separates the definition from the element; the last one wins because '/' is legal
        // inside a definition name.
        int pos = color.LastIndexOf('/');
        if (pos >= 0)
        {
            return new XshdReference<XshdColor>(color[..pos], color[(pos + 1)..]);
        }
        return new XshdReference<XshdColor>(null, color);
    }

    private static XshdReference<XshdRuleSet> ParseRuleSetReference(XmlReader reader)
    {
        if (reader.GetAttribute("ruleSet") is not string ruleSet)
        {
            return default;
        }
        int pos = ruleSet.LastIndexOf('/');
        if (pos >= 0)
        {
            return new XshdReference<XshdRuleSet>(ruleSet[..pos], ruleSet[(pos + 1)..]);
        }
        return new XshdReference<XshdRuleSet>(null, ruleSet);
    }

    private static XshdColor ParseColorAttributes(XmlReader reader)
    {
        var color = new XshdColor
        {
            Foreground = XshdColorParser.ParseColor(reader.GetAttribute("foreground")),
            Background = XshdColorParser.ParseColor(reader.GetAttribute("background")),
            FontWeight = XshdColorParser.ParseFontWeight(reader.GetAttribute("fontWeight")),
            Italic = XshdColorParser.ParseItalic(reader.GetAttribute("fontStyle")),
            Underline = GetBoolAttribute(reader, "underline"),
            Strikethrough = GetBoolAttribute(reader, "strikethrough"),
            FontFamily = reader.GetAttribute("fontFamily"),
            FontSize = int.TryParse(reader.GetAttribute("fontSize"), out int size) ? size : null
        };
        SetPosition(color, reader);
        return color;
    }

    private static bool? GetBoolAttribute(XmlReader reader, string name)
        => reader.GetAttribute(name) is string value ? XmlConvert.ToBoolean(value) : null;

    private static void CheckElementName(XmlReader reader, string? name)
    {
        if (name is null)
        {
            return;
        }
        if (name.Length == 0)
        {
            throw Error(reader, "The empty string is not a valid name.");
        }
        if (name.Contains('/'))
        {
            throw Error(reader, "Element names must not contain a slash.");
        }
    }

    private static void SetPosition(XshdElement element, XmlReader reader)
    {
        if (reader is IXmlLineInfo lineInfo)
        {
            element.LineNumber = lineInfo.LineNumber;
            element.ColumnNumber = lineInfo.LinePosition;
        }
    }

    private static Exception Error(XmlReader reader, string message)
        => reader is IXmlLineInfo lineInfo
            ? new HighlightingDefinitionInvalidException(
                HighlightingLoader.FormatExceptionMessage(message, lineInfo.LineNumber, lineInfo.LinePosition))
            : new HighlightingDefinitionInvalidException(message);
}
