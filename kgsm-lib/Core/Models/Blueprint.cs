using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Represents a blueprint for creating game server instances.
/// </summary>
public class Blueprint
{
    /// <summary>
    /// Gets or sets the name of the blueprint.
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the ports used by the game server.
    /// </summary>
    public string Ports { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the blueprint type (Native or Container).
    /// </summary>
    public BlueprintType BlueprintType { get; set; } = BlueprintType.Native;

    /// <summary>
    /// Gets or sets the Steam App ID.
    /// </summary>
    public string SteamAppId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the client Steam App ID for launch/connect deeplinks.
    /// </summary>
    public string ClientSteamAppId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets whether a Steam account is required for the game server.
    /// </summary>
    public bool IsSteamAccountRequired { get; set; } = false;

    /// <summary>
    /// Gets or sets the executable file name.
    /// </summary>
    public string ExecutableFile { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the subdirectory where the executable is located.
    /// </summary>
    public string ExecutableSubdirectory { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments to pass to the executable.
    /// </summary>
    public string ExecutableArguments { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the level name to use.
    /// </summary>
    public string LevelName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the command to stop the server.
    /// </summary>
    public string? StopCommand { get; set; } = null;

    /// <summary>
    /// Gets or sets the command to save the server state.
    /// </summary>
    public string? SaveCommand { get; set; } = null;

    /// <summary>
    /// Gets or sets the advisory presentation metadata for the blueprint's game
    /// (display name, requirements). Null when the blueprint declares no metadata.
    /// </summary>
    public BlueprintMetadata? Metadata { get; set; } = null;

    /// <summary>
    /// Returns a string that represents the current object.
    /// </summary>
    /// <returns>A string that represents the current object.</returns>
    public override string ToString()
    {
        return $"Blueprint: {Name}, " +
               $"Ports: {Ports}, " +
               $"BlueprintType: {BlueprintType}, " +
               $"SteamAppId: {SteamAppId}, " +
               $"ClientSteamAppId: {ClientSteamAppId}, " +
               $"IsSteamAccountRequired: {IsSteamAccountRequired}, " +
               $"ExecutableFile: {ExecutableFile}, " +
               $"ExecutableSubdirectory: {ExecutableSubdirectory}, " +
               $"ExecutableArguments: {ExecutableArguments}, " +
               $"LevelName: {LevelName}, " +
               $"StopCommand: {StopCommand ?? "None"}, " +
               $"SaveCommand: {SaveCommand ?? "None"}";
    }
}
