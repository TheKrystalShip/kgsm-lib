using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for executing processes.
/// </summary>
public interface IProcessRunner
{
    /// <summary>
    /// Executes a command with the specified arguments.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <returns>Result of the command execution.</returns>
    ProcessResult Execute(string command, params string[] args);

    /// <summary>
    /// Asynchronously executes a command with the specified arguments.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="args">Arguments to pass to the command.</param>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation with the result of the command execution.</returns>
    Task<ProcessResult> ExecuteAsync(string command, string[] args, CancellationToken cancellationToken = default);
}
