using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Main interface for interacting with KGSM.
/// Provides access to blueprint and instance services, as well as general KGSM operations.
/// </summary>
public interface IKgsmClient
{
    /// <summary>
    /// Gets the blueprint service for managing blueprints.
    /// </summary>
    IBlueprintService Blueprints { get; }

    /// <summary>
    /// Gets the instance service for managing instances.
    /// </summary>
    IInstanceService Instances { get; }

    /// <summary>
    /// Gets the library service for managing the named roots instances are placed in.
    /// </summary>
    ILibraryService Libraries { get; }

    /// <summary>
    /// Gets the event service for subscribing to KGSM events.
    /// </summary>
    IEventService Events { get; }

    /// <summary>
    /// Gets the configuration service for managing KGSM configuration.
    /// </summary>
    IConfigService Config { get; }

    /// <summary>
    /// Gets the lifecycle service for managing instance lifecycle operations.
    /// </summary>
    ILifecycleService Lifecycle { get; }

    /// <summary>
    /// Gets the file service for managing file operations.
    /// </summary>
    IFileService Files { get; }

    /// <summary>
    /// Gets the directory service for managing directory operations.
    /// </summary>
    IDirectoryService Directories { get; }

    /// <summary>
    /// Gets the watcher service for monitoring instance readiness.
    /// </summary>
    IWatcherService Watcher { get; }

    /// <summary>
    /// Gets the network service for querying and managing network configuration.
    /// </summary>
    INetworkService Network { get; }

    /// <summary>
    /// Gets the system service for managing system operations.
    /// </summary>
    ISystemService System { get; }

    /// <summary>
    /// Gets the event management service for managing event transports and configuration.
    /// </summary>
    IEventManagementService EventManagement { get; }

    /// <summary>
    /// Gets the jailed instance-filesystem authority (list/read/write/delete/rename inside an
    /// instance's working directory).
    /// </summary>
    IInstanceFiles InstanceFiles { get; }

    /// <summary>
    /// Gets the blueprint write authority (create/remove native <c>&lt;name&gt;.bp.yaml</c> files in the
    /// user blueprints directory).
    /// </summary>
    IBlueprintFiles BlueprintFiles { get; }

    /// <summary>
    /// Prints the help message.
    /// </summary>
    /// <returns>Result of the help command execution.</returns>
    KgsmResult Help();

    /// <summary>
    /// Prints the help message for the interactive mode.
    /// </summary>
    /// <returns>Result of the help command execution.</returns>
    KgsmResult HelpInteractive();

    /// <summary>
    /// Gets the version information for KGSM.
    /// </summary>
    /// <returns>Result containing the version information.</returns>
    KgsmResult GetVersion();

    /// <summary>
    /// Executes an ad-hoc command with the specified arguments.
    /// </summary>
    /// <param name="args">Arguments to send to KGSM.</param>
    /// <returns>Result of the command execution.</returns>
    KgsmResult AdHoc(params string[] args);
}
