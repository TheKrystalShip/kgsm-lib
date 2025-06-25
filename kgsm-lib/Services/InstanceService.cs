using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Utilities;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IInstanceService interface for managing instances in KGSM.
/// </summary>
public class InstanceService : IInstanceService
{
    private readonly IProcessRunner _processRunner;
    private readonly string _kgsmPath;
    private readonly ILogger<InstanceService> _logger;

    /// <summary>
    /// Initializes a new instance of the InstanceService class.
    /// </summary>
    /// <param name="processRunner">The process runner to use for executing KGSM commands.</param>
    /// <param name="kgsmPath">The path to the KGSM executable.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public InstanceService(IProcessRunner processRunner, string kgsmPath, ILogger<InstanceService> logger)
    {
        _processRunner = processRunner ?? throw new ArgumentNullException(nameof(processRunner));
        _kgsmPath = kgsmPath ?? throw new ArgumentNullException(nameof(kgsmPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Dictionary<string, Instance> GetAll()
    {
        _logger.LogDebug("Getting all instances");

        Dictionary<string, Instance> instances = new();
        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instances", "--detailed", "--json");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get instances: {Error}", result.Stderr);
            return instances;
        }

        var serializerOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        serializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));

        try
        {
            instances = JsonSerializer.Deserialize<Dictionary<string, Instance>>(
                result.Stdout,
                serializerOptions
            ) ?? new Dictionary<string, Instance>();

            _logger.LogDebug("Found {Count} instances", instances.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to deserialize instances");
        }

        return instances;
    }

    /// <inheritdoc/>
    public KgsmResult Install(string blueprintName, string? installDir = null, string? version = null, string? name = null)
    {
        ArgumentNullException.ThrowIfNull(blueprintName, nameof(blueprintName));

        _logger.LogDebug("Installing instance of blueprint {Blueprint}", blueprintName);

        List<string> args = new();

        args.Add("--create");
        args.Add(blueprintName);

        if (installDir is not null)
        {
            args.Add("--install-dir");
            args.Add(installDir);
        }

        if (version is not null)
        {
            args.Add("--version");
            args.Add(version);
        }

        if (name is not null)
        {
            args.Add("--name");
            args.Add(name);
        }

        ProcessResult result = _processRunner.Execute(_kgsmPath, args.ToArray());

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to install instance: {Error}", result.Stderr);
        }
        else
        {
            _logger.LogInformation("Successfully installed instance of blueprint {Blueprint}", blueprintName);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult Uninstall(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Uninstalling instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--uninstall", instanceName);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to uninstall instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }
        else
        {
            _logger.LogInformation("Successfully uninstalled instance {InstanceName}", instanceName);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult GetLogs(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting logs for instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--logs");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get logs for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public async Task<string> GetLogsAsync(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting logs asynchronously for instance {InstanceName}", instanceName);

        ProcessResult result = await Task.Run(() => _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--logs"));

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get logs for instance {InstanceName}: {Error}", instanceName, result.Stderr);
            throw new InvalidOperationException($"Failed to get logs for instance '{instanceName}': {result.Stderr}");
        }

        _logger.LogDebug("Successfully got logs for instance {InstanceName}", instanceName);
        return result.Stdout;
    }

    /// <inheritdoc/>
    public KgsmResult GetStatus(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting status for instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--status");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get status for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult GetInfo(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting info for instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--info");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get info for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public bool IsActive(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Checking if instance {InstanceName} is active", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--is-active");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to check if instance {InstanceName} is active: {Error}", instanceName, result.Stderr);
            return false;
        }

        bool isActive = !result.Stdout.Contains("Inactive");
        _logger.LogDebug("Instance {InstanceName} is {Status}", instanceName, isActive ? "active" : "inactive");

        return isActive;
    }

