namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Represents the result of a process execution.
/// </summary>
/// <param name="ExitCode">The exit code of the process.</param>
/// <param name="Stdout">The standard output of the process.</param>
/// <param name="Stderr">The standard error output of the process.</param>
public record ProcessResult(int ExitCode, string Stdout = "", string Stderr = "")
{
    /// <summary>
    /// Exit code representing a successful command execution.
    /// </summary>
    public const int SuccessExitCode = 0;

    /// <summary>
    /// Exit code representing a failed command execution.
    /// </summary>
    public const int FailureExitCode = 1;
}
