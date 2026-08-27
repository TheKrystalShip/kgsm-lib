namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Builds the optional provenance environment (<c>KGSM_EVENT_ACTOR</c> / <c>KGSM_EVENT_ORIGIN</c>) that
/// attributes the events a mutating KGSM command emits — <em>who</em> triggered it and <em>through which
/// surface</em>. KGSM's event layer (<c>commands/events.sh</c>) reads these for every event it emits, so
/// stamping them on the child process is all a caller need do.
/// </summary>
/// <remarks>
/// Shared by every provenance-aware mutation (<see cref="LifecycleService"/> start/stop/restart and the
/// <see cref="InstanceService"/> install/uninstall/update/backup/config verbs) so the rule lives in one
/// place. Only non-empty values are set: a null/empty actor or origin is <em>omitted</em>, and the event
/// records without it — neither a principal nor a surface is ever fabricated to fill the gap. Returns
/// <see langword="null"/> when neither is supplied, so the caller takes the plain no-env command path.
/// <para>
/// An actor is written <c>provider:name</c> (see <c>KgsmActor</c>); the engine refuses anything else
/// rather than writing a value no reader can resolve back into a principal.
/// </para>
/// </remarks>
internal static class KgsmProvenance
{
    public static IReadOnlyDictionary<string, string>? Build(string? actor, string? origin)
    {
        if (string.IsNullOrEmpty(actor) && string.IsNullOrEmpty(origin))
            return null;

        Dictionary<string, string> env = new(2);
        if (!string.IsNullOrEmpty(actor))
            env["KGSM_EVENT_ACTOR"] = actor;
        if (!string.IsNullOrEmpty(origin))
            env["KGSM_EVENT_ORIGIN"] = origin;
        return env;
    }
}
