namespace TheKrystalShip.KGSM.Core.Models;

/// <summary>
/// Options for configuring KGSM services.
/// </summary>
public class KgsmOptions
{
    /// <summary>
    /// Gets or sets the path to the KGSM executable.
    /// </summary>
    public string KgsmPath { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the path to the KGSM Unix socket.
    /// </summary>
    public string SocketPath { get; set; } = string.Empty;
}