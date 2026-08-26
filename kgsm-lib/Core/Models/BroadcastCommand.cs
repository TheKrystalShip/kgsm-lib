namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Reads the broadcast contract out of a blueprint's <c>broadcast_command</c>
/// template.
/// </summary>
/// <remarks>
/// <para>
/// A template carries exactly one <c>{message}</c> placeholder, and everything
/// around it is sent verbatim. Unlike the moderation templates, the placeholder
/// names no identity the caller must resolve — the payload is prose — so the only
/// question this type answers is whether the game declares a broadcast at all.
/// </para>
/// <para>
/// <b>The placeholder may be the whole template.</b> A console that treats any bare
/// line as chat declares <c>{message}</c> and nothing else, which is a valid
/// template and not a malformed one.
/// </para>
/// <para>
/// Substitution is deliberately <em>not</em> done here. The engine's management
/// script resolves the template against the message it is handed, so a second
/// implementation of the same substitution on this side would be a second answer
/// that could disagree with the one that actually runs. This type answers only
/// "can this game be announced to", never "what will be sent".
/// </para>
/// </remarks>
public static class BroadcastCommand
{
    private const string MessagePlaceholder = "{message}";

    /// <summary>
    /// Indicates whether <paramref name="template"/> declares a usable broadcast
    /// command — that is, whether an announcement is supported for this game.
    /// </summary>
    /// <param name="template">
    /// A broadcast command template, e.g. <c>say {message}</c>. A null, empty or
    /// whitespace value means the game declares no broadcast command.
    /// </param>
    /// <returns>
    /// <see langword="true"/> when the template carries the <c>{message}</c>
    /// placeholder; <see langword="false"/> when it is absent or the template
    /// carries no placeholder. A template with no placeholder would send its bare
    /// verb and drop the text, so it is reported as undeclared rather than resolved
    /// to a guess — the same posture the engine takes when it refuses to send one.
    /// </returns>
    public static bool IsSupported(string? template) =>
        !string.IsNullOrWhiteSpace(template)
        && template.Contains(MessagePlaceholder, StringComparison.Ordinal);
}
