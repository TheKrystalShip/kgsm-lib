namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// The name of an event, and the reason a malformed one cannot be written.
/// </summary>
/// <remarks>
/// <para>
/// <b>There is no conversion from <see cref="string"/>.</b> A raw literal cannot reach the writer, so
/// the only way to name an event is to hold one of these — which means a producer's names are
/// declared in one place per producer rather than spelled at each call site. A check that fires is a
/// break that already happened; this is the shape of the contract that cannot break.
/// </para>
/// <para>
/// <b>Shape, never membership.</b> This validates that a name looks like a name. It holds no list of
/// valid names and never will: a registry here would mean every new leaf event needed a release of
/// this package and a re-pin in every consumer before it could be written, which is exactly the
/// coupling the journal exists without. A leaf mints names nothing else has heard of, and that is
/// the design working.
/// </para>
/// <para>
/// A name is dot-separated segments of lowercase letters, digits and underscores, each starting with
/// a letter. <see cref="IsNamespaced"/> reports whether there is more than one segment — the form a
/// reader can group on, since the hierarchy is what a consumer keys its presentation off.
/// </para>
/// </remarks>
public readonly struct EventName : IEquatable<EventName>
{
    private readonly string? _value;

    private EventName(string value) => _value = value;

    /// <summary>The name as the wire spells it. Empty for a default-constructed value.</summary>
    public string Value => _value ?? string.Empty;

    /// <summary>Whether this is a default-constructed value, which names nothing.</summary>
    public bool IsEmpty => string.IsNullOrEmpty(_value);

    /// <summary>
    /// Whether the name carries more than one segment, and can therefore be grouped on.
    /// </summary>
    public bool IsNamespaced => _value is not null && _value.Contains('.', StringComparison.Ordinal);

    /// <summary>
    /// The leading segment — the family a consumer groups by.
    /// </summary>
    /// <returns>The text before the first dot, or the whole name when there is no dot.</returns>
    public string Namespace
    {
        get
        {
            if (_value is null) return string.Empty;
            int dot = _value.IndexOf('.', StringComparison.Ordinal);
            return dot < 0 ? _value : _value[..dot];
        }
    }

    /// <summary>
    /// Reads a name, throwing when it is not one.
    /// </summary>
    /// <remarks>
    /// A hyphen is accepted and normalised to an underscore, because the engine's command line names
    /// events with hyphens and a call site bridging the two should not be able to produce a spelling
    /// no consumer recognises.
    /// </remarks>
    /// <param name="value">The candidate name.</param>
    /// <returns>The name.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="value"/> is not a valid name.</exception>
    public static EventName Parse(string value)
    {
        if (!TryParse(value, out EventName name))
        {
            throw new ArgumentException(
                $"'{value}' is not a valid event name: expected dot-separated segments of lowercase " +
                "letters, digits and underscores, each starting with a letter.",
                nameof(value));
        }

        return name;
    }

    /// <summary>Reads a name, reporting rather than throwing when it is not one.</summary>
    /// <param name="value">The candidate name.</param>
    /// <param name="name">The name, when it is one.</param>
    /// <returns>True when <paramref name="value"/> is a valid name.</returns>
    public static bool TryParse(string? value, out EventName name)
    {
        name = default;

        if (string.IsNullOrWhiteSpace(value)) return false;

        string normalized = value.Replace('-', '_');

        if (!IsWellFormed(normalized)) return false;

        name = new EventName(normalized);
        return true;
    }

    /// <summary>
    /// Whether a string has the shape of an event name.
    /// </summary>
    /// <remarks>
    /// Hand-written rather than a regular expression: this assembly is AOT-compatible, and a compiled
    /// regex is the kind of dependency a package held by a root-running daemon does not need for a
    /// character check.
    /// </remarks>
    /// <param name="value">The candidate, already normalised.</param>
    /// <returns>True when every segment is well formed and there is at least one.</returns>
    public static bool IsWellFormed(string? value)
    {
        if (string.IsNullOrEmpty(value)) return false;
        if (value[0] == '.' || value[^1] == '.') return false;

        bool startOfSegment = true;

        foreach (char c in value)
        {
            if (c == '.')
            {
                // An empty segment ("a..b") leaves the flag set from the dot before it.
                if (startOfSegment) return false;
                startOfSegment = true;
                continue;
            }

            if (startOfSegment)
            {
                if (c is < 'a' or > 'z') return false;
                startOfSegment = false;
                continue;
            }

            bool ok = c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_';
            if (!ok) return false;
        }

        return !startOfSegment;
    }

    /// <inheritdoc/>
    public bool Equals(EventName other) =>
        string.Equals(Value, other.Value, StringComparison.Ordinal);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is EventName other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => StringComparer.Ordinal.GetHashCode(Value);

    /// <inheritdoc/>
    public override string ToString() => Value;

    /// <summary>The name as the wire spells it.</summary>
    /// <param name="name">The name.</param>
    public static implicit operator string(EventName name) => name.Value;

    /// <summary>Whether two names are the same name.</summary>
    /// <param name="left">One name.</param>
    /// <param name="right">The other.</param>
    public static bool operator ==(EventName left, EventName right) => left.Equals(right);

    /// <summary>Whether two names are different names.</summary>
    /// <param name="left">One name.</param>
    /// <param name="right">The other.</param>
    public static bool operator !=(EventName left, EventName right) => !left.Equals(right);
}
