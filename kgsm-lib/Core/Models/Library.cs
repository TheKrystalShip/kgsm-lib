using System.Text.Json.Serialization;
using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// A named root that game server instances are placed in. A host registers one per
/// disk it wants instances to live on, and an instance records the root it was placed
/// under (<see cref="Instance.LibraryDir"/>).
/// </summary>
public record class Library
{
    /// <summary>
    /// Gets or sets the library's name — host-unique, lowercase letters, digits and
    /// dashes. This is what <c>--library</c> takes.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the absolute, canonical path of the library root.
    /// </summary>
    [JsonPropertyName("path")]
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether the root is reachable and carries its marker.
    /// </summary>
    [JsonPropertyName("state")]
    public LibraryState State { get; set; } = LibraryState.Offline;

    /// <summary>
    /// Gets whether the library is online — the convenience form of
    /// <see cref="State"/>.
    /// </summary>
    [JsonIgnore]
    public bool Online => State == LibraryState.Online;

    /// <summary>
    /// Gets or sets the free space on the library's filesystem, in bytes.
    /// Null when the library is offline: nothing measured it, and a figure would be
    /// invented.
    /// </summary>
    [JsonPropertyName("free_bytes")]
    public long? FreeBytes { get; set; }

    /// <summary>
    /// Gets or sets the total size of the library's filesystem, in bytes.
    /// Null on the same terms as <see cref="FreeBytes"/>.
    /// </summary>
    [JsonPropertyName("total_bytes")]
    public long? TotalBytes { get; set; }

    /// <summary>
    /// Gets or sets how many registered instances resolve to this library. Counted from
    /// the instance registry, so it is answered for an offline library too.
    /// </summary>
    [JsonPropertyName("instance_count")]
    public int InstanceCount { get; set; }

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
        => $"Library: {Name}, Path: {Path}, State: {State}, " +
           $"FreeBytes: {FreeBytes?.ToString() ?? "unknown"}, " +
           $"TotalBytes: {TotalBytes?.ToString() ?? "unknown"}, " +
           $"InstanceCount: {InstanceCount}";
}