    /// <inheritdoc/>
    public KgsmResult Start(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Starting instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--start");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to start instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }
        else
        {
            _logger.LogInformation("Successfully started instance {InstanceName}", instanceName);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult Stop(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Stopping instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--stop");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to stop instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }
        else
        {
            _logger.LogInformation("Successfully stopped instance {InstanceName}", instanceName);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult Restart(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Restarting instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--restart");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to restart instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }
        else
        {
            _logger.LogInformation("Successfully restarted instance {InstanceName}", instanceName);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult GetInstalledVersion(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting installed version for instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--version", "--installed");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get installed version for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult GetLatestVersion(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting latest version for instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--version", "--latest");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get latest version for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult CheckUpdate(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Checking for updates for instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--check-update");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to check for updates for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult Update(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Updating instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--update");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to update instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }
        else
        {
            _logger.LogInformation("Successfully updated instance {InstanceName}", instanceName);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult GetBackups(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Getting backups for instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--backups");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to get backups for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult CreateBackup(string instanceName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Creating backup for instance {InstanceName}", instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--create-backup");

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to create backup for instance {InstanceName}: {Error}", instanceName, result.Stderr);
        }
        else
        {
            _logger.LogInformation("Successfully created backup for instance {InstanceName}", instanceName);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public KgsmResult RestoreBackup(string instanceName, string backupName)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));
        ArgumentNullException.ThrowIfNull(backupName, nameof(backupName));

        _logger.LogDebug("Restoring backup {BackupName} for instance {InstanceName}", backupName, instanceName);

        ProcessResult result = _processRunner.Execute(_kgsmPath, "--instance", instanceName, "--restore-backup", backupName);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to restore backup {BackupName} for instance {InstanceName}: {Error}", backupName, instanceName, result.Stderr);
        }
        else
        {
            _logger.LogInformation("Successfully restored backup {BackupName} for instance {InstanceName}", backupName, instanceName);
        }

        return new KgsmResult(result);
    }

    /// <inheritdoc/>
    public async Task<LogSubscription> SubscribeToLogsAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        return await SubscribeToLogsAsync(instanceName, Core.Models.LogLevel.Trace, includeRawLines: true, cancellationToken);
    }

    /// <inheritdoc/>
    public Task<LogSubscription> SubscribeToLogsAsync(string instanceName, Core.Models.LogLevel minimumLogLevel, bool includeRawLines = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(instanceName, nameof(instanceName));

        _logger.LogDebug("Starting log subscription for instance {InstanceName} with minimum level {MinimumLevel}",
                        instanceName, minimumLogLevel);

        // Create a combined cancellation token that respects both the provided token and our internal control
        var combinedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        try
        {
            // Create the process to execute KGSM with --follow flag
            var processStartInfo = new ProcessStartInfo
            {
                FileName = _kgsmPath,
                Arguments = $"--instance {instanceName} --logs --follow",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = processStartInfo };

            // Start the process
            if (!process.Start())
            {
                throw new InvalidOperationException($"Failed to start KGSM process for instance '{instanceName}'");
            }

            _logger.LogDebug("Started KGSM log streaming process {ProcessId} for instance {InstanceName}",
                           process.Id, instanceName);

            // Create subscription and streaming task together
            LogSubscription subscription = null!;
            var streamingTask = Task.Run(async () =>
            {
                await ProcessLogStreamAsync(process, () => subscription, minimumLogLevel, includeRawLines, combinedCts.Token);
            }, combinedCts.Token);

            // Create the subscription object
            subscription = new LogSubscription(instanceName, process, streamingTask, combinedCts);

            // Set up process monitoring
            _ = Task.Run(async () =>
            {
                try
                {
                    await process.WaitForExitAsync(combinedCts.Token);

                    if (!combinedCts.Token.IsCancellationRequested)
                    {
                        // Process exited unexpectedly
                        var exitCode = process.ExitCode;
                        var errorOutput = await process.StandardError.ReadToEndAsync();

                        _logger.LogWarning("KGSM log process for instance {InstanceName} exited unexpectedly with code {ExitCode}. Error: {Error}",
                                         instanceName, exitCode, errorOutput);

                        subscription.OnErrorOccurred(new InvalidOperationException(
                            $"KGSM process exited unexpectedly with code {exitCode}: {errorOutput}"));
                        subscription.OnStatusChanged(false, $"Process exited with code {exitCode}");
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected when cancellation is requested
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error monitoring KGSM process for instance {InstanceName}", instanceName);
                    subscription.OnErrorOccurred(ex);
                }
            }, combinedCts.Token);

            // Hook up the log processing to the subscription events
            subscription.OnStatusChanged(true, "Log streaming started");

            _logger.LogInformation("Successfully started log subscription for instance {InstanceName}", instanceName);

            return Task.FromResult(subscription);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start log subscription for instance {InstanceName}", instanceName);
            combinedCts.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Processes the continuous log stream from a KGSM process.
    /// </summary>
    /// <param name="process">The KGSM process providing the log stream.</param>
    /// <param name="subscriptionFactory">Factory function to get the subscription instance.</param>
    /// <param name="minimumLogLevel">The minimum log level to include.</param>
    /// <param name="includeRawLines">Whether to include raw log lines.</param>
    /// <param name="cancellationToken">Cancellation token for stopping the processing.</param>
    private async Task ProcessLogStreamAsync(Process process, Func<LogSubscription> subscriptionFactory, Core.Models.LogLevel minimumLogLevel, bool includeRawLines, CancellationToken cancellationToken)
    {
        try
        {
            using var reader = process.StandardOutput;
            var buffer = new char[4096];
            var lineBuffer = new List<char>();

            while (!cancellationToken.IsCancellationRequested && !process.HasExited)
            {
                try
                {
                    // Read data from the process output
                    var bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead == 0)
                    {
                        // End of stream
                        break;
                    }

                    // Process each character to build complete lines
                    for (int i = 0; i < bytesRead; i++)
                    {
                        var ch = buffer[i];

                        if (ch == '\n' || ch == '\r')
                        {
                            // We have a complete line
                            if (lineBuffer.Count > 0)
                            {
                                var line = new string(lineBuffer.ToArray()).Trim();
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    await ProcessLogLineAsync(line, subscriptionFactory, minimumLogLevel, includeRawLines);
                                }
                                lineBuffer.Clear();
                            }
                        }
                        else
                        {
                            lineBuffer.Add(ch);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    var subscription = subscriptionFactory();
                    _logger.LogError(ex, "Error reading from log stream for instance {InstanceName}", subscription.InstanceName);
                    break;
                }
            }

            // Process any remaining content in the buffer
            if (lineBuffer.Count > 0)
            {
                var line = new string(lineBuffer.ToArray()).Trim();
                if (!string.IsNullOrWhiteSpace(line))
                {
                    await ProcessLogLineAsync(line, subscriptionFactory, minimumLogLevel, includeRawLines);
                }
            }
        }
        catch (Exception ex)
        {
            var subscription = subscriptionFactory();
            _logger.LogError(ex, "Critical error in log stream processing for instance {InstanceName}", subscription.InstanceName);
            throw;
        }
    }

    /// <summary>
    /// Processes a single log line and raises the appropriate events.
    /// </summary>
    /// <param name="line">The raw log line to process.</param>
    /// <param name="subscriptionFactory">Factory function to get the subscription instance.</param>
    /// <param name="minimumLogLevel">The minimum log level to include.</param>
    /// <param name="includeRawLines">Whether to include raw log lines.</param>
    private async Task ProcessLogLineAsync(string line, Func<LogSubscription> subscriptionFactory, Core.Models.LogLevel minimumLogLevel, bool includeRawLines)
    {
        try
        {
            var subscription = subscriptionFactory();

            // Parse the log line
            var logEntry = LogParser.ParseLogLine(line, subscription.InstanceName);

            // Apply log level filtering
            if (logEntry.Level < minimumLogLevel)
            {
                return; // Skip this log entry
            }

            // Optionally remove raw line data to save memory
            if (!includeRawLines)
            {
                logEntry.RawLine = string.Empty;
            }

            // Raise the log received event
            subscription.OnLogReceived(logEntry);

            _logger.LogTrace("Processed log entry for instance {InstanceName}: {Level} - {Message}",
                           subscription.InstanceName, logEntry.Level, logEntry.Message);
        }
        catch (Exception ex)
        {
            var subscription = subscriptionFactory();
            _logger.LogWarning(ex, "Failed to process log line for instance {InstanceName}: {Line}", subscription.InstanceName, line);
            subscription.OnErrorOccurred(ex);
        }

        await Task.CompletedTask; // Make this method async for future extensibility
    }
}
