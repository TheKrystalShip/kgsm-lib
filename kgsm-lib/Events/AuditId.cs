using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// Derives a stable, content-derived id for a KGSM event envelope — no
/// <see cref="Guid"/>, no randomness. Two independent computations of the same
/// event (e.g. kgsm-monitor persisting it and kgsm-api's live handler correlating
/// it) produce byte-identical ids with zero coordination between the two callers,
/// which is what lets each side recognize "this is the same event" without a
/// shared sequence, a database round-trip, or a network call.
/// </summary>
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
