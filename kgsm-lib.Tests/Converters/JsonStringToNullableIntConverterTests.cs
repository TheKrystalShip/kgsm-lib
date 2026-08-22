using System.Text.Json;
using System.Text.Json.Serialization;

namespace TheKrystalShip.KGSM.Tests.Converters;

/// <summary>
/// The nullable variant of KGSM's stringly-typed integer coercion. Everything here turns on one
/// distinction: a value that cannot be read becomes <c>null</c>, never <c>0</c>.
/// </summary>
public class JsonStringToNullableIntConverterTests
{
    private readonly JsonSerializerOptions _options = new();

    public JsonStringToNullableIntConverterTests()
        => _options.Converters.Add(new JsonStringToNullableIntConverter());

    [Theory]
    [InlineData("0", 0)]
    [InlineData("42", 42)]
    [InlineData("-1", -1)]
    [InlineData("2147483647", 2147483647)]
    public void A_readable_number_survives_as_itself(string raw, int expected)
    {
        var model = JsonSerializer.Deserialize<Model>($"{{\"value\": \"{raw}\"}}", _options);
        Assert.Equal(expected, model!.Value);
    }

    [Fact]
    public void A_json_number_is_read_without_going_through_a_string()
    {
        Assert.Equal(2908, JsonSerializer.Deserialize<Model>("{\"value\": 2908}", _options)!.Value);
    }

    [Theory]
    [InlineData("\"\"")]
    [InlineData("\"   \"")]
    [InlineData("null")]
    public void An_absent_value_is_null_rather_than_zero(string raw)
    {
        // The whole reason this converter exists. A config key that has never been written would
        // otherwise report the instance as measured to need nothing.
        Assert.Null(JsonSerializer.Deserialize<Model>($"{{\"value\": {raw}}}", _options)!.Value);
    }

    [Theory]
    [InlineData("\"not a number\"")]
    [InlineData("\"12.5\"")]
    [InlineData("\"99999999999999\"")]  // beyond int
    [InlineData("{\"nested\": 1}")]
    [InlineData("[1, 2]")]
    public void An_unreadable_value_is_null_and_does_not_throw(string raw)
    {
        // A garbled key loses itself, never the read of every other key beside it.
        Assert.Null(JsonSerializer.Deserialize<Model>($"{{\"value\": {raw}}}", _options)!.Value);
    }

    [Fact]
    public void A_null_writes_as_null_not_as_zero()
    {
        string json = JsonSerializer.Serialize(new Model { Value = null }, _options);
        Assert.Contains("null", json);
        Assert.DoesNotContain("0", json);
    }

    [Fact]
    public void A_value_writes_as_a_number()
    {
        Assert.Contains("2908", JsonSerializer.Serialize(new Model { Value = 2908 }, _options));
    }

    private sealed class Model
    {
        [JsonPropertyName("value")]
        public int? Value { get; set; }
    }
}
