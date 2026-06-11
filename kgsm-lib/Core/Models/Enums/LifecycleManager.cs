using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Represents the lifecycle manager for an instance.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LifecycleManager>))]
public enum LifecycleManager
{
    /// <summary>
    /// The instance is managed standalone.
    /// </summary>
    Standalone,

    /// <summary>
    /// The instance is managed by systemd.
    /// </summary>
    Systemd
}