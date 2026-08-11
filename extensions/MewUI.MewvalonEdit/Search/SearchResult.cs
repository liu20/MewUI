using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Search;

public readonly record struct SearchResult(int Offset, int Length) : ISegment
{
    public int EndOffset => Offset + Length;
}
