using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM;

/// <summary>
/// Reads KGSM's <c>recent_logs</c> field, which is polymorphic on the wire:
/// the management script emits a single newline-joined <em>string</em> when a
/// log file exists (<c>tail | jq -R -s .</c>) but an empty <em>array</em>
/// (<c>[]</c>) when there is no log, both passed through <c>jq --argjson</c>.
/// A plain <c>string</c> property would throw on the array form and, because the
/// whole status payload is one <c>Deserialize</c> call, collapse the entire
/// bulk-read dictionary to empty. This converter normalizes both shapes to the
/// faithful string representation (empty for <c>[]</c>) without inventing
/// structure: a string passes through, an array is newline-joined, null/empty
/// become an empty string.
/// </summary>
public class JsonRecentLogsConverter : JsonConverter<string>
{
    /// <inheritdoc/>
    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        switch (reader.TokenType)
        {
            case JsonTokenType.String:
                return reader.GetString() ?? string.Empty;

            case JsonTokenType.Null:
                return string.Empty;

            case JsonTokenType.StartArray:
                // In practice KGSM only ever emits the empty array []; join any
                // string elements defensively in case that changes.
                var lines = new List<string>();
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    if (reader.TokenType == JsonTokenType.String)
                    {
                        lines.Add(reader.GetString() ?? string.Empty);
                    }
                }
                return string.Join("\n", lines);

            default:
                throw new JsonException(
                    $"Unexpected token '{reader.TokenType}' for recent_logs; expected string, array, or null.");
        }
    }

    /// <inheritdoc/>
    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
        => writer.WriteStringValue(value);
}
