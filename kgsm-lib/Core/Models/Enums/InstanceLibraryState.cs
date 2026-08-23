using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Where an instance's files stand relative to the host's registered libraries. The engine
/// measures this from the instance registry on every read — never from the instance's own config,
/// which is exactly what cannot be opened in the case that matters.
/// </summary>
/// <remarks>
/// Three values rather than <see cref="LibraryState"/>'s two: a library is either reachable or it
/// is not, but an instance can also sit under a root this host has no entry for.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<InstanceLibraryState>))]
public enum InstanceLibraryState
{
    /// <summary>
    /// The instance's library is registered and its root is reachable. Everything about the
    /// instance can be read.
    /// </summary>
    Online,

    /// <summary>
    /// The instance's library is registered and its root is not reachable — an unmounted disk.
    /// The instance still exists and is still registered; nothing inside its directory can be
    /// read, so almost every reading about it is unknown rather than false.
    /// </summary>
    Offline,

    /// <summary>
    /// The instance's working directory sits under no registered library. A measurement, not an
    /// absence: the files are there and readable, and the host simply holds no entry naming the
    /// root they are under.
    /// </summary>
    Unregistered
}
