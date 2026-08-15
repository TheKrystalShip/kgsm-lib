using System.Reflection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Events;
using TheKrystalShip.KGSM.Services;

namespace TheKrystalShip.KGSM.Extensions;

/// <summary>
/// Registers a component's own event journal writer.
/// </summary>
/// <remarks>
/// Being a producer is four decisions — which name to write under, which directory, which version to
/// stamp, and when the directory comes into existence — and every one of them has a single right
/// answer that a producer can derive rather than choose. Made once here so no repo makes them again.
/// </remarks>
public static class JournalServiceCollectionExtensions
{
    /// <summary>
    /// The variable that relocates every producer's journal on this machine.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deriving the journal path from the producer id is what keeps a writer and every reader agreed
    /// on it — and it also means a component run by hand writes exactly where the deployed one does.
    /// A developer running a leaf from a checkout, or a test suite exercising one, would append to the
    /// host's real audit record. <b>Fabricated history is worse than none</b>, so there has to be one
    /// way to move the whole layout aside.
    /// </para>
    /// <para>
    /// One variable rather than a per-leaf setting, because it is one concern: a process either writes
    /// to this host's journals or to a throwaway root, and that is never a question about a particular
    /// leaf. It relocates the <em>root</em> and never the rule — a producer keeps its own name and its
    /// own <c>events</c> subdirectory under it, so a relocated journal is still readable by pointing a
    /// reader's state root at the same place.
    /// </para>
    /// </remarks>
    public const string StateRootVariable = "KGSM_JOURNAL_STATE_ROOT";

    /// <summary>
    /// Registers <see cref="IEventJournalWriter"/> for <paramref name="producer"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The producer id decides the directory (<see cref="JournalLayout.DirectoryFor"/>), which is what
    /// keeps the writer and every reader agreed on where this journal is without either being told.
    /// The version comes from <paramref name="versionSource"/> through
    /// <see cref="ProducerVersion.Of"/>, so one build identity reaches every event.
    /// </para>
    /// <para>
    /// The journal directory is created here, at registration, rather than on the first event: a
    /// consumer discovers producers when it starts, so a producer whose directory appears later is
    /// invisible until it emits <em>and</em> every consumer restarts. Creating it during startup means
    /// a deployed producer is discoverable from the moment it runs, whether or not it has yet had
    /// anything to record.
    /// </para>
    /// <para>
    /// ⚠ <paramref name="configure"/> is for the producer whose journal genuinely does not sit at the
    /// convention — a configurable path, or a test isolating itself under a temporary root. It is not
    /// the normal case, and a directory a reader would attribute to somebody else is reported by the
    /// writer at construction rather than accepted quietly.
    /// </para>
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="producer">
    /// This component's producer id — its state directory's own name, which is what a reader derives.
    /// </param>
    /// <param name="versionSource">
    /// The assembly whose version identifies this build. Passed explicitly rather than inferred from
    /// the call stack, which inlining and AOT both make unreliable.
    /// </param>
    /// <param name="stateRoot">
    /// Where state directories live. Null reads <see cref="StateRootVariable"/>, and falls back to
    /// <see cref="JournalLayout.DefaultStateRoot"/> when that is unset.
    /// </param>
    /// <param name="configure">Adjusts the options before the writer is built. Optional.</param>
    /// <returns>The service collection, so calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="versionSource"/> is null.
    /// </exception>
    /// <exception cref="ArgumentException">Thrown when the producer id is unusable.</exception>
    public static IServiceCollection AddKgsmJournal(
        this IServiceCollection services,
        string producer,
        Assembly versionSource,
        string? stateRoot = null,
        Action<EventJournalWriterOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(versionSource, nameof(versionSource));
        JournalProducer.Validate(producer, nameof(producer));

        string root = stateRoot
            ?? Environment.GetEnvironmentVariable(StateRootVariable)
            ?? JournalLayout.DefaultStateRoot;

        var options = new EventJournalWriterOptions
        {
            Producer = producer,
            Directory = JournalLayout.DirectoryFor(producer, root),
            ProducerVersion = ProducerVersion.Of(versionSource),
        };

        configure?.Invoke(options);
        options.Validate();

        // Before the container is built, so there is no logger to report a failure to. The writer
        // creates it as well and says so properly if it cannot, which is the right moment: a
        // permission problem matters when it costs an event, and it will cost one then too.
        TryCreateDirectory(options.Directory!);

        services.AddSingleton<IEventJournalWriter>(sp => new EventJournalWriter(
            options, sp.GetRequiredService<ILogger<EventJournalWriter>>()));

        return services;
    }

    private static void TryCreateDirectory(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // Swallowed deliberately, and only here. Failing a component's startup because its audit
            // directory could not be made would take a working service down over its record-keeping,
            // and the writer reports the same problem with a logger once the container exists.
        }
    }
}
