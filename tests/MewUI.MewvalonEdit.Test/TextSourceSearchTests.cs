using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Test;

/// <summary>
/// Both search arguments are a range, not a start and an end, which is the trap in porting code
/// written against string.LastIndexOf. Every offset returned is a document offset.
/// </summary>
[TestClass]
public sealed class TextSourceSearchTests
{
    [TestMethod]
    public void CharacterSearchesReturnDocumentOffsets()
    {
        var document = new TextDocument("abcabc");

        Assert.AreEqual(1, document.IndexOf('b', 0, 6));
        Assert.AreEqual(4, document.IndexOf('b', 2, 4));
        Assert.AreEqual(4, document.LastIndexOf('b', 0, 6));
        Assert.AreEqual(1, document.LastIndexOf('b', 0, 4));
        Assert.AreEqual(-1, document.IndexOf('z', 0, 6));
    }

    /// <summary>The range ends where it was told to, so a match just past it is not reported.</summary>
    [TestMethod]
    public void TheRangeIsALengthAndNotAnEndOffset()
    {
        var document = new TextDocument("abcabc");

        Assert.AreEqual(-1, document.IndexOf('c', 0, 2));
        Assert.AreEqual(2, document.IndexOf('c', 0, 3));
    }

    [TestMethod]
    public void IndexOfAnyTakesTheFirstOfAnyOfThem()
    {
        var document = new TextDocument("a b\tc");

        Assert.AreEqual(1, document.IndexOfAny([' ', '\t'], 0, 5));
        Assert.AreEqual(3, document.IndexOfAny([' ', '\t'], 2, 3));
    }

    [TestMethod]
    public void StringSearchesHonourTheComparison()
    {
        var document = new TextDocument("Hello hello");

        Assert.AreEqual(6, document.IndexOf("hello", 0, 11, StringComparison.Ordinal));
        Assert.AreEqual(0, document.IndexOf("hello", 0, 11, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(6, document.LastIndexOf("hello", 0, 11, StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual(-1, document.IndexOf("world", 0, 11, StringComparison.Ordinal));
    }

    [TestMethod]
    public void ARangeOutsideTheDocumentThrows()
    {
        var document = new TextDocument("abc");

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => document.IndexOf('a', 0, 4));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => document.IndexOf('a', -1, 1));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() => document.LastIndexOf('a', 2, 2));
    }

    /// <summary>A snapshot answers the same way, because both go through the same search.</summary>
    [TestMethod]
    public void ASnapshotSearchesTheSameWay()
    {
        ITextSource snapshot = new TextDocument("abcabc").CreateSnapshot();

        Assert.AreEqual(4, snapshot.LastIndexOf('b', 0, 6));
        Assert.AreEqual(3, snapshot.IndexOf("ab", 1, 5, StringComparison.Ordinal));
    }
}
