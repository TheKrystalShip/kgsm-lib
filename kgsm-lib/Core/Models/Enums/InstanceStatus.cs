using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Represents the status of an instance.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<InstanceStatus>))]
public enum InstanceStatus
{
    /// <summary>
    /// The instance is active/running.
    /// </summary>
    Active,

    /// <summary>
    /// The instance is inactive/stopped.
    /// </summary>
    Inactive
}