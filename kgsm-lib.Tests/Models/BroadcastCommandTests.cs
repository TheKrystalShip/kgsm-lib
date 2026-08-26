using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Tests.Models;

/// <summary>
/// A blueprint's broadcast template answers one question — can this game be
/// announced to at all. These lock down the shapes that count as declared, and the
/// ones that must read as undeclared rather than resolve to a guess.
/// </summary>
public class BroadcastCommandTests
{
    [Theory]
    [InlineData("say {message}")]
    [InlineData("/say {message}")]
    [InlineData("servermsg \"{message}\"")]
    [InlineData("c_announce(\"{message}\")")]
    [InlineData("say \"{message}\" --all")]
    public void IsSupported_AcceptsATemplateCarryingThePlaceholder(string template)
    {
        Assert.True(BroadcastCommand.IsSupported(template));
    }

    [Fact]
    public void IsSupported_AcceptsATemplateThatIsOnlyThePlaceholder()
    {
        // A console that treats any bare line as chat declares this and nothing else.
        // Requiring a verb around the text would silently exclude every such game.
        Assert.True(BroadcastCommand.IsSupported("{message}"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsSupported_ReportsAnAbsentTemplateAsUnsupported(string? template)
    {
        // The game declares no broadcast command, so the announcement is refused
        // rather than approximated with a different one.
        Assert.False(BroadcastCommand.IsSupported(template));
    }

    [Theory]
    [InlineData("say")]
    [InlineData("broadcast --all")]
    [InlineData("say {text}")]
    [InlineData("say {msg}")]
    public void IsSupported_ReportsATemplateWithNoPlaceholderAsUnsupported(string template)
    {
        // Without the placeholder the engine would send the bare verb and drop the
        // text — a different command than the one asked for. Reporting it as
        // undeclared matches what the engine does when it refuses to send it.
        Assert.False(BroadcastCommand.IsSupported(template));
    }

    [Fact]
    public void IsSupported_IsCaseSensitiveOnThePlaceholder()
    {
        // The engine substitutes on the exact token; a differently-cased one would
        // not be replaced, so reporting it as supported would promise a send that
        // arrives as a bare verb.
        Assert.False(BroadcastCommand.IsSupported("say {MESSAGE}"));
    }
}
