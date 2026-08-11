using System.Xml;
using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Folding;

/// <summary>Builds foldings for XML elements and comments that span more than one line.</summary>
public class XmlFoldingStrategy
{
    /// <summary>Shows the opening tag's attributes in the collapsed placeholder.</summary>
    public bool ShowAttributesWhenFolded { get; set; }

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(manager);
        var foldings = CreateNewFoldings(document, out int firstErrorOffset);
        manager.UpdateFoldings(foldings, firstErrorOffset);
    }

    /// <summary>Foldings for the document, sorted by start offset. Parsing stops at the first error.</summary>
    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        ArgumentNullException.ThrowIfNull(document);
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var stack = new Stack<(int Offset, int Line, string Placeholder)>();
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Ignore,
            ConformanceLevel = ConformanceLevel.Fragment,
            CheckCharacters = false
        };

        try
        {
            using var reader = XmlReader.Create(new StringReader(document.Text), settings);
            var lineInfo = (IXmlLineInfo)reader;
            while (reader.Read())
            {
                switch (reader.NodeType)
                {
                    case XmlNodeType.Element when !reader.IsEmptyElement:
                        // The reported position is the character after '<'.
                        stack.Push((
                            GetOffset(document, lineInfo) - 1,
                            lineInfo.LineNumber,
                            CreatePlaceholder(reader)));
                        break;
                    case XmlNodeType.EndElement when stack.Count > 0:
                    {
                        (int startOffset, int startLine, string placeholder) = stack.Pop();
                        if (lineInfo.LineNumber > startLine)
                        {
                            int endOffset = FindTagEnd(document, GetOffset(document, lineInfo));
                            foldings.Add(new NewFolding(startOffset, endOffset) { Name = placeholder });
                        }
                        break;
                    }
                    case XmlNodeType.Comment:
                    {
                        int startOffset = GetOffset(document, lineInfo) - "<!--".Length;
                        string value = reader.Value;
                        if (value.Contains('\n'))
                        {
                            string firstLine = value.Split('\n')[0].TrimEnd('\r');
                            foldings.Add(new NewFolding(
                                startOffset,
                                Math.Min(document.TextLength, startOffset + "<!--".Length + value.Length + "-->".Length))
                            {
                                Name = "<!--" + firstLine + "-->"
                            });
                        }
                        break;
                    }
                }
            }
        }
        catch (XmlException exception)
        {
            firstErrorOffset = exception.LineNumber >= 1
                ? document.GetOffset(
                    Math.Min(exception.LineNumber, document.LineCount),
                    Math.Max(1, exception.LinePosition))
                : 0;
        }

        foldings.Sort(static (left, right) => left.StartOffset.CompareTo(right.StartOffset));
        return foldings;
    }

    private string CreatePlaceholder(XmlReader reader)
    {
        var builder = new System.Text.StringBuilder("<", 32);
        builder.Append(reader.Name);
        if (ShowAttributesWhenFolded && reader.HasAttributes)
        {
            while (reader.MoveToNextAttribute())
            {
                builder.Append(' ').Append(reader.Name).Append("=\"").Append(reader.Value).Append('"');
            }
            reader.MoveToElement();
        }
        builder.Append('>');
        return builder.ToString();
    }

    private static int GetOffset(TextDocument document, IXmlLineInfo lineInfo)
        => document.GetOffset(
            Math.Min(lineInfo.LineNumber, document.LineCount),
            Math.Max(1, lineInfo.LinePosition));

    private static int FindTagEnd(TextDocument document, int offset)
    {
        for (int index = offset; index < document.TextLength; index++)
        {
            if (document.GetCharAt(index) == '>')
            {
                return index + 1;
            }
        }
        return document.TextLength;
    }
}
