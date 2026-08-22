using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM;

/// <summary>
/// Converts KGSM's stringly-typed integers to <see cref="Nullable{Int32}"/>, mapping a value that is
/// absent, empty or unparseable to <see langword="null"/>.
/// </summary>
/// <remarks>
/// <para>
/// <b>The difference from <see cref="JsonStringToIntConverter"/> is the whole reason this exists.</b>
/// That one answers an unreadable value with <c>0</c>, which is right for a key whose zero means
/// something — <c>memory_cap_mb="0"</c> is KGSM's spelling of "uncapped". It is wrong for a key that
/// records a measurement: a config that has never had one written to it would report the instance as
/// measured to need nothing, which is a fabricated figure rather than a missing one.
/// </para>
/// <para>
/// Apply it per-property. It is deliberately not registered globally: which of the two answers is
/// honest depends on what the key means, and only the property knows that.
/// </para>
/// </remarks>
public sealed class JsonStringToNullableIntConverter : JsonConverter<int?>
{
    /// <inheritdoc/>
    public override int? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.Null:
                return null;

            case JsonTokenType.Number:
                return reader.TryGetInt32(out int number) ? number : null;

            case JsonTokenType.String:
                string? raw = reader.GetString();
                if (string.IsNullOrWhiteSpace(raw))
                    return null;
                return int.TryParse(raw, out int parsed) ? parsed : null;

            default:
                // Not a value we understand. Consume it and degrade to null rather than throwing and
                // sinking the read of every other key beside it.
                reader.Skip();
                return null;
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, int? value, JsonSerializerOptions options)
    {
        if (value is { } v)
            writer.WriteNumberValue(v);
        else
            writer.WriteNullValue();
    }
}
