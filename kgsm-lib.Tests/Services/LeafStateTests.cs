using TheKrystalShip.KGSM.Events;
using TheKrystalShip.KGSM.Lifecycle;

namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Reading back what a producer's own journal last said about itself.
/// </summary>
/// <remarks>
/// The memory a process that exits does not have. Measured on the speech leaf: it reported a model
/// it could not load, exited when idle, woke with the model fixed, and wrote no recovery — because the
/// fresh process had never seen the fault.
/// </remarks>
public sealed class LeafStateTests : IDisposable
{
    private const string Producer = "kgsm-speech";

    private readonly string _root;
    private readonly string _directory;

    public LeafStateTests()
    {
        _root = Path.Combine(Path.GetTempPath(), "kgsm-leaf-state", Path.GetRandomFileName());
        _directory = JournalLayout.DirectoryFor(Producer, _root);
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_root, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
            // Already gone.
        }
    }

    [Fact]
    public void A_fault_with_no_recovery_after_it_is_still_broken()
    {
        Write(Degraded("hearing"), Degraded("speaking"), Recovered("speaking"));

        Assert.Equal(["hearing"], LeafState.DegradedComponents(_directory).Order());
    }

    [Fact]
    public void A_fault_that_was_recovered_is_not_carried_forward()
    {
        Write(Degraded("backend"), Recovered("backend"));

        Assert.Empty(LeafState.DegradedComponents(_directory));
    }

    [Fact]
    public void A_fault_reported_again_after_a_recovery_is_still_broken()
    {
        Write(Degraded("backend"), Recovered("backend"), Degraded("backend"));

        Assert.Equal(["backend"], LeafState.DegradedComponents(_directory));
    }

    [Fact]
    public void A_leaf_coming_up_wipes_the_slate()
    {
        // The line that separates the two kinds of leaf. A resident one writes leaf_ready on every
        // start, so everything before it described a run that has ended and its faults must not be
        // carried into a fresh process. A leaf that exits when idle writes no ready line, which is
        // exactly why its faults do carry.
        Write(Degraded("sampling"), Ready(), Degraded("net-meter"));

        Assert.Equal(["net-meter"], LeafState.DegradedComponents(_directory));
    }

    [Fact]
    public void Only_the_newest_segment_is_read()
    {
        // A fault that predates the segment boundary is not carried over. The alternative is opening
        // older files on every start of a leaf that may start dozens of times a day, to recover a
        // fault nothing has re-observed since midnight.
        File.WriteAllLines(Path.Combine(_directory, "2026-08-15.ndjson"), [Degraded("yesterday")]);
        File.WriteAllLines(Path.Combine(_directory, "2026-08-16.ndjson"), [Degraded("today")]);

        Assert.Equal(["today"], LeafState.DegradedComponents(_directory));
    }

    [Fact]
    public void An_ordinary_event_from_this_producer_changes_nothing()
    {
        Write(
            Degraded("hearing"),
            """{"V":1,"EventType":"server.started","Data":{"InstanceName":"a"},"Timestamp":"2026-08-16T10:00:00.000Z"}""");

        Assert.Equal(["hearing"], LeafState.DegradedComponents(_directory));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("""{"V":1,"EventType":"leaf.degraded","Data":{},"Timestamp":"2026-08-16T10:00:00.000Z"}""")]
    [InlineData("""{"V":1,"EventType":"leaf.degraded","Data":"x","Timestamp":"2026-08-16T10:00:00.000Z"}""")]
    public void A_line_that_names_no_component_says_nothing_about_the_state(string line)
    {
        Write(line);

        Assert.Empty(LeafState.DegradedComponents(_directory));
    }

    [Fact]
    public void A_journal_that_is_not_there_seeds_nothing()
    {
        // A state that cannot be read is not evidence of a fault, and half a replay would be worse
        // than none — it would carry faults forward past the recovery that cleared them.
        Assert.Empty(LeafState.DegradedComponents(Path.Combine(_root, "nowhere")));
        Assert.Empty(LeafState.DegradedComponents(null));
        Assert.Empty(LeafState.DegradedComponents("   "));
    }

    [Fact]
    public void A_producer_finds_its_own_journal_from_its_id()
    {
        Write(Degraded("hearing"));

        Assert.Equal(["hearing"], LeafState.DegradedComponentsFor(Producer, _root));
    }

    private void Write(params string[] lines) =>
        File.WriteAllLines(Path.Combine(_directory, "2026-08-16.ndjson"), lines);

    private static string Degraded(string component) => Line(LeafLifecycleEvents.Degraded, component);

    private static string Recovered(string component) => Line(LeafLifecycleEvents.Recovered, component);

    private static string Ready() =>
        """{"V":1,"EventType":"leaf.ready","Data":{"StartupMs":5},"Timestamp":"2026-08-16T10:00:00.000Z"}""";

    private static string Line(string type, string component) => $$"""
        {"V":1,"EventType":"{{type}}","Data":{"Component":"{{component}}"},"Timestamp":"2026-08-16T10:00:00.000Z"}
        """;
}
