using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM;

/// <summary>
/// Converts a JSON string value to an integer.
/// Used to handle cases where integer values are represented as strings.
/// </summary>
public class JsonStringToIntConverter : JsonConverter<int>
{
    /// <summary>
    /// Reads and converts the JSON to type int.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Number)
        {
            return reader.GetInt32();
        }

        if (reader.TokenType == JsonTokenType.String)
        {
            string? stringValue = reader.GetString();
            if (string.IsNullOrWhiteSpace(stringValue))
            {
                return 0;
            }

            if (int.TryParse(stringValue, out int result))
            {
                return result;
            }

            return 0;
        }

        return 0;
    }

    /// <summary>
    /// Writes a specified value as JSON.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert to JSON.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
    {
        writer.WriteNumberValue(value);
    }
}
