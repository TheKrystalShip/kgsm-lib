using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Whether a registered library's root is reachable and carries its marker.
/// The engine measures this per invocation; it is never cached.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<LibraryState>))]
public enum LibraryState
{
    /// <summary>
    /// The root exists and its <c>.kgsm-library</c> marker matches the registry entry.
    /// Instances can be placed into it.
    /// </summary>
    Online,

    /// <summary>
    /// The root is missing, or its marker is absent or names a different library — an
    /// unmounted disk is the ordinary case. Placement is refused and the library reports
    /// no capacity, because nothing measured it.
    /// </summary>
    Offline
}
