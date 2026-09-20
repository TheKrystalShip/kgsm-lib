namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// One line of a segment, as a reader finds it on disk rather than as a writer meant it.
/// </summary>
/// <remarks>
/// A segment is append-only and never rewritten, so everything a reader has to allow for arrives
/// from outside the writer: a line is exactly what was appended, or it is what the machine going
/// down left behind.
/// </remarks>
public static class JournalLine
{
    /// <summary>
    /// <paramref name="line"/> with the hole an unclean shutdown left in it stripped off.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>A NUL byte is never content.</b> The writer appends UTF-8 JSON and escapes every control
    /// character, so <c>0x00</c> cannot survive a write. Where a reader finds a run of them, the
    /// filesystem had recorded the file as longer without having written the bytes, and the machine
    /// went down in between: the zeros stand in for an append that no longer exists anywhere.
    /// </para>
    /// <para>
    /// Those bytes are unrecoverable and nothing here pretends otherwise. What sits past the hole is
    /// a different matter — the hole carries no newline of its own, so it runs straight into the next
    /// append that did land and a reader is handed one line made of both. Stripping the zeros gives
    /// back that append, a whole event that was written and flushed, instead of discarding it along
    /// with the hole and reporting its producer for output it never produced.
    /// </para>
    /// <para>
    /// Zeros left in the middle are not stripped: a hole with a partial record on each side of it is
    /// a torn write, and there is no honest way to read one. It stays unparseable and is reported.
    /// </para>
    /// </remarks>
    /// <param name="line">The line as read.</param>
    /// <returns>
    /// The line without its leading and trailing NUL run, or an empty string when it held nothing but
    /// hole.
    /// </returns>
    public static string WithoutHole(string? line)
    {
        if (string.IsNullOrEmpty(line))
            return string.Empty;

        return line.IndexOf('\0') < 0 ? line : line.Trim('\0');
    }

    /// <summary>Whether <paramref name="line"/> carries any of the hole described by <see cref="WithoutHole"/>.</summary>
    /// <param name="line">The line as read.</param>
    public static bool IsHoled(string? line) => line is not null && line.IndexOf('\0') >= 0;
}
