using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of the IBlueprintService interface for managing blueprints in KGSM.
/// </summary>
public class BlueprintService : IBlueprintService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<BlueprintService> _logger;

    /// <summary>
    /// Initializes a new instance of the BlueprintService class.
    /// </summary>
    /// <param name="commandExecutor">The command executor to use for executing KGSM commands.</param>
    /// <param name="logger">The logger to use for logging.</param>
    public BlueprintService(IKgsmCommandExecutor commandExecutor, ILogger<BlueprintService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("BlueprintService initialized");
    }

    /// <inheritdoc/>
    public List<string> List()
    {
        _logger.LogDebug("Listing all blueprints");

        List<string>? blueprintNames = _commandExecutor
            .ExecuteForJson<List<string>>(["blueprints", "list", "--json"]);

        if (blueprintNames == null)
        {
            _logger.LogWarning("No blueprint names found");
            return new();
        }

        _logger.LogDebug("Found {Count} blueprint names", blueprintNames.Count);
        return blueprintNames;
    }

    /// <inheritdoc/>
    public List<string> ListDefault()
    {
        _logger.LogDebug("Listing default blueprints");

        List<string>? blueprintNames = _commandExecutor
            .ExecuteForJson<List<string>>(["blueprints", "list", "default", "--json"]);

        if (blueprintNames == null)
        {
            _logger.LogWarning("No default blueprint names found");
            return new();
        }

        _logger.LogDebug("Found {Count} default blueprint names", blueprintNames.Count);
        return blueprintNames;
    }

    /// <inheritdoc/>
    public List<string> ListCustom()
    {
        _logger.LogDebug("Listing custom blueprints");

        List<string>? blueprintNames = _commandExecutor
            .ExecuteForJson<List<string>>(["blueprints", "list", "custom", "--json"]);

        if (blueprintNames == null)
        {
            _logger.LogWarning("No custom blueprint names found");
            return new();
        }

        _logger.LogDebug("Found {Count} custom blueprint names", blueprintNames.Count);
        return blueprintNames;
    }

    /// <inheritdoc/>
    public Dictionary<string, Blueprint> ListDetailed()
    {
        _logger.LogDebug("Listing detailed blueprints");

        Dictionary<string, Blueprint>? detailedBlueprints = _commandExecutor
            .ExecuteForJson<Dictionary<string, Blueprint>>(["blueprints", "list", "detailed", "--json"]);

        if (detailedBlueprints == null)
        {
            _logger.LogWarning("No detailed blueprints found");
            return new();
        }

        _logger.LogDebug("Found {Count} detailed blueprints", detailedBlueprints.Count);
        return detailedBlueprints;
    }

    /// <inheritdoc/>
    public Blueprint? GetInfo(string blueprintName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintName, nameof(blueprintName));

        _logger.LogDebug("Getting info for blueprint: {Name}", blueprintName);

        Blueprint? blueprint = _commandExecutor
            .ExecuteForJson<Blueprint>(["blueprints", "info", blueprintName, "--json"]);

        if (blueprint != null)
        {
            _logger.LogDebug("Successfully retrieved info for blueprint: {Name}", blueprintName);
        }

        return blueprint;
    }

    /// <inheritdoc/>
    public string? FindPath(string blueprintName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blueprintName, nameof(blueprintName));

        _logger.LogDebug("Finding path for blueprint: {Name}", blueprintName);

        KgsmResult result = _commandExecutor
            .Execute("blueprints", "find", blueprintName);

        if (result.ExitCode != 0)
        {
            _logger.LogError("Failed to find blueprint path for {Name}: {Error}", blueprintName, result.Stderr);
            return null;
        }

        string path = result.Stdout.Trim();

        if (string.IsNullOrWhiteSpace(path))
        {
            _logger.LogWarning("Blueprint path for {Name} is empty", blueprintName);
            return null;
        }

        _logger.LogDebug("Found blueprint path for {Name}: {Path}", blueprintName, path);
        return path;
    }
}
