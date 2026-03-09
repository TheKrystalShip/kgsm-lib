using System.Text.Json;
using System.Text.Json.Serialization;
using TheKrystalShip.KGSM;

namespace TheKrystalShip.KGSM.Tests.Converters;

/// <summary>
/// Tests for the JsonStringToIntConverter class.
/// </summary>
public class JsonStringToIntConverterTests
{
    private readonly JsonSerializerOptions _options;

    public JsonStringToIntConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new JsonStringToIntConverter());
    }

    [Theory]
    [InlineData("0", 0)]
    [InlineData("1", 1)]
    [InlineData("42", 42)]
    [InlineData("999", 999)]
    [InlineData("-1", -1)]
    [InlineData("-999", -999)]
    [InlineData("2147483647", 2147483647)] // int.MaxValue
    [InlineData("-2147483648", -2147483648)] // int.MinValue
    public void Read_ValidStringNumber_ReturnsCorrectInteger(string jsonValue, int expected)
    {
        // Arrange
        string json = $"{{\"value\": \"{jsonValue}\"}}";

        // Act
        var result = JsonSerializer.Deserialize<TestIntModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Read_EmptyString_ReturnsZero()
    {
        // Arrange
        string json = "{\"value\": \"\"}";

        // Act
        var result = JsonSerializer.Deserialize<TestIntModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Value);
    }

    [Fact]
    public void Read_WhitespaceString_ReturnsZero()
    {
        // Arrange
        string json = "{\"value\": \"   \"}";

        // Act
        var result = JsonSerializer.Deserialize<TestIntModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("12.34")]
    [InlineData("not a number")]
    [InlineData("1e10")]
    public void Read_InvalidString_ReturnsZero(string jsonValue)
    {
        // Arrange
        string json = $"{{\"value\": \"{jsonValue}\"}}";

        // Act
        var result = JsonSerializer.Deserialize<TestIntModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(0, result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-42)]
    [InlineData(2147483647)]
    [InlineData(-2147483648)]
    public void Read_ActualNumber_ReturnsCorrectInteger(int expected)
    {
        // Arrange
        string json = $@"{{""value"": {expected}}}";

        // Act
        var result = JsonSerializer.Deserialize<TestIntModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(42)]
    [InlineData(-42)]
    [InlineData(2147483647)]
    [InlineData(-2147483648)]
    public void Write_IntValue_WritesNumber(int value)
    {
        // Arrange
        var model = new TestIntModel { Value = value };

        // Act
        var json = JsonSerializer.Serialize(model, _options);

        // Assert
        Assert.Contains(value.ToString(), json);
    }

    [Fact]
    public void Read_ArrayToken_ThrowsJsonException()
    {
        // Arrange
        string json = "{\"value\": [1, 2, 3]}";

        // Act & Assert
        Assert.Throws<JsonException>(() => 
            JsonSerializer.Deserialize<TestIntModel>(json, _options));
    }

    private class TestIntModel
    {
        [JsonPropertyName("value")]
        [JsonConverter(typeof(JsonStringToIntConverter))]
        public int Value { get; set; }
    }
}
