namespace Aprillz.MewUI.MewvalonEdit.Sample;

/// <summary>
/// Text carrying characters that cannot be typed, so the boxes standing in for them are reachable
/// only from a document loaded like this. The characters are written by code point rather than
/// placed in the literal, where they would be invisible and would not survive a trimming editor.
/// </summary>
internal static class ControlCharacterSample
{
    public static string Text()
        => string.Join('\n',
            $"before{(char)0x02}after   <- STX, the C0 block is named",
            $"bell{(char)0x07}ring     <- BEL",
            $"null{(char)0x00}byte     <- NUL",
            $"escape{(char)0x1B}here   <- ESC",
            $"delete{(char)0x7F}here   <- DEL",
            $"c-one{(char)0x91}here    <- PU1, the C1 block is named too",
            $"unnamed{(char)0x200B}here <- no name, so the code point is drawn",
            "\tleading tab, and a trailing space -> ");
}
