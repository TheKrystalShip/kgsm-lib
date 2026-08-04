using TheKrystalShip.KGSM.Core.Models.Enums;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Reads the player-identity contract out of a blueprint's moderation command
/// template (<c>kick_command</c> / <c>ban_command</c> / <c>unban_command</c>).
/// </summary>
/// <remarks>
/// <para>
/// A template carries exactly one placeholder and the placeholder <em>names</em>
/// the identity token the game expects: <c>{ip}</c>, <c>{name}</c> or <c>{id}</c>.
/// A caller reads that token to know which field of a player record to pass —
/// that is the whole contract, and it lives in one place so nothing can drift
/// out of agreement with it.
/// </para>
/// <para>
/// Substitution is deliberately <em>not</em> done here. The engine's management
/// script resolves the template against the target it is handed, so a second
/// implementation of the same substitution on this side would be a second answer
/// that could disagree with the one that actually runs. This type answers only
/// "what does this game want", never "what will be sent".
/// </para>
/// </remarks>
public static class ModerationCommand
{
    private const string IpPlaceholder = "{ip}";
    private const string NamePlaceholder = "{name}";
    private const string IdPlaceholder = "{id}";

    /// <summary>
    /// Determines which player identity <paramref name="template"/> asks for.
    /// </summary>
    /// <param name="template">
    /// A moderation command template, e.g. <c>kick {ip}</c>. A null, empty or
    /// whitespace value means the game declares no such command.
    /// </param>
    /// <param name="kind">
    /// When this method returns <see langword="true"/>, the identity kind the
    /// template's placeholder names; otherwise the default value.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the template declares exactly one recognised
    /// placeholder; <see langword="false"/> when it is absent, carries none, or
    /// carries more than one (an ambiguous template names no single identity, so
    /// it is reported as undeclared rather than resolved to a guess).
    /// </returns>
    public static bool TryGetTargetKind(string? template, out ModerationTargetKind kind)
    {
        kind = default;

        if (string.IsNullOrWhiteSpace(template))
        {
            return false;
        }

        bool hasIp = template.Contains(IpPlaceholder, StringComparison.Ordinal);
        bool hasName = template.Contains(NamePlaceholder, StringComparison.Ordinal);
        bool hasId = template.Contains(IdPlaceholder, StringComparison.Ordinal);

        int declared = (hasIp ? 1 : 0) + (hasName ? 1 : 0) + (hasId ? 1 : 0);
        if (declared != 1)
        {
            return false;
        }

        kind = hasIp ? ModerationTargetKind.Ip
             : hasName ? ModerationTargetKind.Name
             : ModerationTargetKind.Id;

        return true;
    }

    /// <summary>
    /// Indicates whether <paramref name="template"/> declares a usable moderation
    /// command — that is, whether the action is supported at all for this game.
    /// </summary>
    /// <param name="template">The moderation command template to inspect.</param>
    /// <returns>
    /// <see langword="true"/> when the action is supported; <see langword="false"/>
    /// when the game declares no command for it. An unsupported action is refused
    /// by the engine rather than approximated with a different command.
    /// </returns>
    public static bool IsSupported(string? template) =>
        TryGetTargetKind(template, out _);
}
