using Aprillz.MewUI.MewvalonEdit.Document;
using Aprillz.MewUI.Text;
using Aprillz.MewUI.Text.Editing;

namespace Aprillz.MewUI.MewvalonEdit.Editing;

/// <summary>Decides which parts of a document an edit may touch.</summary>
public interface IReadOnlySectionProvider
{
    /// <summary>Whether text may be inserted at <paramref name="offset"/>.</summary>
    bool CanInsert(int offset);

    /// <summary>The parts of <paramref name="segment"/> that may be deleted, in document order.</summary>
    IEnumerable<ISegment> GetDeletableSegments(ISegment segment);
}

/// <summary>Leaves the whole document editable.</summary>
public sealed class NoReadOnlySections : IReadOnlySectionProvider
{
    public static readonly NoReadOnlySections Instance = new();

    public bool CanInsert(int offset) => true;

    public IEnumerable<ISegment> GetDeletableSegments(ISegment segment)
    {
        ArgumentNullException.ThrowIfNull(segment);
        yield return segment;
    }
}

/// <summary>Presents a ported provider to the core edit path.</summary>
internal sealed class ReadOnlySectionAdapter(IReadOnlySectionProvider provider) : IEditableRegionProvider
{
    public IReadOnlySectionProvider Provider { get; } = provider;

    public bool CanInsert(int offset) => Provider.CanInsert(offset);

    public void GetDeletableRanges(TextRange range, IList<TextRange> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        foreach (var segment in Provider.GetDeletableSegments(new SimpleSegment(range.Start, range.Length)))
        {
            output.Add(new TextRange(segment.Offset, segment.Length));
        }
    }
}
