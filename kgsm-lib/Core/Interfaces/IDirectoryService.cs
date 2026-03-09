using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for managing directory operations in KGSM.
/// Creates and manages the directory structure needed for game server instances.
/// </summary>
public interface IDirectoryService
{
    /// <summary>
    /// Creates the directory structure for an instance.
    /// Creates installation, data, logs, and backup directories.
    /// </summary>
    /// <param name="instanceName">Instance name to create directories for.</param>
    /// <returns>Result of the create operation.</returns>
    KgsmResult Create(string instanceName);

    /// <summary>
    /// Removes the directory structure for an instance.
    /// Warning: This will delete all instance data.
    /// </summary>
    /// <param name="instanceName">Instance name to remove directories for.</param>
    /// <returns>Result of the remove operation.</returns>
    KgsmResult Remove(string instanceName);
}
