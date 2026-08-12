using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Services;

namespace TheKrystalShip.KGSM.Extensions;

/// <summary>
/// Extension methods for configuring KGSM services in an IServiceCollection.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds KGSM services to the specified IServiceCollection, reading events from the engine's
    /// event journal at its default location.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="kgsmPath">The path to the KGSM executable.</param>
    /// <returns>
    /// The IServiceCollection so that additional calls can be chained.
    /// </returns>
    /// <remarks>
    /// No path to configure and nothing to reserve: the journal is a well-known host location
    /// that every consumer reads concurrently. This overload keeps no cursor, so it starts at
    /// the tail of the journal on every run — set <see cref="KgsmOptions.EventCursorPath"/>
    /// (or register an <see cref="IEventCursorStore"/>) through the options overload to have a
    /// consumer resume where it left off.
    /// </remarks>
    /// <exception cref="ArgumentNullException">Thrown when services or kgsmPath are null.</exception>
    public static IServiceCollection AddKgsmServices(this IServiceCollection services, string kgsmPath)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        return AddKgsmServices(services, new KgsmOptions { KgsmPath = kgsmPath });
    }

    /// <summary>
    /// Registers every KGSM service from a fully-built options object.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="options">The KGSM options.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services, options, or the KGSM path are null.</exception>
    private static IServiceCollection AddKgsmCore(IServiceCollection services, KgsmOptions options)
    {
        string kgsmPath = options.KgsmPath;

        if (string.IsNullOrWhiteSpace(kgsmPath))
            throw new ArgumentNullException(nameof(options), "KGSM path cannot be null, empty, or whitespace.");

        // Some services require the kgsmPath and the transport settings, so we register the
        // whole options object.
        services.AddSingleton(options);

        // Transient services
        services.AddTransient<IProcessRunner, ProcessRunner>();
        services.AddTransient<IKgsmCommandExecutor, KgsmCommandExecutor>();
        services.AddTransient<ILogSubscriptionService, LogSubscriptionService>();
        services.AddTransient<IBlueprintService, BlueprintService>();
        services.AddTransient<ILifecycleService, LifecycleService>();
        services.AddTransient<IInstanceService, InstanceService>();
        services.AddTransient<IConfigService, ConfigService>();
        services.AddTransient<IFileService, FileService>();
        services.AddTransient<IDirectoryService, DirectoryService>();
        services.AddTransient<IWatcherService, WatcherService>();
        services.AddTransient<INetworkService, NetworkService>();
        services.AddTransient<ISystemService, SystemService>();
        services.AddTransient<IEventManagementService, EventManagementService>();
        services.AddTransient<IInstanceFiles, InstanceFiles>();
        services.AddTransient<IInstanceBackups, InstanceBackups>();
        services.AddTransient<IBlueprintFiles, BlueprintFiles>();
        services.AddTransient<IRconClient, RconClient>();

        // Singleton services
        services.AddSingleton<IEventService, EventService>();
        services.AddSingleton<IKgsmClient, KgsmClient>();

        // The event source. EventService resolves IEventSource and never learns what backs it,
        // which is what let the transport change without touching a single consumer's handler.
        // A consumer that wants its own cursor storage — one that already owns a database
        // should — registers its own IEventCursorStore after this call, which wins by
        // last-registration.
        services.AddSingleton<IEventCursorStore>(sp => string.IsNullOrWhiteSpace(options.EventCursorPath)
            ? new NullEventCursorStore()
            : new FileEventCursorStore(options, sp.GetRequiredService<ILogger<FileEventCursorStore>>()));

        services.AddSingleton<IEventJournalReader, EventJournalReader>();
        services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<IEventJournalReader>());

        // Reading back over the journal, as opposed to tailing it. It shares nothing with the
        // reader above but the directory: it holds no position, starts nothing, and each query
        // stands alone — so a consumer can take history without taking a live subscription.
        services.AddSingleton<IEventJournalHistory, EventJournalHistory>();

        return services;
    }

    /// <summary>
    /// Adds KGSM services with the specified configuration action.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configureOptions">Action to configure the KGSM options.</param>
    /// <returns>
    /// The IServiceCollection so that additional calls can be chained.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configureOptions are null.</exception>
    public static IServiceCollection AddKgsmServices(this IServiceCollection services, Action<KgsmOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configureOptions, nameof(configureOptions));

        var options = new KgsmOptions();
        configureOptions(options);

        return AddKgsmServices(services, options);
    }

    /// <summary>
    /// Adds KGSM services with the specified KGSM options.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="options">The KGSM options.</param>
    /// <returns>
    /// The IServiceCollection so that additional calls can be chained.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when services or options are null.</exception>
    public static IServiceCollection AddKgsmServices(this IServiceCollection services, KgsmOptions options)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(options, nameof(options));

        return AddKgsmCore(services, options);
    }

    /// <summary>
    /// Adds the kgsm-watchdog control client (<see cref="IWatchdogClient"/>) to the
    /// service collection. Independent of <see cref="AddKgsmServices(IServiceCollection, string)"/> —
    /// a surface can take the watchdog client alone, the full KGSM services, or both.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="socketPath">Path to the watchdog control unix socket.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
    /// <exception cref="ArgumentException">Thrown when socketPath is null, empty, or whitespace.</exception>
    public static IServiceCollection AddKgsmWatchdogClient(this IServiceCollection services, string socketPath)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(socketPath))
            throw new ArgumentException("Watchdog socket path cannot be null, empty, or whitespace.", nameof(socketPath));

        return AddKgsmWatchdogClient(services, options => options.SocketPath = socketPath);
    }

    /// <summary>
    /// Adds the kgsm-watchdog control client with a configuration action.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configureOptions">Action to configure the watchdog client options.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configureOptions is null.</exception>
    public static IServiceCollection AddKgsmWatchdogClient(this IServiceCollection services, Action<WatchdogClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configureOptions, nameof(configureOptions));

        var options = new WatchdogClientOptions();
        configureOptions(options);

        // Singleton: the client owns a pooled HttpClient/handler.
        services.AddSingleton(options);
        services.AddSingleton<IWatchdogClient, WatchdogClient>();

        return services;
    }

    /// <summary>
    /// Adds the kgsm-firewall control client (<see cref="IFirewallService"/>) to the service collection.
    /// Independent of <see cref="AddKgsmServices(IServiceCollection, string)"/> and
    /// <see cref="AddKgsmWatchdogClient(IServiceCollection, string)"/> — a surface can take any combination.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="socketPath">Path to the kgsm-firewall control unix socket.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
    /// <exception cref="ArgumentException">Thrown when socketPath is null, empty, or whitespace.</exception>
    public static IServiceCollection AddKgsmFirewallClient(this IServiceCollection services, string socketPath)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(socketPath))
            throw new ArgumentException("Firewall socket path cannot be null, empty, or whitespace.", nameof(socketPath));

        return AddKgsmFirewallClient(services, options => options.SocketPath = socketPath);
    }

    /// <summary>
    /// Adds the kgsm-firewall control client with a configuration action.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="configureOptions">Action to configure the firewall client options.</param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services or configureOptions is null.</exception>
    public static IServiceCollection AddKgsmFirewallClient(this IServiceCollection services, Action<FirewallClientOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));
        ArgumentNullException.ThrowIfNull(configureOptions, nameof(configureOptions));

        var options = new FirewallClientOptions();
        configureOptions(options);

        // Singleton like the watchdog client: cheap, stateless (a socket is opened per request), and a
        // single registration the surfaces resolve.
        services.AddSingleton(options);
        services.AddSingleton<IFirewallService, FirewallService>();

        return services;
    }

    /// <summary>
    /// Reads every producer's event journal instead of only the engine's.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Call <b>after</b> <see cref="AddKgsmServices(IServiceCollection, KgsmOptions)"/>: this replaces
    /// that call's <see cref="IEventJournalHistory"/> and <see cref="IEventSource"/> with the
    /// federated pair, by last-registration. Every handler a consumer has registered keeps working
    /// unchanged — <c>EventService</c> resolves <see cref="IEventSource"/> and never learns what backs
    /// it, which is the whole point of that indirection.
    /// </para>
    /// <para>
    /// Which journals exist is discovered by finding them on disk, so this needs no list of leaves and a
    /// leaf that starts writing one later costs no rebuild. A consumer whose host is laid out
    /// unconventionally registers its own <see cref="IJournalDiscovery"/> after this call.
    /// </para>
    /// <para>
    /// ⚠ The federated source keeps <b>one cursor per producer</b>, which is a different store from the
    /// single-journal <see cref="IEventCursorStore"/> and cannot be migrated from it: a position in the
    /// engine's journal says nothing about a position in anyone else's. A consumer switching over
    /// therefore starts each journal from <paramref name="startPosition"/>, and one that indexes events
    /// wants <see cref="EventStartPosition.CursorOrOldest"/> so it can rebuild.
    /// </para>
    /// </remarks>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="cursorPath">
    /// Where the per-producer cursor map is kept. Null or blank keeps **no** positions, so every
    /// journal starts from <paramref name="startPosition"/> on every run — what a consumer that
    /// announces events onward rather than deriving durable state from them actually wants, since
    /// resuming would re-announce a backlog.
    /// </param>
    /// <param name="startPosition">Where a journal with no stored position begins.</param>
    /// <param name="engineJournalDirectory">
    /// Where kgsm's own journal lives. Null uses <see cref="KgsmOptions.DefaultEventJournalDirectory"/>.
    /// </param>
    /// <param name="stateRoot">
    /// Where each producer's state directory lives, each holding its journal in an <c>events</c>
    /// subdirectory. Null uses <see cref="JournalDiscovery.DefaultStateRoot"/>.
    /// </param>
    /// <param name="scanBudgetBytes">
    /// The scan budget allowed for each journal in a history query. Non-positive uses
    /// <see cref="KgsmOptions.DefaultEventHistoryScanBudgetBytes"/>. ⚠ It applies <em>per journal</em>,
    /// so each answers to the same depth whatever the fleet size, at the cost of total work scaling
    /// with the number of producers.
    /// </param>
    /// <returns>The IServiceCollection so that additional calls can be chained.</returns>
    /// <exception cref="ArgumentNullException">Thrown when services is null.</exception>
    /// <exception cref="ArgumentException">Thrown when cursorPath is null, empty, or whitespace.</exception>
    public static IServiceCollection AddKgsmJournalFederation(
        this IServiceCollection services,
        string? cursorPath = null,
        EventStartPosition startPosition = EventStartPosition.CursorOrOldest,
        string? engineJournalDirectory = null,
        string? stateRoot = null,
        long scanBudgetBytes = 0)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        long budget = scanBudgetBytes > 0 ? scanBudgetBytes : KgsmOptions.DefaultEventHistoryScanBudgetBytes;

        services.AddSingleton<IJournalDiscovery>(sp => new JournalDiscovery(
            engineJournalDirectory ?? KgsmOptions.DefaultEventJournalDirectory,
            stateRoot,
            sp.GetRequiredService<ILogger<JournalDiscovery>>()));

        services.AddSingleton<IFederatedEventCursorStore>(sp => string.IsNullOrWhiteSpace(cursorPath)
            ? new NullFederatedEventCursorStore()
            : new FileFederatedEventCursorStore(
                cursorPath, sp.GetRequiredService<ILogger<FileFederatedEventCursorStore>>()));

        services.AddSingleton<IEventJournalHistory>(sp => new FederatedEventJournalHistory(
            sp.GetRequiredService<IJournalDiscovery>().Discover(),
            budget,
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<ILogger<FederatedEventJournalHistory>>()));

        services.AddSingleton<FederatedEventSource>(sp => new FederatedEventSource(
            sp.GetRequiredService<IJournalDiscovery>().Discover(),
            sp.GetRequiredService<IFederatedEventCursorStore>(),
            startPosition,
            sp.GetRequiredService<ILoggerFactory>(),
            sp.GetRequiredService<ILogger<FederatedEventSource>>()));

        services.AddSingleton<IEventSource>(sp => sp.GetRequiredService<FederatedEventSource>());

        return services;
    }
}
