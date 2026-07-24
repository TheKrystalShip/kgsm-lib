using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// The write-side authority for native-runtime blueprint files: creates and removes
/// <c>&lt;name&gt;.bp.yaml</c> files in kgsm's writable USER blueprints directory. The SECOND kgsm-lib
/// service that does direct <c>System.IO</c> (the first is <see cref="IInstanceFiles"/>, whose shape this
/// mirrors: atomic write, a jail, and an outcome-not-exception failure channel). The complementary
/// write-side to the existing read-only <see cref="IBlueprintService"/>.
/// </summary>
/// <remarks>
/// Three non-negotiable guardrails (see <c>assistant-blueprint-authoring-plan.md</c>):
/// <list type="number">
/// <item><b>No semantic validation here.</b> This authority only performs STRUCTURAL checks (a required
///   field present, a safe name) — never re-implements what kgsm considers a *valid* blueprint (a legal
///   <c>runtime</c>, port-format parsing, ...). Every write must be validated by reading it back through
///   the engine's existing read path (<see cref="IBlueprintService.GetInfo(string)"/>) — the engine stays
///   the schema authority.</item>
/// <item><b>The jail root is learned from the engine, never re-derived in C#.</b> The user blueprints
///   directory is resolved fresh on every call from <c>kgsm --paths --json</c>'s
///   <c>user.KGSM_USER_BLUEPRINTS_DIR</c> (deserialized to <see cref="KgsmPaths"/> — a stable,
///   machine-readable contract, no free-form text parsing). This mirrors how <see cref="IInstanceFiles"/>
///   learns its jail root from <see cref="IInstanceService.GetInstanceInfo(string)"/>'s <c>WorkingDir</c>
///   rather than re-deriving XDG paths in C#.</item>
/// <item><b>AOT-safe YAML.</b> No YamlDotNet or other reflection-based serializer — the native schema is
///   small and flat, so <see cref="Create"/> string-templates it directly (deterministic, diff-stable
///   field order matching <c>kgsm/templates/blueprint.tp</c>).</item>
/// </list>
/// <para>
/// Failure-channel convention: every method returns a <see cref="FileOpResult"/>/<see cref="FileOpResult{T}"/>
/// whose <see cref="FileOpResult.Outcome"/> reports what happened — including an invalid name, a
/// resolution failure for the user blueprints dir, or a filesystem error — and none of these methods
/// throw for an expected failure. Argument validation (a null/blank <c>name</c>) still throws, per the
/// rest of kgsm-lib's convention.
/// </para>
/// <para>
/// The jail: the root is the engine-reported user blueprints directory, canonicalised (POSIX
/// <c>realpath</c>) fresh on every call — never cached, mirroring <see cref="IInstanceFiles"/>. Unlike
/// <see cref="IInstanceFiles"/> (which resolves an arbitrary caller-supplied relative path), the only
/// caller input here is a single-segment <c>name</c>: it is rejected as
/// <see cref="FileOpOutcome.OutOfJail"/> before any disk access unless it is a safe lowercase slug
/// (<c>[a-z0-9_-]</c>, 1–64 chars, no leading/trailing separator) — no path separators, no <c>..</c>, no
/// absolute path can ever reach this far. The resulting <c>&lt;userDir&gt;/&lt;name&gt;.bp.yaml</c> path
/// is still canonicalised and containment-checked as defense in depth, exactly like
/// <see cref="IInstanceFiles"/>' jail check.
/// </para>
/// <para>
/// <see cref="Remove"/> operates ONLY inside the user blueprints directory — it is structurally
/// incapable of touching the read-only SYSTEM blueprints directory (<c>KGSM_SYSTEM_BLUEPRINTS_DIR</c>,
/// e.g. shipped blueprints like <c>factorio.bp.yaml</c>): the path it deletes is always
/// <c>&lt;userDir&gt;/&lt;name&gt;.bp.yaml</c>, never derived from or resolved against the system dir. If
/// a name has no file in the user dir (whether or not a same-named SYSTEM blueprint exists),
/// <see cref="Remove"/> reports <see cref="FileOpOutcome.NotFound"/> and touches nothing.
/// </para>
/// </remarks>
public interface IBlueprintFiles
{
    /// <summary>
    /// Templates <paramref name="draft"/> into a valid native <c>&lt;name&gt;.bp.yaml</c> string and
    /// atomically writes it into the user blueprints directory (temp file in the same directory →
    /// fsync → rename — a crash mid-write never corrupts an existing file of the same name).
    /// </summary>
    /// <param name="draft">The blueprint draft to template and write.</param>
    /// <param name="overwrite">When true, an existing same-named user blueprint is replaced; when false
    /// (default), an existing target is refused with <see cref="FileOpOutcome.AlreadyExists"/>. A
    /// self-repair loop that re-persists an adjusted draft under the same probe name passes
    /// <see langword="true"/>.</param>
    /// <returns>
    /// <see cref="FileOpOutcome.Ok"/> with the written file's size/mtime/etag;
    /// <see cref="FileOpOutcome.InvalidDraft"/> if <see cref="NativeBlueprintDraft.Native"/>'s
    /// <c>ExecutableFile</c> is blank (the one structural requirement — see the interface remarks for
    /// what this authority does and does not validate);
    /// <see cref="FileOpOutcome.OutOfJail"/> if <see cref="NativeBlueprintDraft.Name"/> is not a safe
    /// slug, or the resolved target somehow escapes the user blueprints directory;
    /// <see cref="FileOpOutcome.AlreadyExists"/> if a same-named user blueprint exists and
    /// <paramref name="overwrite"/> is false;
    /// <see cref="FileOpOutcome.BlueprintsDirUnavailable"/> if the user blueprints directory could not be
    /// resolved from the engine;
    /// <see cref="FileOpOutcome.IoError"/> for any other filesystem failure.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="draft"/> is null.</exception>
    FileOpResult<FileStat> Create(NativeBlueprintDraft draft, bool overwrite = false);

