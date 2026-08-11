using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Folding;

public class NewFolding : ISegment
{
    public NewFolding()
    {
    }

    public NewFolding(int start, int end)
    {
        if (start > end) throw new ArgumentException("start must not exceed end", nameof(start));
        StartOffset = start;
        EndOffset = end;
    }

    public int StartOffset { get; set; }
    public int EndOffset { get; set; }
    public string? Name { get; set; }
    public bool DefaultClosed { get; set; }
    public bool IsDefinition { get; set; }
    int ISegment.Offset => StartOffset;
    int ISegment.Length => EndOffset - StartOffset;
}
