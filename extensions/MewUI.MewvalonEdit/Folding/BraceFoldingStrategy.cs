using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Folding;

public sealed class BraceFoldingStrategy
{
    public char OpeningBrace { get; set; } = '{';
    public char ClosingBrace { get; set; } = '}';

    public void UpdateFoldings(FoldingManager manager, TextDocument document)
    {
        ArgumentNullException.ThrowIfNull(manager);
        manager.UpdateFoldings(CreateNewFoldings(document, out int firstErrorOffset), firstErrorOffset);
    }

    public IEnumerable<NewFolding> CreateNewFoldings(TextDocument document, out int firstErrorOffset)
    {
        ArgumentNullException.ThrowIfNull(document);
        firstErrorOffset = -1;
        var foldings = new List<NewFolding>();
        var starts = new Stack<int>();
        int lastLineStart = 0;
        for (int offset = 0; offset < document.TextLength; offset++)
        {
            char value = document.GetCharAt(offset);
            if (value == OpeningBrace)
            {
                starts.Push(offset);
            }
            else if (value == ClosingBrace && starts.Count > 0)
            {
                int start = starts.Pop();
                if (start < lastLineStart)
                    foldings.Add(new NewFolding(start, offset + 1));
            }
            else if (value is '\r' or '\n')
            {
                lastLineStart = offset + 1;
            }
        }
        if (starts.Count > 0) firstErrorOffset = starts.Min();
        return foldings.OrderBy(folding => folding.StartOffset).ToArray();
    }
}
