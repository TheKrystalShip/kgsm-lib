using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// One contiguous port range for a single protocol — the canonical, range-preserving port
/// representation across the KGSM ecosystem. KGSM derives it from an instance's UFW-style port
/// spec (e.g. <c>27015:27020/udp|27016/tcp</c>) and emits it on the <c>instances info --json</c>
/// surface as <c>"ports": [ { "start": 27015, "end": 27020, "protocol": "udp" }, ... ]</c>.
/// <para>
/// A single port is <c>Start == End</c>; a UFW entry written with no protocol expands to two
/// mappings (one <c>tcp</c>, one <c>udp</c>). This is the shape kgsm-firewall's
/// <c>EnsureOpen(instance, ports[])</c> and the watchdog's port-forwarding path both consume — no consumer
/// re-parses an opaque port string. <see cref="Start"/>/<see cref="End"/> arrive as JSON numbers
/// (the global string→int coercion still accepts a stringly form, so either wire shape binds).
/// </para>
/// </summary>
public record class PortMapping
{
    /// <summary>First port of the inclusive range (a single port has <c>Start == End</c>).</summary>
    [JsonPropertyName("start")]
    public int Start { get; set; }

    /// <summary>Last port of the inclusive range (a single port has <c>Start == End</c>).</summary>
    [JsonPropertyName("end")]
    public int End { get; set; }

    /// <summary>Transport protocol — <c>"tcp"</c> or <c>"udp"</c>.</summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;
}

/// <summary>
/// Helpers over a set of <see cref="PortMapping"/>s. They live in kgsm-lib (the C#↔engine
/// chokepoint) so no consumer re-derives port logic — the watchdog expands individual ports, and
/// anyone needing the legacy string form renders it here.
/// </summary>
public static class PortMappingExtensions
{
    /// <summary>
    /// Expand the range-preserving mappings into individual <c>(Port, Protocol)</c> pairs — what
    /// port-forwarding needs (one external port at a time) and what a per-port conflict
    /// scan iterates. Inverted ranges (<c>End &lt; Start</c>) are skipped defensively. Order
    /// follows the source mappings.
    /// </summary>
    public static IEnumerable<(int Port, string Protocol)> Expand(this IEnumerable<PortMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        foreach (PortMapping m in mappings)
        {
            if (m is null || m.End < m.Start)
                continue;
            for (int port = m.Start; port <= m.End; port++)
                yield return (port, m.Protocol);
        }
    }

    /// <summary>
    /// Render the mappings back to a UFW-style spec string — <c>start:end/proto</c> for a range,
    /// <c>port/proto</c> for a single port, <c>|</c>-joined. The inverse of KGSM's parse, for the
    /// rare consumer that still wants the legacy string (e.g. the watchdog's defensive
    /// <c>$instance_ports</c> env value for blueprint arg expansion).
    /// </summary>
    public static string ToUfwSpec(this IEnumerable<PortMapping> mappings)
    {
        ArgumentNullException.ThrowIfNull(mappings);
        return string.Join("|", mappings
            .Where(m => m is not null)
            .Select(m => m.Start == m.End
                ? $"{m.Start}/{m.Protocol}"
                : $"{m.Start}:{m.End}/{m.Protocol}"));
    }

    /// <summary>
    /// Parse a UFW-style port spec string into the canonical structured form — the inverse of
    /// <see cref="ToUfwSpec"/>. KGSM emits this legacy string on the <c>blueprints … --json</c>
    /// surface (e.g. <c>"26900:26903/tcp|26900:26903/udp"</c> or <c>"7777/udp|27015/udp"</c>),
    /// where — unlike <c>instances info --json</c> — the ports are <em>not</em> pre-structured. This
    /// parser lives at the chokepoint so no consumer (the control-panel API's blueprint catalog, etc.)
    /// re-derives port logic: a structured surface gets a structured value here, not a relayed opaque
    /// string the SPA would have to split.
    /// <para>
    /// Mirrors KGSM's own parse: <c>|</c>-separated entries, each <c>port</c> or <c>start:end</c>
    /// optionally suffixed <c>/tcp</c> or <c>/udp</c>; an entry written with <strong>no</strong>
    /// protocol expands to two mappings (one <c>tcp</c>, one <c>udp</c>), matching the doc note on
    /// <see cref="PortMapping"/>. Honesty/robustness: a malformed entry (non-numeric port, an
    /// unrecognised protocol, or an inverted <c>end &lt; start</c> range) is <strong>skipped</strong>
    /// defensively rather than guessed — the same posture as <see cref="Expand"/>. A null/blank spec
    /// yields an empty list.
    /// </para>
    /// </summary>
    /// <param name="spec">The UFW-style spec string (may be null, blank, or malformed).</param>
    /// <returns>The parsed mappings, in source order; empty when nothing parses.</returns>
    public static List<PortMapping> FromUfwSpec(string? spec)
    {
        var result = new List<PortMapping>();
        if (string.IsNullOrWhiteSpace(spec))
            return result;

        foreach (string entry in spec.Split('|', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            string portPart = entry;
            string? protocol = null;

            int slash = entry.IndexOf('/');
            if (slash >= 0)
            {
                protocol = entry[(slash + 1)..].Trim().ToLowerInvariant();
                portPart = entry[..slash].Trim();
                // Unknown protocol — skip rather than fabricate a transport (never guess).
                if (protocol is not ("tcp" or "udp"))
                    continue;
            }

            int start, end;
            int colon = portPart.IndexOf(':');
            if (colon >= 0)
            {
                if (!int.TryParse(portPart[..colon].Trim(), out start)
                    || !int.TryParse(portPart[(colon + 1)..].Trim(), out end))
                    continue;
            }
            else
            {
                if (!int.TryParse(portPart.Trim(), out start))
                    continue;
                end = start;
            }

            // Inverted range — skip defensively (mirrors Expand's posture).
            if (end < start)
                continue;

            if (protocol is null)
            {
                // No protocol → both transports, matching KGSM's UFW expansion.
                result.Add(new PortMapping { Start = start, End = end, Protocol = "tcp" });
                result.Add(new PortMapping { Start = start, End = end, Protocol = "udp" });
            }
            else
            {
                result.Add(new PortMapping { Start = start, End = end, Protocol = protocol });
            }
        }

        return result;
    }
}
