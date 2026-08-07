using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// Derives a stable id for a KGSM event — no <see cref="Guid"/>, no randomness, so two
/// independent computations of the same event agree with zero coordination between them.
/// That is what lets one component recognize another's "this is the same event" without a
/// shared sequence, a database round-trip, or a network call.
/// </summary>
/// <remarks>
/// Two derivations, for two different situations. <see cref="ForPosition"/> keys an event by
/// where it sits in the journal: unique by construction and ordered like the file, so it is
/// what the audit trail is keyed on and what paginates it. <see cref="ForEvent"/> keys an
/// event by its content, for a caller holding an envelope with no position to hand — it
/// cannot distinguish two identical events emitted within the same second, since the engine's
/// timestamps carry one-second granularity.
/// </remarks>
public static class AuditId
{
    /// <summary>
    /// Computes the deterministic id for <paramref name="wrapper"/>.
    /// </summary>
    /// <remarks>
    /// The id is <c>"evt_"</c> followed by the first 16 lowercase hex characters of a
    /// SHA-256 digest over a canonical, pipe-delimited string built from the
    /// envelope's identifying fields, in order: <see cref="EventWrapper.Hostname"/>,
    /// <see cref="EventWrapper.Timestamp"/> as unix milliseconds, <see cref="EventWrapper.EventType"/>,
    /// the instance name read from the <see cref="EventWrapper.Data"/> payload's
    /// <c>InstanceName</c> property (host/global events carry none), and a SHA-256
    /// digest of <see cref="EventWrapper.Data"/>'s raw JSON text — hashed separately
    /// from (rather than embedded verbatim in) the canonical string so an arbitrarily
    /// large payload still contributes a fixed-width, collision-resistant component.
    /// A missing field (null hostname/timestamp/instance, absent data) always
    /// contributes the same empty/zero form, so the same "nothing here" case hashes
    /// identically every time it occurs.
    /// </remarks>
    /// <param name="wrapper">The event envelope to derive an id for.</param>
    /// <returns>A stable id of the form <c>evt_&lt;16 lowercase hex chars&gt;</c>.</returns>
    public static string ForEvent(EventWrapper wrapper)
    {
        ArgumentNullException.ThrowIfNull(wrapper);

        string dataHash = ToHexLower(SHA256.HashData(Encoding.UTF8.GetBytes(GetRawDataText(wrapper.Data))));
        long timestampMs = wrapper.Timestamp?.ToUnixTimeMilliseconds() ?? 0;

        string canonical = string.Join(
            '|',
            wrapper.Hostname ?? string.Empty,
            timestampMs.ToString(CultureInfo.InvariantCulture),
            wrapper.EventType ?? string.Empty,
            ExtractInstanceName(wrapper.Data),
            dataHash);

        string digest = ToHexLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));

        return "evt_" + digest[..16];
    }

    /// <summary>
    /// Composes the id for the event stored at a journal position.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The id is <c>"evt_"</c>, the segment's name without its extension, an underscore, and the
    /// byte offset the event's line begins at, zero-padded to 12 digits —
    /// <c>evt_2026-08-07_000000001234</c>. Two properties follow from that and both are relied
    /// on. It is <b>unique</b>, because no two lines start at the same offset in the same
    /// segment; unlike a content-derived id it cannot merge two events that happen to be
    /// identical within the same second. And it is <b>ordered</b>: segment names are equal-width
    /// dates and the offset is fixed-width, so comparing two ids as plain strings compares their
    /// positions in the journal. That is what lets one value serve as both the event's identity
    /// and a pagination cursor.
    /// </para>
    /// <para>
    /// The padding is what makes the ordering work — <c>9</c> sorts after <c>10</c> as text, but
    /// <c>000000000009</c> does not. Twelve digits addresses a segment far larger than a day of
    /// events could ever produce; an offset beyond it still yields a correct, unique id and only
    /// loses the string-ordering property.
    /// </para>
    /// </remarks>
    /// <param name="segment">The segment file name, with or without its <c>.ndjson</c> extension.</param>
    /// <param name="offset">The byte offset the event's line starts at.</param>
    /// <returns>An id of the form <c>evt_&lt;segment&gt;_&lt;offset&gt;</c>.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="segment"/> is null, empty, or whitespace.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="offset"/> is negative.</exception>
    public static string ForPosition(string segment, long offset)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(segment, nameof(segment));
        ArgumentOutOfRangeException.ThrowIfNegative(offset, nameof(offset));

        return "evt_" + StemOf(segment) + "_" + offset.ToString("D12", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Reads a journal position back out of an id produced by <see cref="ForPosition"/>.
    /// </summary>
    /// <param name="id">The id to parse.</param>
    /// <param name="segment">The segment stem, without its extension.</param>
    /// <param name="offset">The byte offset.</param>
    /// <returns>
    /// True when <paramref name="id"/> is a position id. False for anything else — including a
    /// content-derived <see cref="ForEvent"/> id, which carries no position — leaving the
    /// caller to treat it as an unusable cursor rather than a wrong one.
    /// </returns>
    public static bool TryParsePosition(string? id, out string segment, out long offset)
    {
        segment = string.Empty;
        offset = 0;

        if (string.IsNullOrEmpty(id) || !id.StartsWith("evt_", StringComparison.Ordinal))
            return false;

        // The stem may itself contain underscores, so the offset is taken from the LAST one.
        int split = id.LastIndexOf('_');
        if (split <= 3)
            return false;

        if (!long.TryParse(id.AsSpan(split + 1), NumberStyles.None, CultureInfo.InvariantCulture, out offset))
            return false;

        segment = id[4..split];
        return segment.Length > 0;
    }

    /// <summary>The segment file name without its extension.</summary>
    private static string StemOf(string segment)
    {
        string name = Path.GetFileName(segment);
        return name.EndsWith(".ndjson", StringComparison.Ordinal) ? name[..^7] : name;
    }

    private static string GetRawDataText(JsonElement data) =>
        data.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null
            ? string.Empty
            : data.GetRawText();

    private static string ExtractInstanceName(JsonElement data)
    {
        if (data.ValueKind == JsonValueKind.Object
            && data.TryGetProperty("InstanceName", out JsonElement instanceName)
            && instanceName.ValueKind == JsonValueKind.String)
        {
            return instanceName.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private static string ToHexLower(byte[] bytes) => Convert.ToHexStringLower(bytes);
}
