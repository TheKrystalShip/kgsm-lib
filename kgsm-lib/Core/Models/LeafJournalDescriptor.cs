using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// The fields of an installed leaf descriptor that journal discovery needs.
/// </summary>
/// <remarks>
/// <para>
/// A deliberately partial read of <c>/var/lib/kgsm/leaves/&lt;leaf&gt;.json</c>. The descriptor's own
/// format is owned elsewhere (<c>leaf-config-descriptor.md</c>) and describes a leaf's whole
/// configuration surface; this library needs only which leaves write a journal and where, so it reads
/// those and ignores the rest.
/// </para>
/// <para>
/// <b>The journal directory is declared, never derived.</b> Only the leaf knows where it can write:
/// this host has one leaf whose unit name and state directory differ (<c>kgsm-assistant-service</c>
/// versus <c>kgsm-assistant</c>) and two with no state directory at all, so any naming convention is
/// right for most leaves and silently wrong for the rest. Wrong here is expensive in both directions —
/// the writer cannot create a directory under root-owned <c>/var/lib</c> and drops the event, while a
/// reader looks in the same empty place and reports the producer unreadable forever.
/// </para>
/// </remarks>
public sealed class LeafJournalDescriptor
{
    /// <summary>The leaf's short id (<c>api</c>, <c>watchdog</c>), unique per host.</summary>
    [JsonPropertyName("id")]
    public string? Id { get; set; }

    /// <summary>The systemd unit (<c>kgsm-api.service</c>). Read for diagnostics only.</summary>
    /// <remarks>
    /// ⚠ Not the journal's location and not the producer id: a unit name and a leaf's state directory
    /// are allowed to differ, and on this host they do.
    /// </remarks>
    [JsonPropertyName("unit")]
    public string? Unit { get; set; }

    /// <summary>
    /// Where this leaf appends its event journal segments, or <see langword="null"/> when it writes no
    /// journal.
    /// </summary>
    /// <remarks>
    /// Absent is the honest answer for a leaf that records nothing of its own, and is what every
    /// descriptor says until that leaf is migrated to owning its own journal. So discovery tracks
    /// exactly which producers exist at any point in that migration, with nothing to exclude and
    /// nothing guessed.
    /// </remarks>
    [JsonPropertyName("journalDir")]
    public string? JournalDirectory { get; set; }
}
