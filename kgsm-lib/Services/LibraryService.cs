using Microsoft.Extensions.Logging;
using TheKrystalShip.KGSM.Core.Interfaces;
using TheKrystalShip.KGSM.Core.Models;

namespace TheKrystalShip.KGSM.Services;

/// <summary>
/// Implementation of <see cref="ILibraryService"/> over the KGSM <c>libraries</c> CLI module.
/// </summary>
public class LibraryService : ILibraryService
{
    private readonly IKgsmCommandExecutor _commandExecutor;
    private readonly ILogger<LibraryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryService"/> class.
    /// </summary>
    /// <param name="commandExecutor">The command executor used to run KGSM commands.</param>
    /// <param name="logger">The logger used for diagnostic output.</param>
    public LibraryService(IKgsmCommandExecutor commandExecutor, ILogger<LibraryService> logger)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        _logger.LogDebug("LibraryService initialized");
    }

    /// <inheritdoc/>
    public List<Library>? List()
    {
        // Null is carried through rather than collapsed to an empty list: a host with nothing
        // registered emits "[]", so null here means the read failed. Those are different answers,
        // and a surface that cannot tell them apart offers "no libraries" as a fact it never read.
        return _commandExecutor.ExecuteForJson<List<Library>>(["libraries", "list", "--json"]);
    }

    /// <inheritdoc/>
    public KgsmResult Add(string path, string? name = null, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(path, nameof(path));

        List<string> args = ["libraries", "add", path];

        if (!string.IsNullOrWhiteSpace(name))
        {
            args.Add("--name");
            args.Add(name);
        }

        return Execute(actor, origin, args);
    }

    /// <inheritdoc/>
    public KgsmResult Remove(string name, bool force = false, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        List<string> args = ["libraries", "remove", name];

        if (force)
        {
            args.Add("--force");
        }

        return Execute(actor, origin, args);
    }

    /// <inheritdoc/>
    public KgsmResult Rename(string oldName, string newName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(oldName, nameof(oldName));
        ArgumentNullException.ThrowIfNull(newName, nameof(newName));

        return Execute(actor, origin, ["libraries", "rename", oldName, newName]);
    }

    private KgsmResult Execute(string? actor, string? origin, List<string> args)
    {
        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);
        return provenance is null
            ? _commandExecutor.Execute(args.ToArray())
            : _commandExecutor.Execute(provenance, args.ToArray());
    }
}
