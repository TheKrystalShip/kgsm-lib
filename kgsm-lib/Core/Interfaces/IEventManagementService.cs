using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for managing KGSM event system configuration and transports.
/// This service maps CLI-side event management commands and is separate from
/// IEventService, which reads the engine's event journal.
/// </summary>
public interface IEventManagementService
{
    /// <summary>
    /// Gets the overall event system status: the journal, plus the webhook transport when enabled.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> containing formatted status text.
    /// <see cref="KgsmResult.IsSuccess"/> is true when the status was retrieved successfully.
    /// </returns>
    KgsmResult GetStatus();

    /// <summary>
    /// Tests one or more event transports and returns pass/fail results.
    /// </summary>
    /// <param name="transport">
    /// The transport to test. Must be one of: <c>all</c>, <c>webhook</c>. The journal is not
    /// testable and needs no test — emission is unconditional, and <see cref="GetStatus"/>
    /// reports whether the engine can write to it.
    /// </param>
    /// <returns>
    /// A <see cref="KgsmResult"/> containing pass/fail result text.
    /// <see cref="KgsmResult.IsSuccess"/> is true when all tested transports pass.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="transport"/> is null, whitespace, or not one of the valid values.
    /// </exception>
    KgsmResult TestTransport(string transport);

    /// <summary>
    /// Emits a specific event via KGSM, appending it to the journal (and to the webhook when enabled).
    /// </summary>
    /// <param name="eventType">
    /// The event type to emit (e.g. <c>instance-created</c>, <c>instance-started</c>).
    /// </param>
    /// <param name="parameters">Optional additional parameters for the event.</param>
    /// <returns>
    /// A <see cref="KgsmResult"/> indicating success or failure.
    /// <see cref="KgsmResult.IsSuccess"/> is true when the event was emitted successfully.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="eventType"/> is null or whitespace.
    /// </exception>
    KgsmResult Emit(string eventType, params string[] parameters);

    /// <summary>
    /// Emits an event via KGSM with explicit provenance. Unlike <see cref="Emit(string, string[])"/> (which leaves KGSM to fall
    /// back to the invoking OS user), this stamps the supplied <paramref name="actor"/> and
    /// <paramref name="origin"/> so an autonomous engine component (e.g. the watchdog) can
    /// attribute the event to <c>system</c> rather than its service account.
    /// </summary>
    /// <param name="eventType">
    /// The event type to emit (e.g. <c>instance-crashed</c>, <c>instance-failed</c>).
    /// </param>
    /// <param name="actor">
    /// The audit principal (who), propagated as <c>$KGSM_EVENT_ACTOR</c>. When null/empty,
    /// KGSM applies its OS-user fallback.
    /// </param>
    /// <param name="origin">
    /// The driving surface (<c>ui</c>/<c>assistant</c>/<c>discord</c>/<c>system</c>/<c>api</c>),
    /// propagated as <c>$KGSM_EVENT_ORIGIN</c>. When null/empty, KGSM emits no origin
    /// (never a fabricated surface).
    /// </param>
    /// <param name="parameters">Optional additional parameters for the event.</param>
    /// <returns>
    /// A <see cref="KgsmResult"/> indicating success or failure.
    /// <see cref="KgsmResult.IsSuccess"/> is true when the event was emitted successfully.
    /// </returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="eventType"/> is null or whitespace.
    /// </exception>
    KgsmResult EmitWithProvenance(string eventType, string? actor, string? origin, params string[] parameters);

    /// <summary>
    /// Enables the webhook transport.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> containing the command output.
    /// <see cref="KgsmResult.IsSuccess"/> is true when the transport was enabled successfully.
    /// </returns>
    KgsmResult EnableWebhook();

    /// <summary>
    /// Disables the webhook transport.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> containing the command output.
    /// <see cref="KgsmResult.IsSuccess"/> is true when the transport was disabled successfully.
    /// </returns>
    KgsmResult DisableWebhook();

    /// <summary>
    /// Tests the webhook transport and returns pass/fail results.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> containing pass/fail result text.
    /// <see cref="KgsmResult.IsSuccess"/> is true when the webhook transport test passes.
    /// </returns>
    KgsmResult TestWebhook();

    /// <summary>
    /// Gets the current status of the webhook transport.
    /// </summary>
    /// <returns>
    /// A <see cref="KgsmResult"/> containing formatted webhook status text.
    /// <see cref="KgsmResult.IsSuccess"/> is true when the status was retrieved successfully.
    /// </returns>
    KgsmResult GetWebhookStatus();
}
