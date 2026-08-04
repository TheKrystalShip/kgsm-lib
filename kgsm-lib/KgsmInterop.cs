using Microsoft.Extensions.DependencyInjection;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Extensions;

namespace TheKrystalShip.KGSM;

/// <summary>
/// KgsmInterop is a class that provides an interface to interact with the KGSM (Krystal Game Server Manager).
/// It allows you to perform various operations such as creating blueprints, managing instances,
/// checking updates, and handling events read from the engine's event journal.
/// 
/// This class is kept for backward compatibility with the previous version of the library.
/// New code should use the IKgsmClient interface and its implementations.
/// </summary>
[Obsolete("This class is kept for backward compatibility. New code should use IKgsmClient interface.")]
public class KgsmInterop
{
    /// <summary>
    /// The underlying KGSM client instance.
    /// </summary>
    private readonly IKgsmClient _client;

    /// <summary>
    /// Gets the event service for handling KGSM events.
    /// </summary>
    public IEventService Events => _client.Events;

    /// <summary>
    /// Initializes a new instance of the KgsmInterop class with the specified KGSM path.
    /// Throws an ArgumentNullException if the kgsmPath is null or empty.
    /// </summary>
    /// <param name="kgsmPath">The path to the KGSM executable.</param>
    /// <exception cref="ArgumentNullException">Thrown when kgsmPath is null or empty.</exception>
    public KgsmInterop(string kgsmPath)
    {
        if (string.IsNullOrWhiteSpace(kgsmPath))
            throw new ArgumentNullException(nameof(kgsmPath), "KGSM path cannot be null, empty, or whitespace.");

        IServiceCollection services = new ServiceCollection();
        services.AddKgsmServices(kgsmPath);

        var serviceProvider = services.BuildServiceProvider();

        _client = serviceProvider.GetRequiredService<IKgsmClient>();
    }

    /// <summary>
    /// Initializes a new instance of the KgsmInterop class with the specified KGSM options.
    /// Throws an ArgumentNullException if the options are null.
    /// </summary>
    /// <param name="options">The KGSM options.</param>
    /// <exception cref="ArgumentNullException">Thrown when options are null.</exception>
    public KgsmInterop(KgsmOptions options) : this(options.KgsmPath)
    {
        ArgumentNullException.ThrowIfNull(options, nameof(options));
    }

    /// <summary>
    /// Prints the help message
    /// </summary>
    public KgsmResult Help()
        => _client.Help();

    /// <summary>
    /// Prints the help message for the interactive mode
    /// </summary>
    public KgsmResult HelpInteractive()
        => _client.HelpInteractive();

    /// <summary>
    /// Print the version information for KGSM
    /// </summary>
    public KgsmResult GetVersion()
        => _client.GetVersion();

    /// <summary>
    /// Prints a list of all available blueprints
    /// </summary>
    public Dictionary<string, Blueprint> GetBlueprints()
        => _client.Blueprints.ListDetailed();

    /// <summary>
    /// Create an instance of a blueprint
    /// </summary>
    /// <param name="blueprintName">Name of the blueprint to install</param>
    /// <param name="installDir">Optional installation directory</param>
    /// <param name="version">Optional version to install</param>
    /// <param name="name">Optional identifier used when creating the instance</param>
    public KgsmResult Install(string blueprintName, string? installDir = null, string? version = null, string? name = null)
        => _client.Instances.Install(blueprintName, installDir, version, name);

    /// <summary>
    /// Uninstall an instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    /// <returns>KgsmResult</returns>
    public KgsmResult Uninstall(string instance)
        => _client.Instances.Uninstall(instance);

    /// <summary>
    /// Prints a list of all instances
    /// </summary>
    /// <returns>A dictionary of instance names to their details</returns>
    public Dictionary<string, Instance> GetInstances()
        => _client.Instances.GetAll();

    /// <summary>
    /// Print the last 10 lines for the instance log
    /// </summary>
    /// <param name="instance">Instance name</param>
    /// <param name="lines">Number of lines to retrieve</param>
    /// <returns>A collection of log lines</returns>
    public ICollection<string> GetLogs(string instance, int lines = 10)
        => _client.Instances.GetLogs(instance, lines);

    /// <summary>
    /// Print the last specified number of lines for the instance log
    /// </summary>
    /// <param name="instance">Instance name</param>
    /// <param name="lines">Number of lines to retrieve</param>
    /// <param name="cancellationToken">Cancellation token for the async operation.</param>
    /// <returns>A collection of log lines</returns>
    public async Task<ICollection<string>> GetLogsAsync(string instance, int lines = 10, CancellationToken cancellationToken = default)
        => await _client.Instances.GetLogsAsync(instance, lines, cancellationToken).ConfigureAwait(false);

    /// <summary>
    /// Print a detailed message about the current status of the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult Status(string instance)
        => _client.Instances.GetStatus(instance);

    /// <summary>
    /// Print a detailed message with information about the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult Info(string instance)
        => _client.Instances.GetInfo(instance);

    /// <summary>
    /// Print if the instance is currently active/running
    /// </summary>
    /// <param name="instance">Instance name</param>
    public bool IsActive(string instance)
        => _client.Instances.IsActive(instance);

    /// <summary>
    /// Start the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult Start(string instance)
        => _client.Instances.Start(instance);

    /// <summary>
    /// Stop the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult Stop(string instance)
        => _client.Instances.Stop(instance);

    /// <summary>
    /// Restart the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult Restart(string instance)
        => _client.Instances.Restart(instance);

    /// <summary>
    /// Print the installed version of the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult GetInstalledVersion(string instance)
        => _client.Instances.GetInstalledVersion(instance);

    /// <summary>
    /// Print the latest available version of the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult GetLatestVersion(string instance)
        => _client.Instances.GetLatestVersion(instance);

    /// <summary>
    /// Check if there's an update available for the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult CheckUpdate(string instance)
        => _client.Instances.CheckUpdate(instance);

    /// <summary>
    /// Run the update process for the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult Update(string instance)
        => _client.Instances.Update(instance);

    /// <summary>
    /// Print a list of the created instance backups
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult GetBackups(string instance)
        => _client.Instances.GetBackups(instance);

    /// <summary>
    /// Create a new backup for the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    public KgsmResult CreateBackup(string instance)
        => _client.Instances.CreateBackup(instance);

    /// <summary>
    /// Restore a specific backup for the instance
    /// </summary>
    /// <param name="instance">Instance name</param>
    /// <param name="backupName">
    /// Name of the backup to restore.
    /// Call GetBackups in order to get a list of available options
    /// </param>
    public KgsmResult RestoreBackup(string instance, string backupName)
        => _client.Instances.RestoreBackup(instance, backupName);

    /// <summary>
    /// Execute Ad-Hoc commands
    /// Useful if the command you're trying to execute hasn't been mapped
    /// by KgsmInterop.
    /// </summary>
    /// <param name="args">Arguments to send to KGSM</param>
    /// <returns>KgsmResult</returns>
    public KgsmResult AdHoc(params string[] args)
        => _client.AdHoc(args);
}