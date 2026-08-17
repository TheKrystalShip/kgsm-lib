using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// One key in an instance's configuration, as reported by
/// <c>kgsm instances config-list &lt;instance&gt; --json</c>.
/// </summary>
/// <remarks>
/// <see cref="Settable"/> is the engine's own judgement, made by the same rule
/// <c>instances config-set</c> applies — so a surface offering to change a key marked settable is
/// offering something the write path will accept. Identity keys, the filesystem paths KGSM manages,
/// and the toggles with dedicated enable/disable flows come back settable-false.
/// </remarks>
public record class InstanceConfigEntry
{
    /// <summary>The configuration key, exactly as <c>config-set</c> takes it.</summary>
    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;

    /// <summary>
    /// Whether this key can be changed through <c>config-set</c>. Read from the engine rather than
    /// re-derived, because a second copy of the rule is a second thing to keep in step.
    /// </summary>
    [JsonPropertyName("settable")]
    public bool Settable { get; set; }

    /// <summary>
    /// The current value, unwrapped and unescaped the way the management script's own read would
    /// see it. Empty string is a real value (the setting is present and blank).
    /// </summary>
    [JsonPropertyName("value")]
    public string Value { get; set; } = string.Empty;
}
