using System.Reflection;

namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// The build a producer stamps on every event it writes.
/// </summary>
/// <remarks>
/// <para>
/// One resolver, because <c>ProducerVersion</c> only answers "which build emitted this" if every
/// producer answers it the same way. An assembly exposes two versions that differ in shape — a
/// four-part <see cref="AssemblyName.Version"/> that no released package is ever numbered with, and
/// the informational version, which is the project's own <c>Version</c> plus whatever build identity
/// it stamps on. Producers picking one each make the field incomparable across a merged page.
/// </para>
/// <para>
/// The informational version wins because it is the number a consumer can act on: it matches the
/// package a repo publishes, the version a consumer pins, and the tag a release carries, and it
/// carries a source revision when the build stamps one.
/// </para>
/// <para>
/// <b>Never a fabricated fallback.</b> An assembly that carries neither yields null, the field is
/// omitted, and a reader sees no claim about the build — which is the honest answer and the one the
/// envelope already defines. A placeholder like <c>0.0.0</c> would be a version that was never built.
/// </para>
/// <para>
/// Reading an assembly attribute is trim- and AOT-safe: attributes are metadata the compiler emits
/// and the ILC keeps, with no reflection over types involved.
/// </para>
/// </remarks>
public static class ProducerVersion
{
    /// <summary>
    /// The version <paramref name="assembly"/> reports, or null when it reports none.
    /// </summary>
    /// <param name="assembly">
    /// The producer's own assembly — passed explicitly rather than inferred from the call stack,
    /// which inlining and AOT both make unreliable.
    /// </param>
    /// <returns>
    /// The informational version, falling back to the assembly version, or null when neither is set.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="assembly"/> is null.</exception>
    public static string? Of(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly, nameof(assembly));

        return Resolve(
            assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion,
            assembly.GetName().Version?.ToString());
    }

    /// <summary>
    /// The precedence rule itself, over two candidate versions.
    /// </summary>
    /// <remarks>
    /// Separate from <see cref="Of"/> because the rule is the part worth pinning down and an assembly's
    /// own metadata is not something a test can vary. What a producer stamps is decided here.
    /// </remarks>
    /// <param name="informationalVersion">The assembly's informational version, if it carries one.</param>
    /// <param name="assemblyVersion">The assembly's four-part version, if it carries one.</param>
    /// <returns>The version to stamp, or null when neither candidate says anything.</returns>
    public static string? Resolve(string? informationalVersion, string? assemblyVersion)
    {
        if (!string.IsNullOrWhiteSpace(informationalVersion))
            return informationalVersion;

        return string.IsNullOrWhiteSpace(assemblyVersion) ? null : assemblyVersion;
    }
}
