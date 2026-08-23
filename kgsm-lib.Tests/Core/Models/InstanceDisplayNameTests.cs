namespace TheKrystalShip.KGSM.Tests.Core.Models;

/// <summary>
/// The label is stored in the instance's <c>.config.ini</c> as plain text, so what it may contain is
/// decided by what the engine can read back out of that file. These pin the two characters that
/// cannot survive the trip, and that everything else is left exactly as somebody typed it.
/// </summary>
public class InstanceDisplayNameTests
{
    [Theory]
    [InlineData("Weekend Server")]
    [InlineData("Ana's \"Best\" Server")]
    [InlineData(@"C:\path\to\nowhere")]
    [InlineData("cost: $100 `uname`")]
    [InlineData("Sûper Ćool 🎮🔥 Server")]
    [InlineData("--help")]
    [InlineData("日本語のサーバー")]
    public void Printable_text_survives_untouched(string label)
    {
        // Quotes, backslashes and backticks are the engine's to escape, and it does. Reproducing any
        // of that here would be a second answer that could disagree with the engine's own.
        Assert.Equal(label, InstanceDisplayName.Sanitize(label));
    }

    [Fact]
    public void A_tab_goes_because_it_truncates_the_value_the_engine_reads_back()
    {
        // The engine renders the config to JSON with a line-oriented parse that separates key from
        // value with a tab. A tab inside the value ends it there, silently, and only on that path —
        // `config-list --json` still returns the whole thing, so the two disagree.
        Assert.Equal("AB", InstanceDisplayName.Sanitize("A\tB"));
    }

    [Fact]
    public void A_newline_goes_because_the_rest_of_the_label_parses_as_config_keys()
    {
        // Worse than truncation: the remainder becomes its own key=value line in the file, and one
        // of the keys reachable that way is `name` — the id. An instance can be made to report an id
        // that is not its own, permanently, because nothing later rewrites the orphaned line.
        Assert.Equal("Nicename=victim", InstanceDisplayName.Sanitize("Nice\nname=victim"));
        Assert.Equal("Niceruntime=container", InstanceDisplayName.Sanitize("Nice\r\nruntime=container"));
    }

    [Theory]
    [InlineData("  Weekend Server  ", "Weekend Server")]
    [InlineData("\tWeekend Server\n", "Weekend Server")]
    public void Surrounding_whitespace_is_trimmed(string label, string expected)
    {
        Assert.Equal(expected, InstanceDisplayName.Sanitize(label));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    public void Nothing_readable_is_the_cleared_state(string? label)
    {
        // Empty is a real answer, not a failure: an instance with no label of its own reads as its id.
        Assert.Equal(string.Empty, InstanceDisplayName.Sanitize(label));
    }
}
