using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

// The engine's answers to "where could this blueprint live" (`kgsm blueprints find <name> --json`) and
// "is this file a valid blueprint" (`kgsm blueprints validate <name|path> --json`). Both cross the KGSM
// process boundary, so both are registered in KgsmJsonContext. No C# ever composes a blueprint path or
// re-implements the schema check — these types only carry what the engine reported.

/// <summary>
/// Which of kgsm's two blueprint directories a file lives in. A <see cref="User"/> blueprint shadows a
/// same-named <see cref="System"/> one (kgsm's loader resolves user before system), which is what makes
/// an override possible.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<BlueprintTier>))]
public enum BlueprintTier
{
    /// <summary>The writable user blueprints directory (<c>KGSM_USER_BLUEPRINTS_DIR</c>).</summary>
    User,

    /// <summary>The read-only, shipped-with-the-engine blueprints directory
    /// (<c>KGSM_SYSTEM_BLUEPRINTS_DIR</c>). Never written to — it is the rsync target of the engine's
    /// deploy and would be erased on the next one.</summary>
    System,
}

/// <summary>
/// One path a blueprint name could resolve to, and whether a file is actually there.
/// </summary>
public sealed record BlueprintCandidate
{
    /// <summary>Which directory this candidate is in.</summary>
    public BlueprintTier Tier { get; init; }

    /// <summary>The absolute path the engine would look at for this tier.</summary>
    public string Path { get; init; } = string.Empty;

    /// <summary>Whether a file is present at <see cref="Path"/>. Existence only — a malformed blueprint
    /// still reports <see langword="true"/>, since locating a file is not the same as approving it.</summary>
    public bool Exists { get; init; }
}

/// <summary>
/// Every path a blueprint name could resolve to, in precedence order, as reported by
/// <c>kgsm blueprints find &lt;name&gt; --json</c>. This is how a consumer tells a purely custom blueprint
/// apart from a user copy shadowing a shipped one: the single path plain <c>find</c> returns cannot
/// distinguish them.
/// </summary>
public sealed record BlueprintCandidates
{
    /// <summary>The blueprint name that was resolved.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>The candidate the engine would actually load — the first existing one in precedence
    /// order. <see langword="null"/> when no candidate exists.</summary>
    public string? Resolved { get; init; }

    /// <summary>Every candidate path, user tier before system tier.</summary>
    public List<BlueprintCandidate> Candidates { get; init; } = [];

    /// <summary>The user-tier candidate, or <see langword="null"/> if the engine reported none.</summary>
    public BlueprintCandidate? User =>
        Candidates.FirstOrDefault(c => c.Tier == BlueprintTier.User);

    /// <summary>The system-tier candidate, or <see langword="null"/> if the engine reported none.</summary>
    public BlueprintCandidate? System =>
        Candidates.FirstOrDefault(c => c.Tier == BlueprintTier.System);

    /// <summary>Whether a shipped original exists for this name — the precondition for reverting an
    /// override, and the only thing that makes deleting the user file a restore rather than a
    /// destruction.</summary>
    public bool HasSystemOriginal => System?.Exists == true;

    /// <summary>Whether a user file is currently shadowing a shipped original (both exist).</summary>
    public bool OverridesSystem => User?.Exists == true && HasSystemOriginal;
}

/// <summary>
/// The engine's verdict on a blueprint file, from <c>kgsm blueprints validate &lt;name|path&gt; --json</c>
/// — YAML syntax plus the required fields for its runtime. kgsm-lib never re-implements this check; it
/// invokes it and reports what came back.
/// </summary>
public sealed record BlueprintValidation
{
    /// <summary>Whether the file passed every check.</summary>
    public bool Valid { get; init; }

    /// <summary>The absolute path that was checked.</summary>
    public string? Path { get; init; }

    /// <summary>Every problem found, not just the first — empty when <see cref="Valid"/>.</summary>
    public List<string> Errors { get; init; } = [];
}
