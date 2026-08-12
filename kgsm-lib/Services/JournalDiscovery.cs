using System.Text.Json;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Events;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Works out which event journals this host has, from the leaves that are installed on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>A journal exists because a producer is installed, not because a list says so.</b> The engine's
/// journal is always present — kgsm is the engine, not a leaf. Every other journal is discovered from
/// the leaf descriptors in <c>/var/lib/kgsm/leaves/</c>, the directory each leaf's deploy already
/// installs into. So a leaf that is not installed contributes no journal and needs excluding from
/// nothing, and a leaf installed later is picked up without any consumer being rebuilt.
/// </para>
/// <para>
/// <b>A leaf's journal directory is declared in its descriptor, never derived from its name.</b> Only
/// the leaf knows where it can write. This host has one leaf whose unit and state directory differ
/// (<c>kgsm-assistant-service</c> versus <c>kgsm-assistant</c>) and two with no state directory at
/// all, so a naming convention would be right for most and silently wrong for the rest — and wrong is
/// expensive both ways, because the writer cannot create a directory under root-owned
/// <c>/var/lib</c> and drops the event, while a reader looks in the same empty place and calls the
/// producer unreadable forever.
/// </para>
/// <para>
/// A leaf that declares no journal directory writes no journal, which is the honest answer for one
/// that records nothing of its own. Discovery therefore tracks exactly which producers exist at any
/// point in the migration, with nothing to exclude and nothing guessed.
/// </para>
/// <para>
/// A descriptor that cannot be read, or whose id does not make a usable producer, is logged and
/// skipped. It is not fatal: one unreadable descriptor must not cost a consumer the journals it could
/// otherwise have read, and a missing journal is already reported per-producer by the readers.
/// </para>
/// </remarks>
public sealed class JournalDiscovery : IJournalDiscovery
{
    /// <summary>The directory each leaf's deploy installs its descriptor into.</summary>
    public const string DefaultLeavesDirectory = "/var/lib/kgsm/leaves";

    /// <summary>
    /// The prefix a leaf's producer id carries, ahead of its descriptor id.
    /// </summary>
    /// <remarks>
    /// The producer <em>id</em> is derived (<c>kgsm-</c> + the descriptor's unique id) while the
    /// <em>directory</em> is declared, because the two answer different questions. An id only has to be
    /// unique and stable, which a per-host-unique descriptor id already is; a path has to be somewhere
    /// the leaf can actually write, which nothing but the leaf can know.
    /// </remarks>
    public const string ProducerPrefix = "kgsm-";

    private readonly string _engineJournalDirectory;
    private readonly string _leavesDirectory;
    private readonly ILogger<JournalDiscovery> _logger;

    /// <summary>
    /// Initializes discovery over an engine journal and a leaves directory.
    /// </summary>
    /// <param name="engineJournalDirectory">Where kgsm's own journal segments live.</param>
    /// <param name="leavesDirectory">Where installed leaf descriptors live.</param>
    /// <param name="logger">The logger to use.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="logger"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when either directory is blank.</exception>
    public JournalDiscovery(
        string engineJournalDirectory, string leavesDirectory, ILogger<JournalDiscovery> logger)
    {
        ArgumentNullException.ThrowIfNull(logger, nameof(logger));
        ArgumentException.ThrowIfNullOrWhiteSpace(engineJournalDirectory, nameof(engineJournalDirectory));
        ArgumentException.ThrowIfNullOrWhiteSpace(leavesDirectory, nameof(leavesDirectory));

        _engineJournalDirectory = engineJournalDirectory;
        _leavesDirectory = leavesDirectory;
        _logger = logger;
    }

    /// <inheritdoc/>
    public IReadOnlyList<JournalSource> Discover()
    {
        // The engine always comes first, and unconditionally: it is the one producer that is not a
        // leaf, so nothing declares it and nothing can make it absent.
        var sources = new List<JournalSource>
        {
            new(JournalProducer.Kgsm, _engineJournalDirectory),
        };

        var seen = new HashSet<string>(StringComparer.Ordinal) { JournalProducer.Kgsm };

        foreach (JournalSource leaf in DiscoverLeafJournals())
        {
            // A producer named twice would make the ids derived from the two journals collide, which is
            // the one thing the producer prefix exists to prevent. First declaration wins.
            if (seen.Add(leaf.Producer))
                sources.Add(leaf);
        }

        _logger.LogDebug(
            "Discovered {Count} event journals: {Producers}",
            sources.Count, string.Join(", ", sources.Select(static s => s.Producer)));

        return sources;
    }

    /// <summary>
    /// Every installed leaf that declares a journal, in a stable order.
    /// </summary>
    /// <remarks>
    /// Sorted by producer so a merged page's cross-journal tie-break is the same on every host and
    /// every restart. Directory enumeration order is not guaranteed, and an ordering that varied per
    /// process would make two readers of the same record disagree about which of two simultaneous
    /// events came first.
    /// </remarks>
    private IEnumerable<JournalSource> DiscoverLeafJournals()
    {
        string[] files;

        try
        {
            if (!Directory.Exists(_leavesDirectory))
            {
                _logger.LogDebug(
                    "No leaf descriptor directory at {Directory}; only the engine journal is known",
                    _leavesDirectory);
                return [];
            }

            files = Directory.GetFiles(_leavesDirectory, "*.json");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex,
                "Could not list leaf descriptors in {Directory}; only the engine journal is known",
                _leavesDirectory);
            return [];
        }

        var journals = new List<JournalSource>(files.Length);

        foreach (string file in files)
        {
            if (ReadJournal(file) is { } journal)
                journals.Add(journal);
        }

        journals.Sort(static (a, b) => string.CompareOrdinal(a.Producer, b.Producer));
        return journals;
    }

    /// <summary>
    /// The journal a descriptor declares, or null when it declares none or cannot be used.
    /// </summary>
    private JournalSource? ReadJournal(string file)
    {
        LeafJournalDescriptor? descriptor;

        try
        {
            string json = File.ReadAllText(file);
            descriptor = JsonSerializer.Deserialize(json, KgsmJsonContext.Default.LeafJournalDescriptor);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            _logger.LogWarning(ex, "Leaf descriptor {File} could not be read; its journal is not known", file);
            return null;
        }

        // No declared directory means this leaf writes no journal. Not a warning: it is the correct
        // answer for a leaf that records nothing of its own.
        if (string.IsNullOrWhiteSpace(descriptor?.JournalDirectory))
            return null;

        if (string.IsNullOrWhiteSpace(descriptor.Id))
        {
            _logger.LogWarning(
                "Leaf descriptor {File} declares a journal but no id, so its producer cannot be named",
                file);
            return null;
        }

        string producer = ProducerPrefix + descriptor.Id.Trim();

        if (!JournalProducer.IsValid(producer))
        {
            _logger.LogWarning(
                "Leaf descriptor {File} has id '{Id}', which does not make a usable journal producer id",
                file, descriptor.Id);
            return null;
        }

        return new JournalSource(producer, descriptor.JournalDirectory.Trim());
    }
}
