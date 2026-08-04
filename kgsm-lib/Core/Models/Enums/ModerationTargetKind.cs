using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// The kind of player identity a game's moderation commands address.
/// </summary>
/// <remarks>
/// A blueprint declares this by the placeholder it writes into its moderation
/// templates — <c>kick {ip}</c> says both "the verb is kick" and "hand it an IP
/// address". The kind is therefore read out of the template rather than
/// configured beside it, where the two could disagree and one of them would be
/// describing a substitution that does not happen.
/// </remarks>
[JsonConverter(typeof(JsonStringEnumConverter<ModerationTargetKind>))]
public enum ModerationTargetKind
{
    /// <summary>An IP address — the <c>{ip}</c> placeholder.</summary>
    Ip,

    /// <summary>A player or character name — the <c>{name}</c> placeholder.</summary>
    Name,

    /// <summary>An account or user id — the <c>{id}</c> placeholder.</summary>
    Id
}
