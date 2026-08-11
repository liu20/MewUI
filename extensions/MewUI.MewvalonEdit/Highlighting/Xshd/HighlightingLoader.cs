using System.Xml;
using System.Xml.Schema;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting.Xshd;

/// <summary>Loads .xshd syntax definitions, in either file version.</summary>
public static class HighlightingLoader
{
    /// <summary>Parses an .xshd document into its element tree, without resolving references.</summary>
    public static XshdSyntaxDefinition LoadXshd(XmlReader reader) => LoadXshd(reader, false);

    /// <summary>Parses an .xshd document into its element tree, without resolving references.</summary>
    /// <param name="reader">Reader over the .xshd document.</param>
    /// <param name="skipValidation">Skips schema validation, for definitions known to be well formed.</param>
    public static XshdSyntaxDefinition LoadXshd(XmlReader reader, bool skipValidation)
    {
        ArgumentNullException.ThrowIfNull(reader);
        try
        {
            reader.MoveToContent();
            // Version 2 is the one that carries a namespace; anything else is read as version 1.
            return reader.NamespaceURI == V2Loader.NAMESPACE
                ? V2Loader.LoadDefinition(reader, skipValidation)
                : V1Loader.LoadDefinition(reader, skipValidation);
        }
        catch (XmlSchemaException error)
        {
            throw WrapException(error, error.LineNumber, error.LinePosition);
        }
        catch (XmlException error)
        {
            throw WrapException(error, error.LineNumber, error.LinePosition);
        }
    }

    /// <summary>Builds a usable definition from a parsed .xshd document.</summary>
    /// <param name="syntaxDefinition">The parsed .xshd document.</param>
    /// <param name="resolver">Resolves references to other definitions, or null to reject them.</param>
    public static IHighlightingDefinition Load(
        XshdSyntaxDefinition syntaxDefinition,
        IHighlightingDefinitionReferenceResolver? resolver)
    {
        ArgumentNullException.ThrowIfNull(syntaxDefinition);
        return new XmlHighlightingDefinition(syntaxDefinition, resolver);
    }

    /// <inheritdoc cref="Load(XshdSyntaxDefinition, IHighlightingDefinitionReferenceResolver)"/>
    public static IHighlightingDefinition Load(
        XmlReader reader,
        IHighlightingDefinitionReferenceResolver? resolver)
        => Load(LoadXshd(reader), resolver);

    /// <inheritdoc cref="Load(XshdSyntaxDefinition, IHighlightingDefinitionReferenceResolver)"/>
    public static IHighlightingDefinition Load(
        TextReader reader,
        IHighlightingDefinitionReferenceResolver? resolver = null)
    {
        ArgumentNullException.ThrowIfNull(reader);
        using var xmlReader = XmlReader.Create(reader);
        return Load(xmlReader, resolver);
    }

    /// <inheritdoc cref="Load(XshdSyntaxDefinition, IHighlightingDefinitionReferenceResolver)"/>
    public static IHighlightingDefinition Load(
        string xshd,
        IHighlightingDefinitionReferenceResolver? resolver = null)
    {
        ArgumentNullException.ThrowIfNull(xshd);
        using var reader = new StringReader(xshd);
        return Load(reader, resolver);
    }

    internal static string FormatExceptionMessage(string message, int lineNumber, int linePosition)
        => lineNumber <= 0
            ? message
            : $"Error at position (line {lineNumber}, column {linePosition}):\n{message}";

    internal static XmlReader GetValidatingReader(XmlReader input, bool ignoreWhitespace, XmlSchemaSet? schemaSet)
    {
        var settings = new XmlReaderSettings
        {
            CloseInput = true,
            IgnoreComments = true,
            IgnoreWhitespace = ignoreWhitespace
        };
        if (schemaSet is not null)
        {
            settings.Schemas = schemaSet;
            settings.ValidationType = ValidationType.Schema;
        }
        return XmlReader.Create(input, settings);
    }

    internal static XmlSchemaSet LoadSchemaSet(XmlReader schemaInput)
    {
        var schemaSet = new XmlSchemaSet();
        schemaSet.Add(null, schemaInput);
        schemaSet.ValidationEventHandler += (sender, args)
            => throw new HighlightingDefinitionInvalidException(args.Message);
        return schemaSet;
    }

    private static Exception WrapException(Exception error, int lineNumber, int linePosition)
        => new HighlightingDefinitionInvalidException(
            FormatExceptionMessage(error.Message, lineNumber, linePosition), error);
}
