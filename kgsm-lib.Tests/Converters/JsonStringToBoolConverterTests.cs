using System.Text.Json;
using System.Text.Json.Serialization;
using TheKrystalShip.KGSM;

namespace TheKrystalShip.KGSM.Tests.Converters;

/// <summary>
/// Tests for the JsonStringToBoolConverter class.
/// </summary>
public class JsonStringToBoolConverterTests
{
    private readonly JsonSerializerOptions _options;

    public JsonStringToBoolConverterTests()
    {
        _options = new JsonSerializerOptions();
        _options.Converters.Add(new JsonStringToBoolConverter());
    }

    [Theory]
    [InlineData("true", true)]
    [InlineData("True", true)]
    [InlineData("TRUE", true)]
    [InlineData("false", false)]
    [InlineData("False", false)]
    [InlineData("FALSE", false)]
    public void Read_StringBooleanValues_ReturnsCorrectBoolean(string jsonValue, bool expected)
    {
        // Arrange
        string json = $"{{\"value\": \"{jsonValue}\"}}";

        // Act
        var result = JsonSerializer.Deserialize<TestBoolModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.Equal(expected, result.Value);
    }

    [Fact]
    public void Read_EmptyString_ReturnsFalse()
    {
        // Arrange
        string json = "{\"value\": \"\"}";

        // Act
        var result = JsonSerializer.Deserialize<TestBoolModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Value);
    }

    [Fact]
    public void Read_NullString_ReturnsFalse()
    {
        // Arrange
        string json = "{\"value\": null}";

        // Act
        var result = JsonSerializer.Deserialize<TestBoolModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Value);
    }

    [Theory]
    [InlineData("1")]
    [InlineData("0")]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("invalid")]
    public void Read_UnrecognizedString_ReturnsFalse(string jsonValue)
    {
        // Arrange
        string json = $"{{\"value\": \"{jsonValue}\"}}";

        // Act
        var result = JsonSerializer.Deserialize<TestBoolModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Value);
    }

    [Fact]
    public void Read_ActualBooleanTrue_ReturnsTrue()
    {
        // Arrange
        string json = "{\"value\": true}";

        // Act
        var result = JsonSerializer.Deserialize<TestBoolModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.True(result.Value);
    }

    [Fact]
    public void Read_ActualBooleanFalse_ReturnsFalse()
    {
        // Arrange
        string json = "{\"value\": false}";

        // Act
        var result = JsonSerializer.Deserialize<TestBoolModel>(json, _options);

        // Assert
        Assert.NotNull(result);
        Assert.False(result.Value);
    }

    [Fact]
    public void Write_TrueValue_WritesStringTrue()
    {
        // Arrange
        var model = new TestBoolModel { Value = true };

        // Act
        var json = JsonSerializer.Serialize(model, _options);

        // Assert
        Assert.Contains("\"true\"", json);
    }

    [Fact]
    public void Write_FalseValue_WritesStringFalse()
    {
        // Arrange
        var model = new TestBoolModel { Value = false };

        // Act
        var json = JsonSerializer.Serialize(model, _options);

        // Assert
        Assert.Contains("\"false\"", json);
    }

    private class TestBoolModel
    {
        [JsonPropertyName("value")]
        [JsonConverter(typeof(JsonStringToBoolConverter))]
        public bool Value { get; set; }
    }
}
