using Aprillz.MewUI.MewvalonEdit.Document;

namespace MewUI.MewvalonEdit.Test;

[TestClass]
public sealed class TextSegmentCollectionTests
{
    [TestMethod]
    public void InsertionBeforeASegmentShiftsIt()
    {
        var document = new TextDocument("hello world");
        var segments = new TextSegmentCollection<TextSegment>(document);
        segments.Add(new TextSegment { StartOffset = 6, EndOffset = 11 });

        document.Insert(0, ">> ");

        var segment = segments.Single();
        Assert.AreEqual(9, segment.StartOffset);
        Assert.AreEqual(14, segment.EndOffset);
        Assert.AreEqual("world", document.GetText(segment.StartOffset, segment.Length));
    }

    [TestMethod]
    public void RemovalInsideASegmentShrinksIt()
    {
        var document = new TextDocument("hello world");
        var segments = new TextSegmentCollection<TextSegment>(document);
        segments.Add(new TextSegment { StartOffset = 0, EndOffset = 11 });

        document.Remove(5, 6);

        var segment = segments.Single();
        Assert.AreEqual(0, segment.StartOffset);
        Assert.AreEqual(5, segment.EndOffset);
    }

    [TestMethod]
    public void FindOverlappingSegmentsReturnsOrderedMatches()
    {
        var segments = new TextSegmentCollection<TextSegment>();
        segments.Add(new TextSegment { StartOffset = 20, EndOffset = 30 });
        segments.Add(new TextSegment { StartOffset = 0, EndOffset = 5 });
        segments.Add(new TextSegment { StartOffset = 4, EndOffset = 12 });

        var overlapping = segments.FindOverlappingSegments(3, 5);

        Assert.HasCount(2, overlapping);
        Assert.AreEqual(0, overlapping[0].StartOffset);
        Assert.AreEqual(4, overlapping[1].StartOffset);
    }

    [TestMethod]
    public void SegmentsWithoutADocumentIgnoreEdits()
    {
        var segments = new TextSegmentCollection<TextSegment>();
        var segment = new TextSegment { StartOffset = 2, EndOffset = 4 };
        segments.Add(segment);

        segments.UpdateOffsets(new DocumentChangeEventArgs(0, removedText: null, insertedText: "abc"));

        Assert.AreEqual(5, segment.StartOffset);
        Assert.AreEqual(7, segment.EndOffset);
    }
}
