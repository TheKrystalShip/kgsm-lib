using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM;

/// <summary>
/// Reads KGSM's <c>start_time</c> field defensively into a <see cref="DateTime"/>?
/// without ever throwing. Current KGSM emits ISO-8601 UTC
/// (<c>YYYY-MM-DDTHH:MM:SSZ</c>), but old, un-regenerated instances still emit
/// non-ISO forms (the local-time C-<c>asctime</c> shape <c>Sun Jun 21 23:53:46 2026</c>,
/// the offset-less <c>2026-06-16 14:23:01</c>, an empty string, or garbage). Because
/// the whole bulk-status roster is one <see cref="JsonSerializer"/> call, a plain
/// <see cref="DateTime"/>? property would throw on a single bad value and collapse the
/// entire fleet read to nothing. This converter degrades a bad value to an honest
/// <see langword="null"/> for that one instance instead.
/// </summary>
/// <remarks>
/// <para>
/// Honesty contract (the kgsm-api <c>ServerAggregator</c> accepts a start time only
/// when its <see cref="DateTime.Kind"/> is <see cref="DateTimeKind.Utc"/>, else drops
/// it):
/// </para>
/// <list type="bullet">
/// <item>JSON <c>null</c> → <see langword="null"/>.</item>
/// <item>A string with an explicit UTC <c>Z</c> or an explicit offset → a
/// <see cref="DateTime"/> at <see cref="DateTimeKind.Utc"/> (an offset is converted to
/// UTC).</item>
/// <item>An offset-less string (the old local-time/asctime formats, empty, or garbage)
/// → <see langword="null"/>. We deliberately do <b>not</b> assume a timezone for an
/// offset-less value — guessing one would fabricate a wrong instant.</item>
/// <item>An unexpected token (number, object, …) → <see cref="Utf8JsonReader.Skip"/>
/// then <see langword="null"/>.</item>
/// </list>
/// <para>It never throws; a bad value loses only its own <c>start_time</c>.</para>
/// </remarks>
public class JsonTolerantUtcDateTimeConverter : JsonConverter<DateTime?>
{
    /// <inheritdoc/>
    public override DateTime? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.String:
                string? raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    return null;

                // RoundtripKind preserves the offset distinction we key honesty on:
                //   - a trailing 'Z'  → DateTimeKind.Utc      (already UTC)
                //   - an explicit +hh:mm offset → DateTimeKind.Local (carries an instant)
                //   - no offset at all → DateTimeKind.Unspecified (ambiguous → honest null)
                if (DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime dt))
                {
                    return dt.Kind switch
                    {
                        DateTimeKind.Utc => dt,
                        // An explicit offset pins a real instant; normalize it to UTC.
                        DateTimeKind.Local => dt.ToUniversalTime(),
                        // No offset → we cannot know the instant without guessing a TZ.
                        _ => null,
                    };
                }

                // The old asctime form ("Sun Jun 21 23:53:46 2026") and any garbage land
                // here — honest null, never a fabricated time.
                return null;

            default:
                // Number/object/array etc. — not a value we understand. Consume it and
                // degrade to null rather than throwing and sinking the whole read.
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, DateTime? value, JsonSerializerOptions options)
    {
        if (value is null)
            writer.WriteNullValue();
        else
            // Emit canonical ISO-8601 UTC (the form current KGSM uses).
            writer.WriteStringValue(value.Value.ToUniversalTime());
    }
}
