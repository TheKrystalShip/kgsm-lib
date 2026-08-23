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
    private readonly KgsmTimeoutOptions _timeouts;
    private readonly ILogger<LibraryService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LibraryService"/> class.
    /// </summary>
    /// <param name="commandExecutor">The command executor used to run KGSM commands.</param>
    /// <param name="logger">The logger used for diagnostic output.</param>
    /// <param name="kgsmOptions">
    /// KGSM options, used here for the drain timeout. Optional: when null (tests that do not
    /// exercise timeouts), generous defaults are used. The DI container injects the registered
    /// instance.
    /// </param>
    public LibraryService(
        IKgsmCommandExecutor commandExecutor,
        ILogger<LibraryService> logger,
        KgsmOptions? kgsmOptions = null)
    {
        _commandExecutor = commandExecutor ?? throw new ArgumentNullException(nameof(commandExecutor));
        _timeouts = kgsmOptions?.Timeouts ?? new KgsmTimeoutOptions();
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
    public KgsmResult Remove(string name, bool force = false, string? drainTo = null, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(name, nameof(name));

        List<string> args = ["libraries", "remove", name];

        if (!string.IsNullOrWhiteSpace(drainTo))
        {
            args.Add("--drain");
            args.Add(drainTo);
        }

        if (force)
        {
            args.Add("--force");
        }

        // A drain copies every resident instance's tree; a bare deregistration writes a registry
        // line. Only the first needs the ceiling, and giving the second the same one would let a
        // hung registry write sit for hours.
        return drainTo is null
            ? Execute(actor, origin, args)
            : Execute(actor, origin, args, _timeouts.Move);
    }

    /// <inheritdoc/>
    public KgsmResult Rename(string oldName, string newName, string? actor = null, string? origin = null)
    {
        ArgumentNullException.ThrowIfNull(oldName, nameof(oldName));
        ArgumentNullException.ThrowIfNull(newName, nameof(newName));

        return Execute(actor, origin, ["libraries", "rename", oldName, newName]);
    }

    private KgsmResult Execute(string? actor, string? origin, List<string> args, TimeSpan? timeout = null)
    {
        IReadOnlyDictionary<string, string>? provenance = KgsmProvenance.Build(actor, origin);

        if (timeout is null)
        {
            return provenance is null
                ? _commandExecutor.Execute(args.ToArray())
                : _commandExecutor.Execute(provenance, args.ToArray());
        }

        return provenance is null
            ? _commandExecutor.Execute(timeout.Value, args.ToArray())
            : _commandExecutor.Execute(provenance, timeout.Value, args.ToArray());
    }
}
