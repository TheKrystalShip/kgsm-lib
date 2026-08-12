using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// What a component needs to write its own event journal.
/// </summary>
/// <remarks>
/// Separate from <see cref="KgsmOptions"/> on purpose. Those options describe a consumer's
/// relationship with the engine — where kgsm is, which journal it reads, where it keeps its cursor.
/// These describe a component's own identity as a <em>producer</em>, which is a different role: a
/// process can hold one, both, or neither.
/// </remarks>
public sealed class EventJournalWriterOptions
{
    /// <summary>
    /// The directory a producer's journal lives in by convention — its own state directory, so
    /// uninstalling the component takes its journal with it and no producer needs write access to
    /// another's.
    /// </summary>
    /// <param name="producer">The producer id.</param>
    /// <returns>The conventional journal directory for that producer.</returns>
    public static string DefaultDirectoryFor(string producer)
    {
        JournalProducer.Validate(producer, nameof(producer));
        return $"/var/lib/{producer}/events";
    }

    /// <summary>
    /// Gets or sets who this component is when it writes. Required.
    /// </summary>
    /// <remarks>
    /// Fixed for the lifetime of the writer, and never taken from a caller of
    /// <see cref="Interfaces.IEventJournalWriter.AppendAsync"/>: a producer id that could be supplied
    /// per event would be a claim about authorship rather than a fact about the writer.
    /// </remarks>
    public string Producer { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the directory to append segments to. Defaults from <see cref="Producer"/> via
    /// <see cref="DefaultDirectoryFor"/> when left unset.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>
    /// Gets or sets this component's own build version, stamped on every event. Null omits the
    /// field rather than writing an empty one.
    /// </summary>
    public string? ProducerVersion { get; set; }

    /// <summary>
    /// Gets or sets the hostname to stamp. Defaults to this machine's name.
    /// </summary>
    /// <remarks>
    /// Written for the sake of a journal read on its own; a reader that knows where it got a line
    /// from trusts that over this field, because it established the one and was told the other.
    /// </remarks>
    public string? Hostname { get; set; } = Environment.MachineName;

    /// <summary>
    /// Gets or sets the clock, for tests. Null uses <see cref="DateTimeOffset.UtcNow"/>.
    /// </summary>
    public Func<DateTimeOffset>? Clock { get; set; }

    /// <summary>
    /// Throws unless these options can be used, and fills <see cref="Directory"/> from
    /// <see cref="Producer"/> when it was left unset.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when the producer id is unusable.</exception>
    public void Validate()
    {
        JournalProducer.Validate(Producer, nameof(Producer));

        if (string.IsNullOrWhiteSpace(Directory))
            Directory = DefaultDirectoryFor(Producer);
    }
}
