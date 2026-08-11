using Aprillz.MewUI.MewvalonEdit.Document;

namespace Aprillz.MewUI.MewvalonEdit.Highlighting;

/// <summary>A run of text and the colour it is drawn in. Offsets are document offsets.</summary>
public class HighlightedSection : ISegment
{
    public int Offset { get; set; }
    public int Length { get; set; }

    public int EndOffset => Offset + Length;

    public HighlightingColor Color { get; set; } = null!;

    /// <inheritdoc/>
    public override string ToString()
        => $"[HighlightedSection ({Offset}-{Offset + Length})={Color}]";
}

/// <summary>The highlighting of one document line.</summary>
public class HighlightedLine
{
    public HighlightedLine(TextDocument document, DocumentLine documentLine)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        DocumentLine = documentLine ?? throw new ArgumentNullException(nameof(documentLine));
    }

    public TextDocument Document { get; }

    public DocumentLine DocumentLine { get; }

    /// <summary>
    /// The coloured sections, sorted by start offset. They do not overlap, but they do nest: an
    /// outer section comes before the sections inside it, and a later section paints over an
    /// earlier one it sits inside.
    /// </summary>
    public IList<HighlightedSection> Sections { get; } = new List<HighlightedSection>();

    /// <summary>Throws when <see cref="Sections"/> is unsorted, out of bounds, or overlapping.</summary>
    public void ValidateInvariants()
    {
        int lineStartOffset = DocumentLine.Offset;
        int lineEndOffset = DocumentLine.EndOffset;
        for (int i = 0; i < Sections.Count; i++)
        {
            var outer = Sections[i];
            if (outer.Offset < lineStartOffset || outer.Length < 0 || outer.EndOffset > lineEndOffset)
            {
                throw new InvalidOperationException("Section is outside line bounds");
            }
            for (int j = i + 1; j < Sections.Count; j++)
            {
                var inner = Sections[j];
                bool disjoint = inner.Offset >= outer.EndOffset;
                bool nested = inner.Offset >= outer.Offset && inner.EndOffset <= outer.EndOffset;
                if (!disjoint && !nested)
                {
                    throw new InvalidOperationException("Sections are overlapping or incorrectly sorted.");
                }
            }
        }
    }

    /// <summary>
    /// Layers another highlighting of the same line over this one, splitting its sections wherever
    /// they cross one of ours so the nesting invariant survives.
    /// </summary>
    public void MergeWith(HighlightedLine? additionalLine)
    {
        if (additionalLine is null)
        {
            return;
        }

        int pos = 0;
        var activeSectionEndOffsets = new Stack<int>();
        activeSectionEndOffsets.Push(DocumentLine.EndOffset);
        foreach (var newSection in additionalLine.Sections)
        {
            int newSectionStart = newSection.Offset;
            // Walk the existing sections up to where the first piece of newSection goes.
            while (pos < Sections.Count)
            {
                var section = Sections[pos];
                if (newSection.Offset < section.Offset)
                {
                    break;
                }
                while (section.Offset > activeSectionEndOffsets.Peek())
                {
                    activeSectionEndOffsets.Pop();
                }
                activeSectionEndOffsets.Push(section.EndOffset);
                pos++;
            }

            // A copy, so the sections traversed while inserting do not disturb the outer walk.
            var insertionStack = new Stack<int>(activeSectionEndOffsets.Reverse());
            int index;
            for (index = pos; index < Sections.Count; index++)
            {
                var section = Sections[index];
                if (newSection.EndOffset <= section.Offset)
                {
                    break;
                }
                Insert(ref index, ref newSectionStart, section.Offset, newSection.Color, insertionStack);
                while (section.Offset > insertionStack.Peek())
                {
                    insertionStack.Pop();
                }
                insertionStack.Push(section.EndOffset);
            }
            Insert(ref index, ref newSectionStart, newSection.EndOffset, newSection.Color, insertionStack);
        }
    }

    private void Insert(
        ref int pos,
        ref int newSectionStart,
        int insertionEndPos,
        HighlightingColor color,
        Stack<int> insertionStack)
    {
        if (newSectionStart >= insertionEndPos)
        {
            return;
        }
        while (insertionStack.Peek() <= newSectionStart)
        {
            insertionStack.Pop();
        }
        while (insertionStack.Peek() < insertionEndPos)
        {
            int end = insertionStack.Pop();
            if (end > newSectionStart)
            {
                Sections.Insert(pos++, new HighlightedSection
                {
                    Offset = newSectionStart,
                    Length = end - newSectionStart,
                    Color = color
                });
                newSectionStart = end;
            }
        }
        if (insertionEndPos > newSectionStart)
        {
            Sections.Insert(pos++, new HighlightedSection
            {
                Offset = newSectionStart,
                Length = insertionEndPos - newSectionStart,
                Color = color
            });
            newSectionStart = insertionEndPos;
        }
    }
}
