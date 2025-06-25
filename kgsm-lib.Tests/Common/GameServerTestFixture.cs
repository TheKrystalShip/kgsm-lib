using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Extensions;
using Xunit;

namespace TheKrystalShip.KGSM.Tests.Common;

/// <summary>
/// Base class for test fixtures that create and manage test game server instances.
/// </summary>
public abstract class GameServerTestFixture : IDisposable
{

    /// <summary>
    /// Gets the name of the test instance.
    /// </summary>
    public string InstanceName { get; }

    /// <summary>
    /// Gets the installation directory of the test instance.
    /// </summary>
    public string InstallDir { get; }

    /// <summary>
    /// Gets the blueprint name used for this test instance.
    /// </summary>
    public string BlueprintName { get; }

    /// <summary>
    /// Gets the KGSM client for interacting with KGSM.
    /// </summary>
    protected readonly IKgsmClient KgsmClient;

    /// <summary>
    /// Gets the logger factory for creating loggers.
    /// </summary>
    protected readonly ILoggerFactory LoggerFactory;

    /// <summary>
    /// Gets the logger for this test fixture.
    /// </summary>
    protected readonly ILogger Logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="GameServerTestFixture"/> class.
    /// </summary>
    /// <param name="blueprintName">The name of the blueprint to use for the test instance.</param>
    protected GameServerTestFixture(string blueprintName)
    {
        BlueprintName = blueprintName;
        InstanceName = $"{blueprintName}-test-{Guid.NewGuid().ToString()[..8]}";
        InstallDir = Path.Combine(TestConstants.TestInstallDir, InstanceName);
        Directory.CreateDirectory(InstallDir);

        // Create service provider for KGSM client
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug));
        services.AddKgsmServices(TestConstants.KgsmPath, TestConstants.KgsmSocketPath);

        var serviceProvider = services.BuildServiceProvider();

        KgsmClient = serviceProvider.GetRequiredService<IKgsmClient>();
        LoggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
        Logger = LoggerFactory.CreateLogger<GameServerTestFixture>();

        // Install the game server instance
        InstallInstance();
    }

    /// <summary>
    /// Installs the test instance.
    /// </summary>
    private void InstallInstance()
    {
        Logger.LogInformation("Installing {Blueprint} test instance '{InstanceName}' at {InstallDir}",
            BlueprintName, InstanceName, InstallDir);

        var result = KgsmClient.Instances.Install(BlueprintName, InstallDir, name: InstanceName);

        if (!result.IsSuccess)
        {
            Logger.LogError("Failed to install {Blueprint} test instance: {ErrorMessage}",
                BlueprintName, result.Stderr);
            throw new Exception($"Failed to install test {BlueprintName} instance: {result.Stderr}");
        }

        Logger.LogInformation("Successfully installed {Blueprint} test instance", BlueprintName);
    }

    /// <summary>
    /// Cleans up resources used by the test fixture.
    /// </summary>
    public virtual void Dispose()
    {
        try
        {
            Logger.LogInformation("Cleaning up {Blueprint} test instance '{InstanceName}'",
                BlueprintName, InstanceName);

            // Stop instance if running
            if (KgsmClient.Instances.IsActive(InstanceName))
            {
                Logger.LogInformation("Stopping active instance '{InstanceName}'", InstanceName);
                KgsmClient.Instances.Stop(InstanceName);
                Thread.Sleep(2000); // Give it time to fully stop
            }

            // Uninstall test instance
            Logger.LogInformation("Uninstalling instance '{InstanceName}'", InstanceName);
            var result = KgsmClient.Instances.Uninstall(InstanceName);

            if (!result.IsSuccess)
            {
                Logger.LogWarning("Failed to uninstall instance '{InstanceName}': {ErrorMessage}",
                    InstanceName, result.Stderr);
            }

            // Clean up test directory
            if (Directory.Exists(InstallDir))
            {
                Logger.LogInformation("Deleting instance directory '{InstallDir}'", InstallDir);
                Directory.Delete(InstallDir, true);
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "Error during cleanup of test instance '{InstanceName}'", InstanceName);
        }
    }
}

/// <summary>
/// Collection definition for tests that require real game server instances.
/// </summary>
[CollectionDefinition("GameServerTests")]
public class GameServerTestCollection : ICollectionFixture<FactorioTestFixture>,
                                       ICollectionFixture<NecesseTestFixture>,
                                       ICollectionFixture<TerrariaTestFixture>
{
    // This class has no code, and is never created. Its purpose is to be the place
    // to apply [CollectionDefinition] and all the ICollectionFixture<> interfaces.
}

/// <summary>
/// Test fixture for Factorio tests.
/// </summary>
public class FactorioTestFixture : GameServerTestFixture
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FactorioTestFixture"/> class.
    /// </summary>
    public FactorioTestFixture() : base("factorio")
    {
    }
}

/// <summary>
/// Test fixture for Necesse tests.
/// </summary>
public class NecesseTestFixture : GameServerTestFixture
{
    /// <summary>
    /// Initializes a new instance of the <see cref="NecesseTestFixture"/> class.
    /// </summary>
    public NecesseTestFixture() : base("necesse")
    {
    }
}

/// <summary>
/// Test fixture for Terraria tests.
/// </summary>
public class TerrariaTestFixture : GameServerTestFixture
{
    /// <summary>
    /// Initializes a new instance of the <see cref="TerrariaTestFixture"/> class.
    /// </summary>
    public TerrariaTestFixture() : base("terraria")
    {
    }
}
