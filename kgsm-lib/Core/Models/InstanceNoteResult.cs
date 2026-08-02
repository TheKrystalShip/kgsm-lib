namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// The outcome of writing an instance's server note. The note spans three config keys
/// (<c>note_updated_by</c>, <c>note_updated_at</c>, then <c>note</c>) and kgsm sets one key per
/// invocation, so the write is a sequence rather than a transaction: this reports exactly which
/// keys landed.
/// </summary>
/// <remarks>
/// The body is written <strong>last</strong> on purpose. A failure part-way therefore leaves the
/// <em>old</em> body with fresh attribution — visibly wrong to a reader, but never a new body
/// credited to the wrong person, and a retry of the same save repairs it. A caller that reports
/// the failure should surface <see cref="AppliedKeys"/> so the partial state is not silent.
/// </remarks>
/// <param name="IsSuccess">Whether all three keys were written.</param>
/// <param name="AppliedKeys">The config keys that were written, in the order they were applied.</param>
/// <param name="FailedKey">The key whose write failed, or <see langword="null"/> on success.</param>
/// <param name="Error">The engine's stderr for the failed write, or <see langword="null"/> on success.</param>
/// <param name="ExitCode">The exit code of the failed write, or <c>0</c> on success.</param>
public sealed record InstanceNoteResult(
    bool IsSuccess,
    IReadOnlyList<string> AppliedKeys,
    string? FailedKey = null,
    string? Error = null,
    int ExitCode = 0);
