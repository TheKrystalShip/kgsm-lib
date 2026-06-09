using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for executing processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Executes a command with the specified arguments, using the default timeout.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <returns>Result of the command execution.</returns>
    ProcessResult Execute(string command, params string[] args);

    /// <summary>
    /// Executes a command with an explicit timeout. If the process does not exit
    /// within <paramref name="timeout"/>, its entire process tree is killed and a
    /// failure result is returned whose error text indicates a timeout.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="timeout">Maximum time to wait for the process to exit.</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <returns>Result of the command execution.</returns>
    ProcessResult Execute(string command, TimeSpan timeout, params string[] args);

    /// <summary>
    /// Asynchronously executes a command with the specified arguments.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the result of the command execution.</returns>
    Task<ProcessResult> ExecuteAsync(string command, string[] args, CancellationToken cancellationToken = default);
}