    /// <summary>
    /// Deletes <c>&lt;name&gt;.bp.yaml</c> from the user blueprints directory ONLY — see the interface
    /// remarks for why this can never reach a system blueprint.
    /// </summary>
    /// <param name="name">The blueprint name (the same slug passed as <see cref="NativeBlueprintDraft.Name"/>
    /// to <see cref="Create"/>) to remove.</param>
    /// <returns>
    /// <see cref="FileOpOutcome.Ok"/> on success;
    /// <see cref="FileOpOutcome.OutOfJail"/> if <paramref name="name"/> is not a safe slug;
    /// <see cref="FileOpOutcome.NotFound"/> if no such file exists in the user blueprints directory
    /// (including when only a same-named SYSTEM blueprint exists — that file is never touched);
    /// <see cref="FileOpOutcome.BlueprintsDirUnavailable"/> if the user blueprints directory could not be
    /// resolved from the engine;
    /// <see cref="FileOpOutcome.IoError"/> for any other filesystem failure.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    FileOpResult Remove(string name);

    /// <summary>
    /// Reports whether <c>&lt;name&gt;.bp.yaml</c> currently exists in the user blueprints directory,
    /// without reading it.
    /// </summary>
    /// <param name="name">The blueprint name to check.</param>
    /// <returns>
    /// <see cref="FileOpOutcome.Ok"/> with <see langword="true"/>/<see langword="false"/>;
    /// <see cref="FileOpOutcome.OutOfJail"/> if <paramref name="name"/> is not a safe slug;
    /// <see cref="FileOpOutcome.BlueprintsDirUnavailable"/> if the user blueprints directory could not be
    /// resolved from the engine.
    /// </returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="name"/> is null or whitespace.</exception>
    FileOpResult<bool> Exists(string name);
}
