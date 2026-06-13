using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM;

/// <summary>
/// Maps a single element of KGSM's bulk-status wire
/// (<c>instances list --status --json</c>) into a <see cref="Reading{T}"/> of
/// <see cref="InstanceRuntimeStatus"/>.
///
/// KGSM emits each map value as one of two shapes: a status object, or — for an
/// instance whose management file cannot answer <c>--status</c> — an error object
/// <c>{"error":…, "instance":…, "requires_regeneration":true}</c>, so one bad
/// instance never sinks the fleet read. This converter turns the first into a
/// <see cref="ReadingState.Measured"/> reading and the second into a
/// <see cref="ReadingState.Unavailable"/> reading carrying the cause — so
/// "measured vs could-not-read" is a typed distinction rather than a nullable
/// field a consumer can mistake for data.
/// </summary>
/// <remarks>
/// Asymmetric by design: <see cref="Read"/> consumes KGSM's polymorphic wire
/// shape; <see cref="Write"/> emits the canonical <see cref="Reading{T}"/>
/// envelope. The library never serializes this type, but a faithful,
/// self-describing shape is the safe default if a consumer ever does.
/// Both paths deserialize/serialize the inner status through the caller's
/// <see cref="JsonSerializerOptions"/> so KGSM's stringly-typed scalars (the
/// global string→bool/int converters) still apply and the path stays AOT-safe
/// via source-generated type info.
/// </remarks>
public sealed class KgsmBulkStatusReadingConverter : JsonConverter<Reading<InstanceRuntimeStatus>>
{
    /// <inheritdoc/>
    public override Reading<InstanceRuntimeStatus> Read(
        ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using JsonDocument doc = JsonDocument.ParseValue(ref reader);
        JsonElement root = doc.RootElement;

        // Error shape: an instance whose management file can't answer --status.
        if (root.ValueKind == JsonValueKind.Object &&
            root.TryGetProperty("error", out JsonElement errorElement))
        {
            string? reason = errorElement.ValueKind == JsonValueKind.String
                ? errorElement.GetString()
                : errorElement.GetRawText();

            bool requiresRegeneration =
                root.TryGetProperty("requires_regeneration", out JsonElement rr) &&
                (rr.ValueKind == JsonValueKind.True ||
                 (rr.ValueKind == JsonValueKind.String &&
                  bool.TryParse(rr.GetString(), out bool parsed) && parsed));

            return Reading<InstanceRuntimeStatus>.Unavailable(
                reason,
                requiresRegeneration ? ReadingCode.RequiresRegeneration : ReadingCode.SourceError);
        }

        // Success shape: a real status object.
        var typeInfo = (JsonTypeInfo<InstanceRuntimeStatus>)options.GetTypeInfo(typeof(InstanceRuntimeStatus));
        InstanceRuntimeStatus value = root.Deserialize(typeInfo)!;
        return Reading<InstanceRuntimeStatus>.Measured(value);
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer, Reading<InstanceRuntimeStatus> value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("state", value.State.ToString());

        if (value.Value is not null)
        {
            writer.WritePropertyName("value");
            var typeInfo = (JsonTypeInfo<InstanceRuntimeStatus>)options.GetTypeInfo(typeof(InstanceRuntimeStatus));
            JsonSerializer.Serialize(writer, value.Value, typeInfo);
        }

        if (value.Reason is not null)
            writer.WriteString("reason", value.Reason);

        if (value.Code is not null)
            writer.WriteString("code", value.Code.ToString());

        writer.WriteEndObject();
    }
}
