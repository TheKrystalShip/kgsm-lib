namespace TheKrystalShip.KGSM.Tests.Services;

using TheKrystalShip.KGSM.Core.Models.Enums;

/// <summary>
/// Tests for the LibraryService class — the typed surface over <c>kgsm libraries</c>.
/// </summary>
public class LibraryServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<LibraryService>> _mockLogger;
    private readonly LibraryService _libraryService;

    public LibraryServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<LibraryService>>();
        _libraryService = new LibraryService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    private static bool ArgsAre(string[] actual, params string[] expected)
        => actual.SequenceEqual(expected);

    // --- Constructor guards ---

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LibraryService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new LibraryService(_mockCommandExecutor.Object, null!));
    }

    // --- List ---

    [Fact]
    public void List_IssuesTheJsonListCommand()
    {
        List<Library> libraries =
        [
            new() { Name = "default", Path = "/opt", State = LibraryState.Online, InstanceCount = 7 }
        ];

        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson(
                It.Is<string[]>(a => ArgsAre(a, "libraries", "list", "--json")),
                null,
                default(List<Library>)))
            .Returns(libraries);

        List<Library>? result = _libraryService.List();

        Assert.NotNull(result);
        Assert.Single(result!);
        Assert.Equal("default", result![0].Name);
        Assert.True(result[0].Online);
    }

    [Fact]
    public void List_FailedRead_ReturnsNull_NotAnEmptyList()
    {
        // A host with nothing registered emits "[]". Null therefore means the read failed,
        // and a caller must be able to tell the two apart.
        _mockCommandExecutor
            .Setup(x => x.ExecuteForJson(
                It.IsAny<string[]>(),
                null,
                default(List<Library>)))
            .Returns((List<Library>?)null);

        Assert.Null(_libraryService.List());
    }

    // --- Add ---

    [Fact]
    public void Add_NullPath_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _libraryService.Add(null!));
    }

    [Fact]
    public void Add_WithoutName_PassesOnlyThePath()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "libraries", "add", "/mnt/ssd"))))
            .Returns(new KgsmResult(new ProcessResult(0, "added", string.Empty)));

        KgsmResult result = _libraryService.Add("/mnt/ssd");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Add_WithName_PassesTheNameFlag()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "libraries", "add", "/mnt/ssd", "--name", "ssd"))))
            .Returns(new KgsmResult(new ProcessResult(0, "added", string.Empty)));

        KgsmResult result = _libraryService.Add("/mnt/ssd", "ssd");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Add_WithProvenance_TakesTheEnvironmentOverload()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(
                It.Is<IReadOnlyDictionary<string, string>>(e =>
                    e["KGSM_EVENT_ACTOR"] == "discord:haru" && e["KGSM_EVENT_ORIGIN"] == "discord"),
                It.Is<string[]>(a => ArgsAre(a, "libraries", "add", "/mnt/ssd"))))
            .Returns(new KgsmResult(new ProcessResult(0, "added", string.Empty)));

        KgsmResult result = _libraryService.Add("/mnt/ssd", actor: "discord:haru", origin: "discord");

        Assert.True(result.IsSuccess);
        _mockCommandExecutor.Verify(x => x.Execute(It.IsAny<string[]>()), Times.Never);
    }

    // --- Remove ---

    [Fact]
    public void Remove_NullName_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _libraryService.Remove(null!));
    }

    [Fact]
    public void Remove_WithoutForce_PassesNoForceFlag()
    {
        // The engine's refusal names the instances that block the removal; suppressing it
        // here would take that answer away from the surface.
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "libraries", "remove", "ssd"))))
            .Returns(new KgsmResult(new ProcessResult(0, "removed", string.Empty)));

        KgsmResult result = _libraryService.Remove("ssd");

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Remove_WithForce_PassesTheForceFlag()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "libraries", "remove", "ssd", "--force"))))
            .Returns(new KgsmResult(new ProcessResult(0, "removed", string.Empty)));

        KgsmResult result = _libraryService.Remove("ssd", force: true);

        Assert.True(result.IsSuccess);
    }

    // --- Rename ---

    [Fact]
    public void Rename_NullNames_ThrowArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() => _libraryService.Rename(null!, "fast"));
        Assert.Throws<ArgumentNullException>(() => _libraryService.Rename("ssd", null!));
    }

    [Fact]
    public void Rename_PassesBothNames()
    {
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(a => ArgsAre(a, "libraries", "rename", "ssd", "fast"))))
            .Returns(new KgsmResult(new ProcessResult(0, "renamed", string.Empty)));

        KgsmResult result = _libraryService.Rename("ssd", "fast");

        Assert.True(result.IsSuccess);
    }
}
