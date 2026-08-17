using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// One port the host is listening on, as reported by
/// <c>kgsm network ports list-used --json</c>.
/// </summary>
/// <remarks>
/// <see cref="Process"/> is null when the socket could not be attributed to a process — the listing
/// is read without elevated privilege, so a port owned by another user reports the port and nothing
/// else. Null is "who holds it is unknown", never a placeholder name.
/// </remarks>
public record class HostPort
{
    /// <summary>The listening port number.</summary>
    [JsonPropertyName("port")]
    public int Port { get; set; }

    /// <summary>The protocol it listens on: <c>tcp</c> or <c>udp</c>.</summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>The process holding the socket, or null when it could not be attributed.</summary>
    [JsonPropertyName("process")]
    public string? Process { get; set; }
}

/// <summary>
/// Which two claimants want the same port, as reported by
/// <c>kgsm network ports conflicts --json</c>.
/// </summary>
/// <remarks>
/// The engine finds these; nothing above it re-derives them by comparing instance configs. An
/// instance's ports are expanded through the canonical parser first, so a range and a proto-less
/// entry are both compared port by port.
/// </remarks>
public record class PortConflict
{
    /// <summary>
    /// <c>instance</c> when two KGSM instances are configured for the same port, <c>external</c>
    /// when a process outside KGSM already holds one an instance wants.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; set; } = string.Empty;

    /// <summary>The contested port.</summary>
    [JsonPropertyName("port")]
    public int Port { get; set; }

    /// <summary>The protocol the contest is on: <c>tcp</c> or <c>udp</c>.</summary>
    [JsonPropertyName("protocol")]
    public string Protocol { get; set; } = string.Empty;

    /// <summary>The instance whose configuration claims the port.</summary>
    [JsonPropertyName("instance")]
    public string Instance { get; set; } = string.Empty;

    /// <summary>
    /// The other claimant: another instance's name for an <c>instance</c> conflict, or the
    /// outside process for an <c>external</c> one.
    /// </summary>
    [JsonPropertyName("other")]
    public string Other { get; set; } = string.Empty;
}
