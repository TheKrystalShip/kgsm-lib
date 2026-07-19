using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IKgsmClient interface for interacting with KGSM.
/// Provides access to the various KGSM modules:
/// - Blueprint Service
/// - Instance Service
/// - Event Service
/// - Configuration Service
/// - Lifecycle Service
/// - File Service
/// - Directory Service
/// - Watcher Service
/// - Network Service
/// - System Service
/// - Event Management Service
/// </summary>
public class KgsmClient : IKgsmClient
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<KgsmClient> _logger;

    /// <inheritdoc/>
    public IBlueprintService Blueprints { get; }

    /// <inheritdoc/>
    public IInstanceService Instances { get; }

    /// <inheritdoc/>
    public IEventService Events { get; }

    /// <inheritdoc/>
    public IConfigService Config { get; }

    /// <inheritdoc/>
    public ILifecycleService Lifecycle { get; }

    /// <inheritdoc/>
    public IFileService Files { get; }

    /// <inheritdoc/>
    public IDirectoryService Directories { get; }

    /// <inheritdoc/>
    public IWatcherService Watcher { get; }

    /// <inheritdoc/>
    public INetworkService Network { get; }

    /// <inheritdoc/>
    public ISystemService System { get; }

    /// <inheritdoc/>
    public IEventManagementService EventManagement { get; }

    /// <inheritdoc/>
    public IInstanceFiles InstanceFiles { get; }

    /// <summary>
    /// Initializes a new instance of the KgsmClient class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="blueprintService">The blueprint service to use for managing blueprints.</param>
    /// <param name="instanceService">The instance service to use for managing instances.</param>
    /// <param name="eventService">The event service to use for handling events.</param>
    /// <param name="configService">The configuration service to use for managing configuration.</param>
    /// <param name="lifecycleService">The lifecycle service to use for managing instance lifecycle operations.</param>
    /// <param name="fileService">The file service to use for managing file operations.</param>
    /// <param name="directoryService">The directory service to use for managing directory operations.</param>
    /// <param name="watcherService">The watcher service to use for monitoring instance readiness.</param>
    /// <param name="networkService">The network service to use for querying and managing network configuration.</param>
    /// <param name="systemService">The system service to use for managing system operations.</param>
    /// <param name="eventManagementService">The event management service to use for managing event transports and configuration.</param>
    /// <param name="instanceFilesService">The jailed instance-filesystem authority.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public KgsmClient(
        IKgsmCommandExecutor commandExecutor,
        IBlueprintService blueprintService,
        IInstanceService instanceService,
        IEventService eventService,
        IConfigService configService,
        ILifecycleService lifecycleService,
        IFileService fileService,
        IDirectoryService directoryService,
        IWatcherService watcherService,
        INetworkService networkService,
        ISystemService systemService,
        IEventManagementService eventManagementService,
        IInstanceFiles instanceFilesService,
        ILogger<KgsmClient> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        Blueprints = blueprintService ?? throw new ArgumentNullException(nameof(blueprintService));
        Instances = instanceService ?? throw new ArgumentNullException(nameof(instanceService));
        Events = eventService ?? throw new ArgumentNullException(nameof(eventService));
        Config = configService ?? throw new ArgumentNullException(nameof(configService));
        Lifecycle = lifecycleService ?? throw new ArgumentNullException(nameof(lifecycleService));
        Files = fileService ?? throw new ArgumentNullException(nameof(fileService));
        Directories = directoryService ?? throw new ArgumentNullException(nameof(directoryService));
        Watcher = watcherService ?? throw new ArgumentNullException(nameof(watcherService));
        Network = networkService ?? throw new ArgumentNullException(nameof(networkService));
        System = systemService ?? throw new ArgumentNullException(nameof(systemService));
        EventManagement = eventManagementService ?? throw new ArgumentNullException(nameof(eventManagementService));
        InstanceFiles = instanceFilesService ?? throw new ArgumentNullException(nameof(instanceFilesService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("KgsmClient initialized");

        // Initialize the event service
        Events.Initialize();
    }

    /// <inheritdoc/>
    public KgsmResult Help()
    {
        _logger.LogDebug("Getting help information");

        return _commandExecutor.Execute("help");
    }

    /// <inheritdoc/>
    public KgsmResult HelpInteractive()
    {
        _logger.LogDebug("Getting interactive help information");

        return _commandExecutor.Execute("interactive", "help");
    }

    /// <inheritdoc/>
    public KgsmResult GetVersion()
    {
        _logger.LogDebug("Getting KGSM version");

        return _commandExecutor.Execute("--version");
    }

    /// <inheritdoc/>
    public KgsmResult AdHoc(params string[] args)
    {
        _logger.LogDebug("Executing ad-hoc command with arguments: {Arguments}", string.Join(" ", args));

        return _commandExecutor.Execute(args);
    }
}
