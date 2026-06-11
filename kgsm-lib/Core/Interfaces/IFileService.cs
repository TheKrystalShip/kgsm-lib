using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Core.Interfaces;

/// <summary>
/// Interface for managing file operations in KGSM.
/// Creates and manages all necessary files for game server operation.
/// </summary>
public interface IFileService
{
    /// <summary>
    /// Generates all required files for the instance including:
    /// - instance.manage.sh
    /// - systemd service/socket files (if applicable)
    /// - UFW firewall rules (if applicable)
    /// - symlink to the management file (if applicable)
    /// - UPnP configuration files (if applicable)
    /// </summary>
    /// <param name="instanceName">Instance name to create files for.</param>
    /// <returns>Result of the create operation.</returns>
    KgsmResult Create(string instanceName);

    /// <summary>
    /// Creates the instance.manage.sh management script.
    /// </summary>
    /// <param name="instanceName">Instance name to create management script for.</param>
    /// <returns>Result of the create operation.</returns>
    KgsmResult CreateManage(string instanceName);

    /// <summary>
    /// Copies instance configuration file to working directory.
    /// </summary>
    /// <param name="instanceName">Instance name to copy configuration for.</param>
    /// <returns>Result of the create operation.</returns>
    [Obsolete("kgsm removed the standalone 'files config' component (it deleted files.config.sh); " +
        "the instance config file is now created automatically by Create(). This method issues a " +
        "command kgsm no longer routes and will always fail. Scheduled for removal in the next major version.")]
    KgsmResult CreateConfig(string instanceName);

    /// <summary>
    /// Generates systemd service/socket files for the instance.
    /// </summary>
    /// <param name="instanceName">Instance name to create systemd files for.</param>
    /// <returns>Result of the create operation.</returns>
    KgsmResult CreateSystemd(string instanceName);

    /// <summary>
    /// Generates and enables UFW firewall rule for the instance.
    /// </summary>
    /// <param name="instanceName">Instance name to create UFW rule for.</param>
    /// <returns>Result of the create operation.</returns>
    KgsmResult CreateUfw(string instanceName);

    /// <summary>
    /// Creates a symlink to the management file in the PATH.
    /// </summary>
    /// <param name="instanceName">Instance name to create symlink for.</param>
    /// <returns>Result of the create operation.</returns>
    KgsmResult CreateSymlink(string instanceName);

    /// <summary>
    /// Generates UPnP configuration files for the instance (if applicable).
    /// </summary>
    /// <param name="instanceName">Instance name to create UPnP configuration for.</param>
    /// <returns>Result of the create operation.</returns>
    KgsmResult CreateUpnp(string instanceName);

    /// <summary>
    /// Removes all files and integrations for instance uninstall.
    /// </summary>
    /// <param name="instanceName">Instance name to remove files for.</param>
    /// <returns>Result of the remove operation.</returns>
    KgsmResult Remove(string instanceName);

    /// <summary>
    /// Removes systemd service/socket files for the instance.
    /// </summary>
    /// <param name="instanceName">Instance name to remove systemd files for.</param>
    /// <returns>Result of the remove operation.</returns>
    KgsmResult RemoveSystemd(string instanceName);

    /// <summary>
    /// Removes UFW firewall rules for the instance.
    /// </summary>
    /// <param name="instanceName">Instance name to remove UFW rules for.</param>
    /// <returns>Result of the remove operation.</returns>
    KgsmResult RemoveUfw(string instanceName);

    /// <summary>
    /// Removes the symlink to the management file.
    /// </summary>
    /// <param name="instanceName">Instance name to remove symlink for.</param>
    /// <returns>Result of the remove operation.</returns>
    KgsmResult RemoveSymlink(string instanceName);

    /// <summary>
    /// Removes UPnP configuration files for the instance.
    /// </summary>
    /// <param name="instanceName">Instance name to remove UPnP configuration for.</param>
    /// <returns>Result of the remove operation.</returns>
    KgsmResult RemoveUpnp(string instanceName);

    /// <summary>
    /// Removes the instance configuration file from working directory.
    /// </summary>
    /// <param name="instanceName">Instance name to remove configuration for.</param>
    /// <returns>Result of the remove operation.</returns>
    [Obsolete("kgsm removed the standalone 'files config' component (it deleted files.config.sh); " +
        "config-file cleanup is now handled by Remove(). This method issues a command kgsm no longer " +
        "routes and will always fail. Scheduled for removal in the next major version.")]
    KgsmResult RemoveConfig(string instanceName);

    /// <summary>
    /// Removes the management file (instance.manage.sh).
    /// </summary>
    /// <param name="instanceName">Instance name to remove management file for.</param>
    /// <returns>Result of the remove operation.</returns>
    KgsmResult RemoveManage(string instanceName);
}
