using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Indentation;

public interface IIndentationStrategy
{
    void IndentLine(TextDocument document, DocumentLine line);
    void IndentLines(TextDocument document, int beginLine, int endLine);
}
