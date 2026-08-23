using System.Text;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Normalizes the human-readable label an instance is shown as.
/// </summary>
/// <remarks>
/// <para>A label is a single line of text somebody chose to be read. It is stored in the instance's
/// <c>.config.ini</c> as plain text, which is what separates it from
/// <see cref="InstanceNote"/> — a note is base64-encoded and so can hold anything, while a label
/// reaches the file as itself and has to survive being read back out of it.</para>
/// <para>⚠ <b>That is a hard constraint, not tidiness.</b> The engine renders the config to JSON
/// with a line-oriented parse that separates key from value with a tab, so a tab inside a value
/// truncates it there and a newline makes the remainder of the label parse as further config keys —
/// under which an instance can report an id that is not its own. Both are silent. Stripping the
/// characters that cause it is the one place a C# caller's label is guaranteed to pass through.</para>
/// </remarks>
public static class InstanceDisplayName
{
    /// <summary>
    /// Reduces a label to the single line of text it is meant to be: every control character is
    /// dropped — tabs and newlines included, since a label spans one line by definition — and
    /// surrounding whitespace is trimmed.
    /// </summary>
    /// <param name="displayName">The label as a caller supplied it.</param>
    /// <returns>
    /// The label as it will be stored. The empty string comes back empty, which is the cleared
    /// state: an instance with no label of its own reads as its id.
    /// </returns>
    public static string Sanitize(string? displayName)
    {
        if (string.IsNullOrEmpty(displayName))
            return string.Empty;

        var sb = new StringBuilder(displayName.Length);
        foreach (char c in displayName)
            if (!char.IsControl(c))
                sb.Append(c);

        return sb.ToString().Trim();
    }
}
