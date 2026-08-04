using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Tests.Models;

/// <summary>
/// The placeholder in a blueprint's moderation template IS the identity contract —
/// a caller reads it to know which field of a player record to send. These lock that
/// reading down, including the cases that must report "no usable contract" rather
/// than resolve to a guess.
/// </summary>
public class ModerationCommandTests
{
    [Theory]
    [InlineData("kick {ip}", ModerationTargetKind.Ip)]
    [InlineData("ban {ip}", ModerationTargetKind.Ip)]
    [InlineData("unban {ip}", ModerationTargetKind.Ip)]
    [InlineData("ban {name}", ModerationTargetKind.Name)]
    [InlineData("banid {id}", ModerationTargetKind.Id)]
    public void TryGetTargetKind_ReadsTheKindFromThePlaceholder(string template, ModerationTargetKind expected)
    {
        Assert.True(ModerationCommand.TryGetTargetKind(template, out ModerationTargetKind kind));
        Assert.Equal(expected, kind);
    }

    [Theory]
    [InlineData("kick {ip} --now")]
    [InlineData("ban {name} permanently")]
    [InlineData("/kick {id}")]
    public void TryGetTargetKind_IgnoresTextAroundThePlaceholder(string template)
    {
        // Placeholder position is a game's business — some games put the token last,
        // others take a trailing reason. Only the placeholder itself is read.
        Assert.True(ModerationCommand.TryGetTargetKind(template, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void TryGetTargetKind_AbsentTemplate_IsUnsupported(string? template)
    {
        // Empty means the game declares no such command. The action is refused, never
        // approximated with a different one.
        Assert.False(ModerationCommand.TryGetTargetKind(template, out _));
        Assert.False(ModerationCommand.IsSupported(template));
    }

    [Theory]
    [InlineData("kick")]
    [InlineData("kick {player}")]
    [InlineData("kick {}")]
    public void TryGetTargetKind_NoRecognisedPlaceholder_IsUnsupported(string template)
    {
        // A bare verb would send the command with no target at all — a different
        // command than the one asked for.
        Assert.False(ModerationCommand.TryGetTargetKind(template, out _));
    }

    [Theory]
    [InlineData("ban {ip} {name}")]
    [InlineData("ban {name} {id}")]
    public void TryGetTargetKind_MoreThanOnePlaceholder_IsUnsupported(string template)
    {
        // An ambiguous template names no single identity, so there is nothing to tell a
        // caller to send. Reported as undeclared rather than resolved to whichever
        // placeholder happens to be checked first.
        Assert.False(ModerationCommand.TryGetTargetKind(template, out _));
    }

    [Fact]
    public void IsSupported_TracksTryGetTargetKind()
    {
        Assert.True(ModerationCommand.IsSupported("kick {ip}"));
        Assert.False(ModerationCommand.IsSupported("kick"));
    }
}
