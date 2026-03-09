using System.Text.Json;

namespace TheKrystalShip.KGSM.Tests.Models;

/// <summary>
/// Tests for the Blueprint model class.
/// </summary>
public class BlueprintTests
{
    [Fact]
    public void Blueprint_DefaultConstructor_CreatesInstanceWithEmptyProperties()
    {
        // Act
        var blueprint = new Blueprint();

        // Assert
        Assert.NotNull(blueprint);
        Assert.Empty(blueprint.Name);
        Assert.Empty(blueprint.Ports);
        Assert.Empty(blueprint.SteamAppId);
        Assert.False(blueprint.IsSteamAccountRequired);
        Assert.Empty(blueprint.ExecutableFile);
        Assert.Empty(blueprint.ExecutableSubdirectory);
        Assert.Empty(blueprint.ExecutableArguments);
        Assert.Empty(blueprint.LevelName);
        Assert.Null(blueprint.StopCommand);
        Assert.Null(blueprint.SaveCommand);
    }

    [Fact]
    public void Blueprint_PropertyInitialization_SetsPropertiesCorrectly()
    {
        // Act
        var blueprint = new Blueprint
        {
            Name = "valheim",
            Ports = "2456-2458",
            SteamAppId = "896660",
            IsSteamAccountRequired = true,
            ExecutableFile = "valheim_server.x86_64",
            ExecutableSubdirectory = "bin",
            ExecutableArguments = "-nographics",
            LevelName = "Dedicated",
            StopCommand = "quit",
            SaveCommand = "save"
        };

        // Assert
        Assert.Equal("valheim", blueprint.Name);
        Assert.Equal("2456-2458", blueprint.Ports);
        Assert.Equal("896660", blueprint.SteamAppId);
        Assert.True(blueprint.IsSteamAccountRequired);
        Assert.Equal("valheim_server.x86_64", blueprint.ExecutableFile);
        Assert.Equal("bin", blueprint.ExecutableSubdirectory);
        Assert.Equal("-nographics", blueprint.ExecutableArguments);
        Assert.Equal("Dedicated", blueprint.LevelName);
        Assert.Equal("quit", blueprint.StopCommand);
        Assert.Equal("save", blueprint.SaveCommand);
    }

    [Fact]
    public void Blueprint_ToString_ReturnsFormattedString()
    {
        // Arrange
        var blueprint = new Blueprint
        {
            Name = "test-server",
            Ports = "8080",
            SteamAppId = "12345"
        };

        // Act
        var result = blueprint.ToString();

        // Assert
        Assert.Contains("test-server", result);
        Assert.Contains("8080", result);
        Assert.Contains("12345", result);
    }

    [Fact]
    public void Blueprint_Serialization_SerializesCorrectly()
    {
        // Arrange
        var blueprint = new Blueprint
        {
            Name = "valheim",
            Ports = "2456-2458",
            SteamAppId = "896660",
            IsSteamAccountRequired = false
        };

        // Act
        var json = JsonSerializer.Serialize(blueprint);

        // Assert
        Assert.Contains("valheim", json);
        Assert.Contains("2456-2458", json);
        Assert.Contains("896660", json);
    }

    [Fact]
    public void Blueprint_Deserialization_DeserializesCorrectly()
    {
        // Arrange
        var json = @"{
            ""Name"": ""valheim"",
            ""Ports"": ""2456-2458"",
            ""SteamAppId"": ""896660"",
            ""IsSteamAccountRequired"": false,
            ""ExecutableFile"": ""valheim_server.x86_64"",
            ""ExecutableSubdirectory"": """",
            ""ExecutableArguments"": ""-nographics"",
            ""LevelName"": ""Dedicated"",
            ""StopCommand"": null,
            ""SaveCommand"": null
        }";

        // Act
        var blueprint = JsonSerializer.Deserialize<Blueprint>(json);

        // Assert
        Assert.NotNull(blueprint);
        Assert.Equal("valheim", blueprint.Name);
        Assert.Equal("2456-2458", blueprint.Ports);
        Assert.Equal("896660", blueprint.SteamAppId);
        Assert.False(blueprint.IsSteamAccountRequired);
    }
}
