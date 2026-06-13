using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Whether a <see cref="Reading{T}"/> carries a real measurement and, if not,
/// the kind of absence. The discriminator between <see cref="Unavailable"/> and
/// <see cref="Skipped"/> is "did we attempt the fetch?" — skipped never started;
/// unavailable started but came back empty (a timeout is always
/// <see cref="Unavailable"/>, never <see cref="Skipped"/>).
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReadingState>))]
public enum ReadingState
{
    /// <summary>A real value is present in <see cref="Reading{T}.Value"/>.</summary>
    Measured,

    /// <summary>
    /// Permanent absence — the source can never produce this (e.g. a game with
    /// no player-query protocol). Surfaces render "N/A".
    /// </summary>
    Unsupported,

    /// <summary>
    /// Attempted but produced nothing — source down, monitor offline, or the
    /// fetch hit its deadline. Retryable.
    /// </summary>
    Unavailable,

    /// <summary>
    /// Never attempted — we chose not to run it (e.g. a fast/brief read that
    /// does not fetch this field).
    /// </summary>
    Skipped,
}

/// <summary>
/// A machine-readable cause that subdivides <see cref="ReadingState.Unavailable"/>
/// or <see cref="ReadingState.Skipped"/>. It never echoes the state: there is no
/// code for <see cref="ReadingState.Unsupported"/> (that is fully expressed by the
/// state itself), and a "deadline vs down" distinction is a code, not a state.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter<ReadingCode>))]
public enum ReadingCode
{
    /// <summary>The instance's management file must be regenerated before its status can be read.</summary>
    RequiresRegeneration,

    /// <summary>The fetch hit its deadline.</summary>
    DeadlineExceeded,

    /// <summary>The metrics monitor was not reachable.</summary>
    MonitorOffline,

    /// <summary>The source attempted the read and returned an error.</summary>
    SourceError,
}

/// <summary>
/// A measurement that may be absent, carrying <em>why</em> it is absent rather
/// than a masquerading default. A bare <c>null</c>/<c>0</c>/<c>false</c>/<c>""</c>
/// used as a "we had nothing, so we filled the slot" filler is the exact
/// silent-drift bug this type exists to close: a consumer (the LLM or a surface)
/// must be able to tell a real measurement from an unmeasured slot, and "we'll
/// never know" from "try again in a second."
/// </summary>
/// <remarks>
/// State is decided where the data is read — the capability/port is the only
/// thing that knows whether it measured something; the envelope just carries it
/// up. Each concrete <c>Reading&lt;T&gt;</c> the library deserializes needs a
/// <c>KgsmJsonContext</c> registration (Native-AOT, reflection-free).
/// </remarks>
/// <typeparam name="T">The measured value type.</typeparam>
public sealed record Reading<T>
{
    /// <summary>Whether a measurement is present and, if not, the kind of absence.</summary>
    [JsonPropertyName("state")]
    public ReadingState State { get; init; }

    /// <summary>
    /// The measured value. Meaningful (and, for reference types, non-null) only
    /// when <see cref="State"/> is <see cref="ReadingState.Measured"/>.
    /// </summary>
    [JsonPropertyName("value")]
    public T? Value { get; init; }

    /// <summary>Human-readable explanation of an absence. Null when measured.</summary>
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    /// <summary>
    /// Machine-readable cause subdividing an <see cref="ReadingState.Unavailable"/>
    /// or <see cref="ReadingState.Skipped"/> absence. Null when measured or when no
    /// finer cause applies.
    /// </summary>
    [JsonPropertyName("code")]
    public ReadingCode? Code { get; init; }

    /// <summary>True when a real value is present.</summary>
    [JsonIgnore]
    public bool IsMeasured => State == ReadingState.Measured;

    /// <summary>Builds a present measurement.</summary>
    public static Reading<T> Measured(T value) =>
        new() { State = ReadingState.Measured, Value = value };

    /// <summary>Builds an attempted-but-empty (retryable) reading.</summary>
    public static Reading<T> Unavailable(string? reason = null, ReadingCode? code = null) =>
        new() { State = ReadingState.Unavailable, Reason = reason, Code = code };

    /// <summary>Builds a permanently-absent reading.</summary>
    public static Reading<T> Unsupported(string? reason = null) =>
        new() { State = ReadingState.Unsupported, Reason = reason };

    /// <summary>Builds a deliberately-not-attempted reading.</summary>
    public static Reading<T> Skipped(string? reason = null, ReadingCode? code = null) =>
        new() { State = ReadingState.Skipped, Reason = reason, Code = code };
}
