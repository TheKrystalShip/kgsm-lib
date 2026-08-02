using System.Text;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// The codec for an instance's operator-authored <strong>server note</strong> — the free-text
/// sticky note (mods, rules, a heads-up before joining) that surfaces render on a game server.
/// </summary>
/// <remarks>
/// <para><b>Why a codec exists at all.</b> The note lives in the instance's <c>.config.ini</c> under
/// three keys — <c>note</c> (the body), <c>note_updated_by</c> and <c>note_updated_at</c> — and that
/// file is <em>sourced</em> by the management script as <c>key="value"</c>. A raw free-text body
/// containing a quote, a <c>$</c>, a backtick or a newline would brick the instance. kgsm's own
/// INI→JSON emit is tab-delimited into <c>jq</c>, so a tab or newline would also corrupt the roster
/// read. Base64 has none of those characters, so the body is stored encoded and decoded here.</para>
/// <para><b>kgsm stays generic.</b> The engine knows nothing about notes: this writes through the
/// ordinary <c>instances config-set</c> path and reads the value straight off the instance roster.
/// kgsm-lib is the only place the encoding is understood, which is what keeps every C# surface
/// (the API, the bot, the assistant) reading one shape.</para>
/// <para><b>The literal fallback.</b> A value that does not decode is returned verbatim as the body.
/// Someone who hand-edits <c>.config.ini</c> and types a plain note still gets it rendered (with no
/// attribution), and the next save through a surface re-encodes it properly. A hand-typed value that
/// happens to be well-formed base64 <em>and</em> decodes to clean UTF-8 text is indistinguishable
/// from an encoded one and will be shown decoded — accepted, as the collision needs a body drawn
/// only from the base64 alphabet with a length that is a multiple of four.</para>
/// </remarks>
public static class InstanceNote
{
    /// <summary>
    /// The maximum length, in characters, of a sanitized note body. Surfaces enforce this before
    /// writing (a longer body is rejected, never silently truncated).
    /// </summary>
    public const int MaxLength = 600;

    /// <summary>The config key holding the encoded body.</summary>
    public const string BodyKey = "note";

    /// <summary>The config key holding the actor string of whoever last wrote the note.</summary>
    public const string UpdatedByKey = "note_updated_by";

    /// <summary>The config key holding the UTC ISO-8601 timestamp of the last write.</summary>
    public const string UpdatedAtKey = "note_updated_at";

    /// <summary>
    /// Normalizes a note body for storage: CRLF/CR collapse to LF, control characters other than
    /// LF are dropped (they would survive base64 and reach a renderer), and surrounding whitespace
    /// is trimmed. The result is what <see cref="MaxLength"/> is measured against.
    /// </summary>
    public static string Sanitize(string? body)
    {
        if (string.IsNullOrEmpty(body))
            return string.Empty;

        var sb = new StringBuilder(body.Length);
        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];
            if (c == '\r')
            {
                // CRLF -> LF (skip the LF that follows), lone CR -> LF.
                if (i + 1 < body.Length && body[i + 1] == '\n') i++;
                sb.Append('\n');
                continue;
            }

            // Keep LF; drop every other control character (including tab, which would corrupt
            // kgsm's tab-delimited INI->JSON emit if it ever reached an unencoded path).
            if (c == '\n' || !char.IsControl(c))
                sb.Append(c);
        }

        return sb.ToString().Trim();
    }

    /// <summary>
    /// Sanitizes and base64-encodes a note body into the value stored under <see cref="BodyKey"/>.
    /// An empty (or whitespace-only) body encodes to the empty string — the cleared state.
    /// </summary>
    /// <exception cref="ArgumentException">The sanitized body exceeds <see cref="MaxLength"/>.</exception>
    public static string Encode(string? body)
    {
        string clean = Sanitize(body);
        if (clean.Length == 0)
            return string.Empty;

        if (clean.Length > MaxLength)
            throw new ArgumentException(
                $"note body is {clean.Length} characters; the maximum is {MaxLength}", nameof(body));

        return Convert.ToBase64String(Encoding.UTF8.GetBytes(clean));
    }

    /// <summary>
    /// Decodes a stored <see cref="BodyKey"/> value back to the note body, or <see langword="null"/>
    /// when there is no note. A value that is not well-formed base64 over clean UTF-8 text is
    /// returned verbatim (see the class remarks on the literal fallback).
    /// </summary>
    public static string? Decode(string? stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return null;

        string raw = stored.Trim();
        return TryDecodeBase64(raw, out string? decoded) ? decoded : raw;
    }

    // Well-formed base64 whose bytes are valid UTF-8 AND whose text survives sanitizing unchanged.
    // The last check is what rejects a short plain word that happens to be base64-shaped but decodes
    // to control bytes; strict UTF-8 (throwOnInvalidBytes) rejects the rest.
    private static bool TryDecodeBase64(string raw, out string? decoded)
    {
        decoded = null;

        // Convert.TryFromBase64String tolerates internal whitespace; a note body never has the
        // base64 shape by accident once it contains a space, so require the compact form.
        if (raw.Length == 0 || raw.Length % 4 != 0)
            return false;

        byte[] buffer = new byte[raw.Length / 4 * 3];
        if (!Convert.TryFromBase64String(raw, buffer, out int written) || written == 0)
            return false;

        string text;
        try
        {
            text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true)
                .GetString(buffer, 0, written);
        }
        catch (DecoderFallbackException)
        {
            return false;
        }

        if (text.Length == 0 || Sanitize(text) != text)
            return false;

        decoded = text;
        return true;
    }
}
