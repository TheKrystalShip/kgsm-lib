using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IProcessRunner interface for executing processes.
/// </summary>
public class ProcessRunner : IProcessRunner
{
    private const int EXIT_CODE_SUCCESS = 0;
    private const int EXIT_CODE_FAILURE = 1;
    private const int DEFAULT_TIMEOUT_MS = 30000; // 30 seconds

    private readonly ILogger<ProcessRunner> _logger;

    /// <summary>
    /// Initializes a new instance of the ProcessRunner class.
    /// </summary>
    /// <param name="logger">The logger to use for logging.</param>
    public ProcessRunner(ILogger<ProcessRunner> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("ProcessRunner initialized");
    }

    /// <inheritdoc/>
    public ProcessResult Execute(string command, params string[] args)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        string arguments = args.Length > 0 ? string.Join(" ", args) : string.Empty;

        _logger.LogDebug("Executing command: {Command} {Arguments}", command, arguments);

        ProcessStartInfo processStartInfo = new()
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        Process? process;

        try
        {
            process = Process.Start(processStartInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process: {Command} {Arguments}", command, arguments);
            return new ProcessResult(EXIT_CODE_FAILURE, string.Empty, ex.Message);
        }

        if (process is null)
        {
            _logger.LogError("Process failed to start: {Command} {Arguments}", command, arguments);
            return new ProcessResult(EXIT_CODE_FAILURE, string.Empty, "Process failed to start");
        }

        using (process)
        {
            try
            {
                // Use async reading to prevent deadlocks when buffers fill up
                // This is critical for processes with large output
                List<string> stdoutLines = new();
                List<string> stderrLines = new();

                // Start asynchronous reading of stdout and stderr
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        stdoutLines.Add(e.Data);
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        stderrLines.Add(e.Data);
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Wait for process to exit with timeout
                bool exited = process.WaitForExit(DEFAULT_TIMEOUT_MS);

                if (!exited)
                {
                    _logger.LogError("Process timed out after {Timeout}ms: {Command} {Arguments}", DEFAULT_TIMEOUT_MS, command, arguments);

                    try
                    {
                        process.Kill(entireProcessTree: true);
                    }
                    catch (Exception killEx)
                    {
                        _logger.LogWarning(killEx, "Failed to kill timed out process: {Command} {Arguments}", command, arguments);
                    }

                    return new ProcessResult(EXIT_CODE_FAILURE, string.Empty, $"Process execution timed out after {DEFAULT_TIMEOUT_MS}ms");
                }

                // Ensure all output has been read
                process.WaitForExit();

                int exitCode = process.ExitCode;
                string stdout = string.Join(Environment.NewLine, stdoutLines).Trim();
                string stderr = string.Join(Environment.NewLine, stderrLines).Trim();

                if (exitCode != 0)
                {
                    _logger.LogWarning("Command failed with exit code {ExitCode}: {Command} {Arguments}", exitCode, command, arguments);
                    _logger.LogDebug("Command stderr: {Stderr}", stderr);
                }
                else
                {
                    _logger.LogDebug("Command succeeded: {Command} {Arguments}", command, arguments);
                }

                return new ProcessResult(exitCode, stdout, stderr);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute process: {Command} {Arguments}", command, arguments);
                return new ProcessResult(EXIT_CODE_FAILURE, string.Empty, ex.Message);
            }
        }
    }

    /// <inheritdoc/>
    public async Task<ProcessResult> ExecuteAsync(string command, string[] args, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command, nameof(command));

        string arguments = args.Length > 0 ? string.Join(" ", args) : string.Empty;

        _logger.LogDebug("Executing command asynchronously: {Command} {Arguments}", command, arguments);

        ProcessStartInfo processStartInfo = new()
        {
            FileName = command,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        Process? process;

        try
        {
            process = Process.Start(processStartInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start process: {Command} {Arguments}", command, arguments);
            return new ProcessResult(EXIT_CODE_FAILURE, string.Empty, ex.Message);
        }

        if (process is null)
        {
            _logger.LogError("Process failed to start: {Command} {Arguments}", command, arguments);
            return new ProcessResult(EXIT_CODE_FAILURE, string.Empty, "Process failed to start");
        }

        using (process)
        {
            try
            {
                // Use async reading to prevent deadlocks when buffers fill up
                // This is critical for processes with large output
                List<string> stdoutLines = new();
                List<string> stderrLines = new();
                object stdoutLock = new();
                object stderrLock = new();

                // Start asynchronous reading of stdout and stderr
                process.OutputDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        lock (stdoutLock)
                        {
                            stdoutLines.Add(e.Data);
                        }
                    }
                };

                process.ErrorDataReceived += (sender, e) =>
                {
                    if (e.Data != null)
                    {
                        lock (stderrLock)
                        {
                            stderrLines.Add(e.Data);
                        }
                    }
                };

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                // Register cancellation
                using (cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                        {
                            _logger.LogWarning("Process cancelled: {Command} {Arguments}", command, arguments);
                            process.Kill(entireProcessTree: true);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to kill cancelled process: {Command} {Arguments}", command, arguments);
                    }
                }))
                {
                    // Wait for process to exit asynchronously
                    await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                    // Ensure all output has been read
                    process.WaitForExit();

                    int exitCode = process.ExitCode;
                    string stdout;
                    string stderr;

                    lock (stdoutLock)
                    {
                        stdout = string.Join(Environment.NewLine, stdoutLines).Trim();
                    }

                    lock (stderrLock)
                    {
                        stderr = string.Join(Environment.NewLine, stderrLines).Trim();
                    }

                    if (exitCode != 0)
                    {
                        _logger.LogWarning("Command failed with exit code {ExitCode}: {Command} {Arguments}", exitCode, command, arguments);
                        _logger.LogDebug("Command stderr: {Stderr}", stderr);
                    }
                    else
                    {
                        _logger.LogDebug("Command succeeded: {Command} {Arguments}", command, arguments);
                    }

                    return new ProcessResult(exitCode, stdout, stderr);
                }
            }
            catch (OperationCanceledException)
            {
                _logger.LogWarning("Process execution was cancelled: {Command} {Arguments}", command, arguments);
                return new ProcessResult(EXIT_CODE_FAILURE, string.Empty, "Process execution was cancelled");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to execute process: {Command} {Arguments}", command, arguments);
                return new ProcessResult(EXIT_CODE_FAILURE, string.Empty, ex.Message);
            }
        }
    }
}
