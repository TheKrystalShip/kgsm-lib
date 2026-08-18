namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Why a backup was taken, as the engine's manifest records it. A closed set: the engine refuses a
/// value outside it, and a consumer switches on these rather than on a free string.
/// </summary>
/// <remarks>
/// A reason is a <b>fact</b> about how the archive was produced and is fixed at capture — nothing
/// edits it afterwards. Whether the backup may be pruned is the separate, mutable
/// <see cref="BackupRetention"/>: a fact and a policy sharing one slot could never diverge, and they
/// need to.
/// <para>
/// There is deliberately no member for "unknown". A manifest that records no reason reports null,
/// and null is the answer — see <see cref="InstanceBackup.Reason"/>.
/// </para>
/// </remarks>
public static class BackupReason
{
    /// <summary>An ad-hoc request that stated nothing else — no update, no restore, no cadence.</summary>
    public const string Manual = "manual";

    /// <summary>An automated cadence took it.</summary>
    public const string Scheduled = "scheduled";

    /// <summary>An update took it, immediately before overwriting the install. The rollback point.</summary>
    public const string PreUpdate = "pre-update";

    /// <summary>A restore took it, immediately before replacing the instance's data.</summary>
    public const string PreRestore = "pre-restore";

    /// <summary>Taken over a failing server, to preserve the broken state for triage.</summary>
    public const string Incident = "incident";

    /// <summary>Every reason the engine accepts.</summary>
    public static readonly IReadOnlyList<string> All = [Manual, Scheduled, PreUpdate, PreRestore, Incident];
}

/// <summary>
/// Whether rotation may delete a backup. A policy, and the one part of a backup that changes after
/// it is written — <c>IInstanceService.PinBackup</c> and <c>UnpinBackup</c> are how it changes.
/// </summary>
public static class BackupRetention
{
    /// <summary><c>prune-backups</c> may delete it once it falls outside the keep window.</summary>
    public const string Prunable = "prunable";

    /// <summary>
    /// <c>prune-backups</c> skips it, and it does not count toward the keep window — so pinning
    /// backups never shrinks how many live ones the rotation holds.
    /// </summary>
    public const string Pinned = "pinned";

    /// <summary>Every retention policy the engine accepts.</summary>
    public static readonly IReadOnlyList<string> All = [Prunable, Pinned];
}
