using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Blueprint types.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlueprintType>))]
public enum BlueprintType
{
    /// <summary>
    /// Native blueprint type.
    /// </summary>
    Native,

    /// <summary>
    /// Container blueprint type.
    /// </summary>
    Container
}