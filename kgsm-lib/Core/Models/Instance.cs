using System.Text.Json.Serialization;
using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Represents an instance of a game server.
/// </summary>
public record class Instance
{
    /// <summary>
    /// Gets or sets the name of the instance.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

        /// <summary>
    /// Gets or sets the blueprint file path for the instance.
    /// </summary>
    [JsonPropertyName("blueprint_file")]
    public string BlueprintFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the installation date of the instance.
    /// </summary>
    [JsonPropertyName("install_datetime")]
    public DateTime InstallDateTime { get; set; } = DateTime.MinValue;

    /// <summary>
    /// Gets or sets the working directory for the instance.
    /// </summary>
    [JsonPropertyName("working_dir")]
    public string WorkingDir { get; set; } = string.Empty;

        /// <summary>
    /// Gets or sets the backups directory for the instance.
    /// </summary>
    [JsonPropertyName("backups_dir")]
    public string BackupsDir { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the installation directory for the instance.
    /// </summary>
    [JsonPropertyName("install_dir")]
    public string InstallDir { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the saves directory for the instance.
    /// </summary>
    [JsonPropertyName("saves_dir")]
    public string SavesDir { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the temporary directory for the instance.
    /// </summary>
    [JsonPropertyName("temp_dir")]
    public string TempDir { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the logs directory for the instance.
    /// </summary>
    [JsonPropertyName("logs_dir")]
    public string LogsDir { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the launch directory for the instance.
    /// </summary>
    [JsonPropertyName("launch_dir")]
    public string LaunchDir { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the executable subdirectory for the instance.
    /// </summary>
    [JsonPropertyName("executable_subdirectory")]
    public string ExecutableSubdirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the executable file for the instance.
    /// </summary>
    [JsonPropertyName("executable_file")]
    public string ExecutableFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the management file for the instance.
    /// </summary>
    [JsonPropertyName("management_file")]
    public string ManagementFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the compose file for the instance (for container instances).
    /// </summary>
    [JsonPropertyName("compose_file")]
    public string ComposeFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the version file for the instance.
    /// </summary>
    [JsonPropertyName("version_file")]
    public string VersionFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the PID file for the instance.
    /// </summary>
    [JsonPropertyName("pid_file")]
    public string PidFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the socket file for the instance.
    /// </summary>
    [JsonPropertyName("socket_file")]
    public string SocketFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the runtime environment for the instance. This is the sole
    /// supervision discriminator: native instances are supervised by kgsm-watchdog,
    /// container instances by Docker. Binds case-insensitively from KGSM's lowercase
    /// <c>runtime</c> field (no <c>[JsonPropertyName]</c> needed).
    /// </summary>
    public InstanceRuntime Runtime { get; set; } = InstanceRuntime.Native;

    /// <summary>
    /// Gets or sets the per-instance cgroup v2 directory KGSM derives for a native
    /// instance (e.g. <c>/sys/fs/cgroup/kgsm.slice/&lt;name&gt;</c>) — the exact path
    /// kgsm-watchdog (re)creates on every native start. <see cref="string.Empty"/> for
    /// container instances (Docker owns their cgroup) and when KGSM omits it. This is the
    /// engine-owned cgroup-path contract: a consumer reading per-instance cgroup counters
    /// (kgsm-monitor) uses this rather than re-deriving the layout or opening the watchdog
    /// socket, and falls back to its own probe (the <c>/proc</c> tree) when the directory
    /// is absent. Bound case-insensitively from KGSM's <c>cgroup_path</c> field.
    /// </summary>
    [JsonPropertyName("cgroup_path")]
    public string CgroupPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the platform for the instance.
    /// </summary>
    public string Platform { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether auto-update is enabled for the instance.
    /// </summary>
    [JsonPropertyName("auto_update")]
    public bool AutoUpdate { get; set; } = false;

    /// <summary>
    /// Gets or sets the CPU scheduling priority for the instance (<c>low</c>/<c>normal</c>/<c>high</c>).
    /// Null when KGSM omits <c>cpu_priority</c> — honest unknown, never fabricated as a default.
    /// The watchdog translates it to a cgroup <c>cpu.weight</c> value when enforcing.
    /// </summary>
    [JsonPropertyName("cpu_priority")]
    public string? CpuPriority { get; set; }

    /// <summary>
    /// Gets or sets the memory cap in megabytes for the instance's cgroup.
    /// Null when KGSM omits <c>memory_cap_mb</c> — honest unknown, never fabricated as a default.
    /// </summary>
    [JsonPropertyName("memory_cap_mb")]
    public int? MemoryCapMb { get; set; }

    /// <summary>
    /// Gets or sets whether a scheduled restart is configured for the instance.
    /// Null when KGSM omits <c>scheduled_restart</c> — honest unknown, never fabricated.
    /// Read by kgsm-scheduler to drive scheduled restarts.
    /// </summary>
    [JsonPropertyName("scheduled_restart")]
    public string? ScheduledRestart { get; set; }

    /// <summary>
    /// Gets or sets the time-of-day for the scheduled restart.
    /// Null when KGSM omits <c>restart_time</c> — honest unknown, never fabricated.
    /// </summary>
    [JsonPropertyName("restart_time")]
    public string? RestartTime { get; set; }

    /// <summary>
    /// Gets or sets the day for the scheduled restart.
    /// Null when KGSM omits <c>restart_day</c> — honest unknown, never fabricated.
    /// </summary>
    [JsonPropertyName("restart_day")]
    public string? RestartDay { get; set; }

    /// <summary>
    /// Gets or sets the timezone used to interpret the scheduled restart time.
    /// Null when KGSM omits <c>timezone</c> — honest unknown, never fabricated.
    /// </summary>
    [JsonPropertyName("timezone")]
    public string? Timezone { get; set; }

    /// <summary>
    /// Gets or sets whether a backup is taken automatically before a scheduled restart.
    /// Null when KGSM omits <c>auto_backup_on_restart</c> — honest unknown, never fabricated.
    /// Read by kgsm-scheduler to drive restart-window auto-backups.
    /// </summary>
    [JsonPropertyName("auto_backup_on_restart")]
    public bool? AutoBackupOnRestart { get; set; }

    /// <summary>
    /// Gets or sets the number of most-recent backups to retain when pruning.
    /// Null when KGSM omits <c>backup_retention</c> — honest unknown, never fabricated.
    /// </summary>
    [JsonPropertyName("backup_retention")]
    public int? BackupRetention { get; set; }

    /// <summary>
    /// Gets or sets whether the watchdog should automatically restart this instance on crash.
    /// Null when KGSM omits <c>crash_restart</c> — consumers default to true (auto-restart on).
    /// When false, any unintentional exit emits instance-crashed but does NOT schedule a restart.
    /// </summary>
    [JsonPropertyName("crash_restart")]
    public bool? CrashRestart { get; set; }

    /// <summary>
    /// Gets or sets the per-instance max consecutive restarts before the watchdog gives up.
    /// Null when KGSM omits <c>crash_max_restarts</c> — consumers fall back to global MaxRetries.
    /// </summary>
    [JsonPropertyName("crash_max_restarts")]
    public int? CrashMaxRestarts { get; set; }

    /// <summary>
    /// Gets or sets the logs redirect pattern for the instance.
    /// </summary>
    [JsonPropertyName("log_file")]
    public string LogFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the regex pattern that indicates successful startup of the instance.
    /// </summary>
    [JsonPropertyName("startup_success_regex")]
    public string StartupSuccessRegex { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the regex matched against the game's log output to detect a player
    /// joining (player-presence Increment 2, native detection). Empty when the blueprint
    /// sets no pattern — that detection is disabled (honest unknown, no event invented).
    /// Optional named groups <c>(?&lt;id&gt;…)</c> / <c>(?&lt;name&gt;…)</c> populate the
    /// emitted event's player id / name; at least one must match. Materialized from the
    /// blueprint into the instance config, so the resident supervisor (kgsm-watchdog) reads
    /// it off this Instance to tail a native instance's log. (Containers use the base64 env
    /// form + in-image shim instead; the field is shared but the matching engine differs —
    /// Perl in the container shim, .NET here.)
    /// </summary>
    [JsonPropertyName("player_joined_regex")]
    public string PlayerJoinedRegex { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the regex matched against the game's log output to detect a player
    /// leaving — the leave counterpart of <see cref="PlayerJoinedRegex"/>, same rules.
    /// </summary>
    [JsonPropertyName("player_left_regex")]
    public string PlayerLeftRegex { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the resident supervisor (kgsm-watchdog) should UPnP
    /// port-forward this instance — the per-instance gate its <c>UpnpService</c>
    /// reads. <see langword="false"/> (the safe default) when absent from the wire:
    /// current KGSM stripped UPnP from the bash engine (the headless-network plan
    /// re-homes it into the watchdog), so it is not emitted today and forwarding
    /// stays off until that migration re-supplies this flag. Typed here because the
    /// watchdog is a live consumer — it must compile against this Instance.
    /// </summary>
    [JsonPropertyName("enable_port_forwarding")]
    public bool EnablePortForwarding { get; set; } = false;

    /// <summary>
    /// Gets or sets the level name for the instance.
    /// </summary>
    [JsonPropertyName("level_name")]
    public string LevelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the executable arguments for the instance.
    /// </summary>
    [JsonPropertyName("executable_arguments")]
    public string ExecutableArguments { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the Steam app ID for the instance.
    /// </summary>
    [JsonPropertyName("steam_app_id")]
    public string SteamAppId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client Steam app ID for launch/connect deeplinks.
    /// </summary>
    [JsonPropertyName("client_steam_app_id")]
    public string ClientSteamAppId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether a Steam account is required for the instance.
    /// </summary>
    [JsonPropertyName("is_steam_account_required")]
    public bool IsSteamAccountRequired { get; set; } = false;

    /// <summary>
    /// Gets or sets the instance's ports as the canonical range-preserving structured form
    /// (<c>[{start,end,protocol}]</c>). KGSM derives this from the UFW-style port spec and emits
    /// it on <c>instances info --json</c>; it replaces the old opaque <c>ports</c> string. Consumers expand it (<see cref="PortMappingExtensions.Expand"/>)
    /// or render it (<see cref="PortMappingExtensions.ToUfwSpec"/>) rather than re-parsing a string.
    /// </summary>
    [JsonPropertyName("ports")]
    public List<PortMapping> Ports { get; set; } = [];

    /// <summary>
    /// Gets or sets whether firewall management is enabled for the instance.
    /// </summary>
    [JsonPropertyName("enable_firewall_management")]
    public bool EnableFirewallManagement { get; set; } = false;

    /// <summary>
    /// Gets or sets the firewall rule file for the instance.
    /// </summary>
    [JsonPropertyName("firewall_rule_file")]
    public string FirewallRuleFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the stop command for the instance.
    /// </summary>
    [JsonPropertyName("stop_command")]
    public string StopCommand { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the save command for the instance.
    /// </summary>
    [JsonPropertyName("save_command")]
    public string SaveCommand { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the save command timeout in seconds for the instance.
    /// </summary>
    [JsonPropertyName("save_command_timeout_seconds")]
    [JsonConverter(typeof(JsonStringToIntConverter))]
    public int SaveCommandTimeoutSeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the stop command timeout in seconds for the instance.
    /// </summary>
    [JsonPropertyName("stop_command_timeout_seconds")]
    [JsonConverter(typeof(JsonStringToIntConverter))]
    public int StopCommandTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Gets or sets whether backups are compressed for the instance.
    /// </summary>
    [JsonPropertyName("compress_backups")]
    public bool CompressBackups { get; set; } = false;

    /// <summary>
    /// Gets or sets whether command shortcuts are enabled for the instance.
    /// </summary>
    [JsonPropertyName("enable_command_shortcuts")]
    public bool EnableCommandShortcuts { get; set; } = false;

    /// <summary>
    /// Gets or sets the command shortcut file for the instance.
    /// </summary>
    [JsonPropertyName("command_shortcut_file")]
    public string CommandShortcutFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets the blueprint name extracted from the blueprint file path.
    /// </summary>
    public string Blueprint => Path.GetFileNameWithoutExtension(BlueprintFile);

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"Instance: {Name}, " +
               $"Runtime: {Runtime}, " +
               $"Platform: {Platform}, " +
               $"WorkingDir: {WorkingDir}, " +
               $"InstallDir: {InstallDir}, " +
               $"LogsDir: {LogsDir}, " +
               $"InstallDateTime: {InstallDateTime}, " +
               $"BlueprintFile: {BlueprintFile}, " +
               $"ManagementFile: {ManagementFile}, " +
               $"PidFile: {PidFile}, " +
               $"SocketFile: {SocketFile}, " +
               $"FirewallRuleFile: {FirewallRuleFile}";
    }
}
