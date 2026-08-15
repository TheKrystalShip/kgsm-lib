using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// What a component needs to write its own event journal.
/// </summary>
/// <remarks>
/// Separate from a consumer's <c>KgsmOptions</c> on purpose. Those options describe a consumer's
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
    public static string DefaultDirectoryFor(string producer) => JournalLayout.DirectoryFor(producer);

    /// <summary>
    /// Gets or sets who this component is when it writes. Required.
    /// </summary>
    /// <remarks>
    /// Fixed for the lifetime of the writer, and never taken from a caller of
    /// <c>IEventJournalWriter.AppendAsync</c>: a producer id that could be supplied per event would be
    /// a claim about authorship rather than a fact about the writer.
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

    /// <summary>
    /// What is wrong with writing to <see cref="Directory"/> as <see cref="Producer"/>, or null when
    /// nothing is.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A reader never takes a producer's word for who it is — it derives that from the directory a
    /// line came from. So these options are wrong, however well-formed they look, if a reader finding
    /// this journal would attribute it to somebody else or would not find it at all. This asks
    /// <see cref="JournalLayout.ProducerOf"/> exactly what a reader concludes and compares it against
    /// what this producer believes, rather than restating the rule a second time.
    /// </para>
    /// <para>
    /// ⚠ Both failures are silent at runtime. A journal a reader cannot attribute is not reported as
    /// unreadable — it is not found, and a producer with no journal has honestly recorded nothing. The
    /// events are written, the writes succeed, and the record is invisible.
    /// </para>
    /// <para>
    /// A relocated state root is <b>not</b> a mismatch: only the two path segments that carry meaning
    /// are examined, so a test writing under a temporary root, or a host that keeps state elsewhere,
    /// stays conventional.
    /// </para>
    /// </remarks>
    /// <returns>A description of the mismatch, or null when a reader would attribute this correctly.</returns>
    public string? DescribeDirectoryMismatch()
    {
        if (!JournalProducer.IsValid(Producer) || string.IsNullOrWhiteSpace(Directory))
            return null;

        string? derived = JournalLayout.ProducerOf(Directory);

        if (derived is null)
        {
            return $"Journal directory '{Directory}' is not a location any reader scans, so events "
                + $"written there are attributed to no producer and '{Producer}' reads as a component "
                + $"that has recorded nothing. Expected a path ending "
                + $"'<state-root>/{Producer}/{JournalLayout.Subdirectory}'.";
        }

        if (!string.Equals(derived, Producer, StringComparison.Ordinal))
        {
            return $"Journal directory '{Directory}' is read as producer '{derived}', not '{Producer}': "
                + $"every event written there is attributed to '{derived}'.";
        }

        return null;
    }
}
