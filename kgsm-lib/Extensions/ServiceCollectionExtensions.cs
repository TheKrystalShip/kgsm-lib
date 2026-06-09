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
}
