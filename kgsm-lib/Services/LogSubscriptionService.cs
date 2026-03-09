using Microsoft.Extensions.Logging;
using System.Diagnostics;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;
using TheKrystalShip.KGSM.Core.Models.Enums;
using TheKrystalShip.KGSM.Utilities;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the ILogSubscriptionService interface for managing log subscriptions in KGSM.
/// Handles real-time log streaming from KGSM instances with filtering and event-based notification.
/// </summary>
public class LogSubscriptionService : ILogSubscriptionService
{
    private readonly string _kgsmPath;
    private readonly ILogger<LogSubscriptionService> _logger;

    /// <summary>
    /// Initializes a new instance of the LogSubscriptionService class.
    /// </summary>
    /// <param name="kgsmOptions">The options for KGSM, including the path to the executable.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public LogSubscriptionService(
        KgsmOptions kgsmOptions,
        ILogger<LogSubscriptionService> logger)
    {
        _kgsmPath = kgsmOptions.KgsmPath ?? throw new ArgumentNullException(nameof(kgsmOptions.KgsmPath));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("LogSubscriptionService initialized with KGSM path: {KgsmPath}", _kgsmPath);
    }

    /// <inheritdoc/>
    public async Task<LogSubscription> SubscribeToLogsAsync(string instanceName, CancellationToken cancellationToken = default)
    {
        return await SubscribeToLogsAsync(instanceName, Core.Models.Enums.LogLevel.Trace, includeRawLines: true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public Task<LogSubscription> SubscribeToLogsAsync(
        string instanceName,
        Core.Models.Enums.LogLevel minimumLogLevel,
        bool includeRawLines = true,
        CancellationToken cancellationToken = default)
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
                await ProcessLogStreamAsync(process, () => subscription, minimumLogLevel, includeRawLines, combinedCts.Token).ConfigureAwait(false);
            }, combinedCts.Token);

            // Create the subscription object
            subscription = new LogSubscription(instanceName, process, streamingTask, combinedCts);

            // Set up process monitoring
            _ = Task.Run(async () =>
            {
                try
                {
                    await process.WaitForExitAsync(combinedCts.Token).ConfigureAwait(false);

                    if (!combinedCts.Token.IsCancellationRequested)
                    {
                        // Process exited unexpectedly
                        var exitCode = process.ExitCode;
                        var errorOutput = await process.StandardError.ReadToEndAsync().ConfigureAwait(false);

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
                    _logger.LogDebug("Log process monitoring cancelled for instance {InstanceName}", instanceName);
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
    private async Task ProcessLogStreamAsync(
        Process process,
        Func<LogSubscription> subscriptionFactory,
        Core.Models.Enums.LogLevel minimumLogLevel,
        bool includeRawLines,
        CancellationToken cancellationToken)
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
                    var bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);

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
                            // Line break found, process the accumulated line
                            if (lineBuffer.Count > 0)
                            {
                                var line = new string(lineBuffer.ToArray()).Trim();
                                if (!string.IsNullOrWhiteSpace(line))
                                {
                                    await ProcessLogLineAsync(line, subscriptionFactory, minimumLogLevel, includeRawLines).ConfigureAwait(false);
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
                    await ProcessLogLineAsync(line, subscriptionFactory, minimumLogLevel, includeRawLines).ConfigureAwait(false);
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
    private async Task ProcessLogLineAsync(
        string line,
        Func<LogSubscription> subscriptionFactory,
        Core.Models.Enums.LogLevel minimumLogLevel,
        bool includeRawLines)
    {
        try
        {
            LogSubscription subscription = subscriptionFactory();

            // Parse the log line
            LogEntry logEntry = LogParser.ParseLogLine(line, subscription.InstanceName);

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

        await Task.CompletedTask.ConfigureAwait(false); // Make this method async for future extensibility
    }
}
