namespace TheKrystalShip.KGSM.Events;

/// <summary>
/// The identity of a component that writes an event journal.
/// </summary>
/// <remarks>
/// <para>
/// A producer id is never read out of an event's own payload. It comes from <em>which journal a
/// line was read from</em>, which a reader establishes itself and can therefore check — where a
/// field inside the data is a claim it cannot. That is what stops one producer's line from
/// presenting itself as another's.
/// </para>
/// <para>
/// This class holds the format rule and the one producer the library knows by name. It is
/// deliberately <b>not</b> a registry of leaves: which journals exist on a host is discovered from
/// the installed leaf descriptors, so a list here would be a second answer able to disagree with
/// the host.
/// </para>
/// </remarks>
public static class JournalProducer
{
    /// <summary>
    /// The engine's own journal. Known by name because kgsm is the engine rather than a leaf —
    /// it is present wherever the library is useful at all.
    /// </summary>
    public const string Kgsm = "kgsm";

    /// <summary>
    /// Whether <paramref name="producer"/> is a usable producer id.
    /// </summary>
    /// <remarks>
    /// Lowercase letters, digits and dashes, and no underscore. The underscore is excluded because
    /// it separates the fields of a position id (<see cref="AuditId.ForPosition(string, string, long)"/>),
    /// and a producer containing one would make an id ambiguous to read back.
    /// </remarks>
    /// <param name="producer">The candidate id.</param>
    /// <returns>True when it can be used.</returns>
    public static bool IsValid(string? producer)
    {
        if (string.IsNullOrEmpty(producer))
            return false;

        foreach (char c in producer)
        {
            bool ok = c is >= 'a' and <= 'z' || c is >= '0' and <= '9' || c == '-';
            if (!ok)
                return false;
        }

        return true;
    }

    /// <summary>
    /// Throws unless <paramref name="producer"/> is valid.
    /// </summary>
    /// <param name="producer">The candidate id.</param>
    /// <param name="paramName">The caller's parameter name, for the exception.</param>
    /// <exception cref="ArgumentException">Thrown when the id is unusable.</exception>
    public static void Validate(string? producer, string paramName)
    {
        if (!IsValid(producer))
        {
            throw new ArgumentException(
                $"'{producer}' is not a valid journal producer id: expected lowercase letters, digits "
                + "and dashes, with no underscore.",
                paramName);
        }
    }
}
