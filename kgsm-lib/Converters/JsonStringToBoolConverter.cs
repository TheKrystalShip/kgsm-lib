using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM;

/// <summary>
/// Converts a JSON string value to a boolean.
/// Used to handle cases where boolean values are represented as strings like "true" and "false".
/// </summary>
public class JsonStringToBoolConverter : JsonConverter<bool>
{
    /// <summary>
    /// Reads and converts the JSON to type bool.
    /// </summary>
    /// <param name="reader">The reader.</param>
    /// <param name="typeToConvert">The type to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    /// <returns>The converted value.</returns>
    public override bool Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                string? stringValue = reader.GetString();
                if (string.IsNullOrEmpty(stringValue))
                {
                    return false;
                }

                // Try case-insensitive comparison for "true" and "false"
                if (stringValue.Equals("true", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (stringValue.Equals("false", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                if (stringValue.Equals("active", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (stringValue.Equals("inactive", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                // For any other string value, return false
                return false;

            case JsonTokenType.True:
                return true;

            case JsonTokenType.False:
                return false;

            // Treat null as false
            case JsonTokenType.Null:
                return false;

            default:
                throw new JsonException($"Invalid token type '{reader.TokenType}' for boolean conversion.");
        }
    }

    /// <summary>
    /// Writes the boolean value as a JSON string.
    /// </summary>
    /// <param name="writer">The writer to write to.</param>
    /// <param name="value">The value to convert.</param>
    /// <param name="options">An object that specifies serialization options to use.</param>
    public override void Write(Utf8JsonWriter writer, bool value, JsonSerializerOptions options)
    {
        writer.WriteStringValue(value ? "true" : "false");
    }
}
