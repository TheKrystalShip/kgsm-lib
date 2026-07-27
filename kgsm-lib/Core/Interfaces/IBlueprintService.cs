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
    /// <remarks>
    /// This form asks the engine to approve the blueprint as well as locate it, so a malformed file
    /// reports no path at all. Use <see cref="FindAll(string)"/> when the file has to be found in order
    /// to be repaired, or when a caller needs to tell a custom blueprint apart from an override.
    /// </remarks>
    string? FindPath(string blueprintName);

    /// <summary>
    /// Reports every path a blueprint name could resolve to, in precedence order, along with whether a
    /// file exists at each — <c>kgsm blueprints find &lt;name&gt; --json</c>.
    /// </summary>
    /// <param name="blueprintName">The name of the blueprint to resolve.</param>
    /// <returns>
    /// The candidate set, or <see langword="null"/> when the name resolves to nothing at all (no file in
    /// either tier) or the engine could not be reached.
    /// </returns>
    /// <remarks>
    /// Unlike <see cref="FindPath(string)"/> this reports on existence alone and skips the format check,
    /// so a malformed blueprint is still locatable. Two existing candidates mean a user file is shadowing
    /// a shipped one; only a user candidate means there is no original to fall back to.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="blueprintName"/> is null or whitespace.</exception>
    BlueprintCandidates? FindAll(string blueprintName);

    /// <summary>
    /// Runs the engine's blueprint schema check — <c>kgsm blueprints validate &lt;name|path&gt; --json</c>
    /// — and returns its verdict with every problem it found.
    /// </summary>
    /// <param name="blueprintNameOrPath">
    /// A blueprint name, or an absolute path to a candidate file. The path form is what allows a file to
    /// be checked BEFORE it is committed under a blueprint's real name.
    /// </param>
    /// <returns>
    /// The engine's verdict — including a <c>Valid == false</c> one carrying the error list — or
    /// <see langword="null"/> when the engine could not be reached or its answer was unreadable (an
    /// unknown verdict, never an assumed pass).
    /// </returns>
    /// <remarks>
    /// Nothing is written and no event is emitted. This is the ONLY schema judgment kgsm-lib makes: the
    /// engine owns the rules, and the library never re-implements them.
    /// </remarks>
    /// <exception cref="ArgumentException">Thrown when <paramref name="blueprintNameOrPath"/> is null or whitespace.</exception>
    BlueprintValidation? Validate(string blueprintNameOrPath);
}
