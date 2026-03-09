using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for managing blueprints in KGSM.
/// </summary>
public interface IBlueprintService
{
    /// <summary>
    /// Gets a list of all blueprint names.
    /// </summary>
    /// <returns>
    /// A list of blueprint names or empty if none found.
    /// </returns>
    List<string> List();

    /// <summary>
    /// Gets a list of default (official) blueprint names.
    /// </summary>
    /// <returns>
    /// A list of default blueprint names or empty if none found.
    /// </returns>
    List<string> ListDefault();

    /// <summary>
    /// Gets a list of custom (user-created) blueprint names.
    /// </summary>
    /// <returns>
    /// A list of custom blueprint names or empty if none found.
    /// </returns>
    List<string> ListCustom();

    /// <summary>
    /// Gets detailed information for all blueprints.
    /// </summary>
    /// <returns>
    /// A dictionary of blueprint names to blueprint objects with full details,
    /// or empty if none found.
    /// </returns>
    Dictionary<string, Blueprint> ListDetailed();

    /// <summary>
    /// Gets detailed information about a specific blueprint.
    /// </summary>
    /// <param name="blueprintName">The name of the blueprint to get info for.</param>
    /// <returns>
    /// The blueprint object with all details.
    /// </returns>
    Blueprint? GetInfo(string blueprintName);

    /// <summary>
    /// Finds the file path for a specific blueprint.
    /// </summary>
    /// <param name="blueprintName">The name of the blueprint to find.</param>
    /// <returns>
    /// The absolute file path to the blueprint file.
    /// </returns>
    string? FindPath(string blueprintName);
}
