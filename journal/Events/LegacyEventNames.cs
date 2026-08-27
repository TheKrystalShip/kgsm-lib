namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// The names earlier builds wrote, and what each one is called now.
/// </summary>
/// <remarks>
/// <para>
/// A journal holds what was written, for as long as retention keeps it — so a reader meets both
/// spellings for one retention period after a producer cuts over. This is the one place that knows
/// that, applied where a line is read, so every consumer above works in the current vocabulary and
/// none of them carries a second name for anything.
/// </para>
/// <para>
/// <b>This table has an end date.</b> It exists for as long as a segment written by an earlier build
/// can still be read — one <c>event_journal_retention_days</c> after the last producer cuts over.
/// Past that no line can carry a legacy name, and the table goes with the code that reads it.
/// </para>
/// <para>
/// ⚠ <b>Direction is one-way.</b> Nothing translates a current name back: the legacy spellings are
/// not written by anything and a reverse lookup would only ever serve code trying to write one.
/// </para>
/// </remarks>
public static class LegacyEventNames
{
    private static readonly Dictionary<string, string> Renamed = new(StringComparer.Ordinal)
    {
        ["instance_started"] = "server.started",
        ["instance_ready"] = "server.ready",
        ["instance_stopped"] = "server.stopped",
        ["instance_restarted"] = "server.restarted",
        ["instance_crashed"] = "server.crashed",
        ["instance_failed"] = "server.crash.exhausted",
        ["instance_installed"] = "server.installed",
        ["instance_uninstalled"] = "server.uninstalled",
        ["instance_uninstall_failed"] = "server.uninstall.failed",
        ["instance_moved"] = "server.moved",
        ["instance_display_name_changed"] = "server.renamed",
        ["instance_version_updated"] = "server.updated",
        ["instance_update_failed"] = "server.update.failed",
        ["instance_update_available"] = "server.update.available",
        ["instance_config_changed"] = "config.changed",
        ["instance_input_sent"] = "console.input.sent",
        ["instance_announcement_sent"] = "announcement.sent",
        ["instance_ports_opened"] = "network.ports.opened",
        ["instance_ports_closed"] = "network.ports.closed",
        ["instance_upnp_opened"] = "network.upnp.opened",
        ["instance_upnp_closed"] = "network.upnp.closed",
        ["instance_upnp_reasserted"] = "network.upnp.reasserted",
        ["instance_player_joined"] = "player.joined",
        ["instance_player_left"] = "player.left",
        ["instance_player_kicked"] = "player.kicked",
        ["instance_player_banned"] = "player.banned",
        ["instance_player_unbanned"] = "player.unbanned",
        ["instance_backup_created"] = "backup.created",
        ["instance_backup_restored"] = "backup.restored",
        ["instance_backup_deleted"] = "backup.deleted",
        ["instance_backups_pruned"] = "backup.pruned",
        ["instance_backup_pinned"] = "backup.pinned",
        ["instance_backup_unpinned"] = "backup.unpinned",
        ["backup_downloaded"] = "backup.downloaded",
        ["instance_backup_started"] = "backup.started",
        ["instance_backup_finished"] = "backup.finished",
        ["instance_restore_started"] = "backup.restore.started",
        ["instance_restore_finished"] = "backup.restore.finished",
        ["instance_stop_started"] = "server.stop.started",
        ["instance_stop_finished"] = "server.stop.finished",
        ["instance_restart_started"] = "server.restart.started",
        ["instance_restart_stopped"] = "server.restart.stopped",
        ["instance_restart_finished"] = "server.restart.finished",
        ["instance_update_started"] = "server.update.started",
        ["instance_update_finished"] = "server.update.finished",
        ["instance_updated"] = "server.update.completed",
        ["instance_installation_started"] = "server.install.started",
        ["instance_installation_finished"] = "server.install.finished",
        ["instance_created"] = "server.install.created",
        ["instance_directories_created"] = "server.install.directories_created",
        ["instance_files_created"] = "server.install.files_created",
        ["instance_download_started"] = "server.download.started",
        ["instance_download_finished"] = "server.download.finished",
        ["instance_download_failed"] = "server.download.failed",
        ["instance_downloaded"] = "server.download.completed",
        ["instance_deploy_started"] = "server.deploy.started",
        ["instance_deploy_finished"] = "server.deploy.finished",
        ["instance_deploy_failed"] = "server.deploy.failed",
        ["instance_deployed"] = "server.deploy.completed",
        ["instance_uninstall_started"] = "server.uninstall.started",
        ["instance_uninstall_finished"] = "server.uninstall.finished",
        ["instance_files_removed"] = "server.uninstall.files_removed",
        ["instance_directories_removed"] = "server.uninstall.directories_removed",
        ["instance_removed"] = "server.uninstall.removed",
        ["blueprint_created"] = "blueprint.created",
        ["blueprint_updated"] = "blueprint.updated",
        ["blueprint_removed"] = "blueprint.removed",
        ["library_added"] = "library.added",
        ["library_removed"] = "library.removed",
        ["library_renamed"] = "library.renamed",
        ["library_failed"] = "library.failed",
        ["file_written"] = "file.written",
        ["host_threshold_breached"] = "host.threshold.breached",
        ["host_threshold_cleared"] = "host.threshold.cleared",
        ["server_memory_oom"] = "server.memory.oom_killed",
        ["auth_login"] = "auth.signed_in",
        ["auth_logout"] = "auth.signed_out",
        ["auth_session_revoked"] = "auth.session.revoked",
        ["auth_cluster_session"] = "auth.cluster.vouched",
        ["user_provisioned"] = "user.provisioned",
        ["user_approved"] = "user.approved",
        ["user_disabled"] = "user.disabled",
        ["user_tier_changed"] = "user.tier_changed",
        ["user_deleted"] = "user.deleted",
        ["user_password_changed"] = "user.password_changed",
        ["identity_linked"] = "identity.linked",
        ["identity_unlinked"] = "identity.unlinked",
        ["service_connected"] = "service.connected",
        ["service_disconnected"] = "service.disconnected",
        ["service_config_changed"] = "service.config_changed",
        ["service_restarted"] = "service.restarted",
        ["command_failed"] = "command.failed",
        ["command_refused"] = "command.refused",
        ["command_cancelled"] = "command.cancelled",
        ["leaf_ready"] = "leaf.ready",
        ["leaf_degraded"] = "leaf.degraded",
        ["leaf_recovered"] = "leaf.recovered",
        ["leaf_stopping"] = "leaf.stopping",
        ["reactor_decided"] = "reactor.decided",
        ["reactor_acted"] = "reactor.acted",
        ["assistant_action_declined"] = "assistant.action.declined",
        ["assistant_action_proposed"] = "assistant.action.proposed",
        ["assistant_claim_corrected"] = "assistant.claim.corrected",
        ["assistant_blueprint_authored"] = "assistant.blueprint.authored",
        ["assistant_blueprint_authoring_started"] = "assistant.blueprint.authoring_started",
    };

    /// <summary>
    /// The current name for <paramref name="type"/>, which is <paramref name="type"/> itself unless an
    /// earlier build wrote it under another one.
    /// </summary>
    /// <param name="type">The name as a line carries it.</param>
    /// <returns>The name every consumer above this works in.</returns>
    public static string Canonical(string? type) =>
        type is not null && Renamed.TryGetValue(type, out string? current) ? current : type ?? string.Empty;

    /// <summary>Whether <paramref name="type"/> is a spelling only earlier builds wrote.</summary>
    /// <param name="type">The name as a line carries it.</param>
    /// <returns>True when the name has been renamed since it was written.</returns>
    public static bool IsLegacy(string? type) => type is not null && Renamed.ContainsKey(type);
}
