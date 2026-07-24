using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// The machine-readable XDG directory layout emitted by <c>kgsm --paths --json</c> — grouped into a
/// read-only <see cref="System"/> block and a writable <see cref="User"/> block, mirroring the engine's
/// own <c>core/paths.sh</c> variables one-for-one. Deserialized via <c>KgsmJsonContext</c> (AOT-safe).
/// This is how a C# consumer learns an engine path (e.g. the user blueprints directory) without parsing
/// free-form text.
/// </summary>
public sealed record KgsmPaths
{
    /// <summary>The read-only, shipped-with-the-engine paths.</summary>
    [JsonPropertyName("system")]
    public KgsmSystemPaths? System { get; init; }

    /// <summary>The writable, user-owned XDG paths.</summary>
    [JsonPropertyName("user")]
    public KgsmUserPaths? User { get; init; }
}

/// <summary>The read-only <c>system</c> block of <see cref="KgsmPaths"/>.</summary>
public sealed record KgsmSystemPaths
{
    /// <summary>The engine install root (<c>KGSM_ROOT</c>).</summary>
    [JsonPropertyName("KGSM_ROOT")]
    public string? Root { get; init; }

    /// <summary><c>KGSM_CORE_DIR</c>.</summary>
    [JsonPropertyName("KGSM_CORE_DIR")]
    public string? CoreDir { get; init; }

    /// <summary><c>KGSM_COMMANDS_DIR</c>.</summary>
    [JsonPropertyName("KGSM_COMMANDS_DIR")]
    public string? CommandsDir { get; init; }

    /// <summary><c>KGSM_HANDLERS_DIR</c>.</summary>
    [JsonPropertyName("KGSM_HANDLERS_DIR")]
    public string? HandlersDir { get; init; }

    /// <summary><c>KGSM_TEMPLATES_DIR</c>.</summary>
    [JsonPropertyName("KGSM_TEMPLATES_DIR")]
    public string? TemplatesDir { get; init; }

    /// <summary><c>KGSM_MIGRATIONS_DIR</c>.</summary>
    [JsonPropertyName("KGSM_MIGRATIONS_DIR")]
    public string? MigrationsDir { get; init; }

    /// <summary>The read-only, shipped blueprints directory (<c>KGSM_SYSTEM_BLUEPRINTS_DIR</c>).</summary>
    [JsonPropertyName("KGSM_SYSTEM_BLUEPRINTS_DIR")]
    public string? SystemBlueprintsDir { get; init; }

    /// <summary><c>KGSM_SYSTEM_OVERRIDES_DIR</c>.</summary>
    [JsonPropertyName("KGSM_SYSTEM_OVERRIDES_DIR")]
    public string? SystemOverridesDir { get; init; }

    /// <summary><c>KGSM_DEFAULT_CONFIG_FILE</c>.</summary>
    [JsonPropertyName("KGSM_DEFAULT_CONFIG_FILE")]
    public string? DefaultConfigFile { get; init; }
}

/// <summary>The writable <c>user</c> block of <see cref="KgsmPaths"/>.</summary>
public sealed record KgsmUserPaths
{
    /// <summary><c>KGSM_CONFIG_DIR</c>.</summary>
    [JsonPropertyName("KGSM_CONFIG_DIR")]
    public string? ConfigDir { get; init; }

    /// <summary><c>KGSM_CONFIG_FILE</c>.</summary>
    [JsonPropertyName("KGSM_CONFIG_FILE")]
    public string? ConfigFile { get; init; }

    /// <summary><c>KGSM_DATA_DIR</c>.</summary>
    [JsonPropertyName("KGSM_DATA_DIR")]
    public string? DataDir { get; init; }

    /// <summary><c>KGSM_INSTANCES_DIR</c>.</summary>
    [JsonPropertyName("KGSM_INSTANCES_DIR")]
    public string? InstancesDir { get; init; }

    /// <summary><c>KGSM_LOGS_DIR</c>.</summary>
    [JsonPropertyName("KGSM_LOGS_DIR")]
    public string? LogsDir { get; init; }

    /// <summary>The writable user blueprints directory (<c>KGSM_USER_BLUEPRINTS_DIR</c>) — where
    /// <see cref="Interfaces.IBlueprintFiles"/> creates and removes <c>&lt;name&gt;.bp.yaml</c> files.</summary>
    [JsonPropertyName("KGSM_USER_BLUEPRINTS_DIR")]
    public string? UserBlueprintsDir { get; init; }

    /// <summary><c>KGSM_USER_OVERRIDES_DIR</c>.</summary>
    [JsonPropertyName("KGSM_USER_OVERRIDES_DIR")]
    public string? UserOverridesDir { get; init; }
}
