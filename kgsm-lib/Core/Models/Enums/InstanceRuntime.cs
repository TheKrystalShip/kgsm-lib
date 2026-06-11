using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Represents the runtime environment for an instance.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InstanceRuntime>))]
public enum InstanceRuntime
{
    /// <summary>
    /// The instance runs natively.
    /// </summary>
    Native,

    /// <summary>
    /// The instance runs in a container.
    /// </summary>
    Container
}