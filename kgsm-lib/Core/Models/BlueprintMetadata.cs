namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Advisory, presentation-oriented metadata describing a blueprint's game, for
/// catalog and UI surfaces such as the control panel (e.g. a friendly display
/// name and rough resource requirements).
/// </summary>
/// <remarks>
/// Every value is nullable and a <c>null</c> means <em>unknown or unbounded</em>;
/// it is never a substitute for a real <c>0</c>. The resource figures
/// (<see cref="MinRamMb"/>, <see cref="RecommendedRamMb"/>, <see cref="BaseDiskMb"/>)
/// are vendor-declared estimates, not measured guarantees — honoring KGSM's
/// "never fabricate a metric" invariant, an uncurated field stays <c>null</c>.
/// On the wire these arrive as the nested <c>Metadata</c> object emitted by
/// <c>kgsm blueprints info --json</c>.
/// </remarks>
public class BlueprintMetadata
{
    /// <summary>
    /// Gets or sets the human-friendly game name (e.g. "7 Days to Die"), or null.
    /// </summary>
    public string? DisplayName { get; set; }

    /// <summary>
    /// Gets or sets a short description of the game/server, or null.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the game's slug on RAWG.io (e.g. "garrys-mod"), or null. A
    /// lookup hint for the external catalog the control panel uses to fetch cover
    /// art / description / tags — the same kind of external-catalog id as a Steam
    /// App ID. KGSM never calls RAWG; the consumer (kgsm-api) does. The blueprint
    /// name is NOT assumed to equal the slug; an unverified game stays null (a wrong
    /// slug would be misattribution).
    /// </summary>
    public string? RawgSlug { get; set; }

    /// <summary>
    /// Gets or sets the maximum number of players, or null if
    /// unbounded/configurable/unknown.
    /// </summary>
    public int? MaxPlayers { get; set; }

    /// <summary>
    /// Gets or sets the advisory minimum RAM in megabytes, or null if unknown.
    /// </summary>
    public int? MinRamMb { get; set; }

    /// <summary>
    /// Gets or sets the advisory recommended RAM in megabytes, or null if unknown.
    /// </summary>
    public int? RecommendedRamMb { get; set; }

    /// <summary>
    /// Gets or sets the advisory base install footprint in megabytes (grows with
    /// saves/mods after install), or null if unknown.
    /// </summary>
    public int? BaseDiskMb { get; set; }
}
