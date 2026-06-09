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

    /// <summary>
    /// Creates a symlink from the KGSM instances directory to an instance's working directory.
    /// This makes the instance accessible from a standard location.
    /// </summary>
    /// <param name="blueprint">The blueprint the instance was created from.</param>
    /// <param name="instanceName">Instance name to create the symlink for.</param>
    /// <param name="workingDir">The instance's working directory the symlink points to.</param>
    /// <returns>Result of the link operation.</returns>
    /// <exception cref="ArgumentException">Thrown when any argument is null or whitespace.</exception>
    KgsmResult LinkInstance(string blueprint, string instanceName, string workingDir);

    /// <summary>
    /// Removes the symlink from the KGSM instances directory for an instance.
    /// </summary>
    /// <param name="blueprint">The blueprint the instance was created from.</param>
    /// <param name="instanceName">Instance name to remove the symlink for.</param>
    /// <returns>Result of the unlink operation.</returns>
    /// <exception cref="ArgumentException">Thrown when any argument is null or whitespace.</exception>
    KgsmResult UnlinkInstance(string blueprint, string instanceName);

    /// <summary>
    /// Ensures a directory exists, creating it if necessary.
    /// </summary>
    /// <param name="path">The directory path to ensure exists.</param>
    /// <returns>Result of the ensure-created operation.</returns>
    /// <exception cref="ArgumentException">Thrown when path is null or whitespace.</exception>
    KgsmResult EnsureCreated(string path);
}
