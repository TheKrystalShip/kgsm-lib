using Microsoft.Extensions.DependencyInjection;
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
    /// Adds KGSM services to the specified IServiceCollection.
    /// </summary>
    /// <param name="services">The IServiceCollection to add services to.</param>
    /// <param name="kgsmPath">The path to the KGSM executable.</param>
    /// <param name="socketPath">The path to the KGSM Unix socket.</param>
    /// <returns>
    /// The IServiceCollection so that additional calls can be chained.
    /// </returns>
    /// <exception cref="ArgumentNullException">Thrown when services, kgsmPath, or socketPath are null.</exception>
    public static IServiceCollection AddKgsmServices(this IServiceCollection services, string kgsmPath, string socketPath)
    {
        ArgumentNullException.ThrowIfNull(services, nameof(services));

        if (string.IsNullOrWhiteSpace(kgsmPath))
            throw new ArgumentNullException(nameof(kgsmPath), "KGSM path cannot be null, empty, or whitespace.");

        if (string.IsNullOrWhiteSpace(socketPath))
            throw new ArgumentNullException(nameof(socketPath), "Socket path cannot be null, empty, or whitespace.");

        // Some services require the kgsmPath and socketPath, so we register them as options
        services.AddSingleton(new KgsmOptions { KgsmPath = kgsmPath, SocketPath = socketPath });

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

        // Singleton services
        services.AddSingleton<IUnixSocketClient, UnixSocketClient>();
        services.AddSingleton<IEventService, EventService>();
        services.AddSingleton<IKgsmClient, KgsmClient>();

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

        return AddKgsmServices(services, options.KgsmPath, options.SocketPath);
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

        return AddKgsmServices(services, options.KgsmPath, options.SocketPath);
    }

    /// <summary>
    /// Adds the kgsm-watchdog control client (<see cref="IWatchdogClient"/>) to the
    /// service collection. Independent of <see cref="AddKgsmServices(IServiceCollection, string, string)"/> —
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

        // Singleton: the client owns a pooled HttpClient/handler, like IUnixSocketClient.
        services.AddSingleton(options);
        services.AddSingleton<IWatchdogClient, WatchdogClient>();

        return services;
    }

    /// <summary>
    /// Adds the kgsm-firewall control client (<see cref="IFirewallService"/>) to the service collection.
    /// Independent of <see cref="AddKgsmServices(IServiceCollection, string, string)"/> and
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
}
