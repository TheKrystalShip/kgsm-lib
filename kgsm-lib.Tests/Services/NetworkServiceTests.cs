namespace TheKrystalShip.KGSM.Tests.Services;

/// <summary>
/// Tests for the NetworkService class.
/// </summary>
public class NetworkServiceTests
{
    private readonly Mock<IKgsmCommandExecutor> _mockCommandExecutor;
    private readonly Mock<ILogger<NetworkService>> _mockLogger;
    private readonly NetworkService _networkService;

    public NetworkServiceTests()
    {
        _mockCommandExecutor = new Mock<IKgsmCommandExecutor>();
        _mockLogger = new Mock<ILogger<NetworkService>>();
        _networkService = new NetworkService(_mockCommandExecutor.Object, _mockLogger.Object);
    }

    // -------------------------------------------------------------------------
    // Constructor
    // -------------------------------------------------------------------------

    [Fact]
    public void Constructor_NullCommandExecutor_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new NetworkService(null!, _mockLogger.Object));
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        Assert.Throws<ArgumentNullException>(() =>
            new NetworkService(_mockCommandExecutor.Object, null!));
    }

    // -------------------------------------------------------------------------
    // CheckPort
    // -------------------------------------------------------------------------

    [Fact]
    public void CheckPort_PortZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _networkService.CheckPort(0));
    }

    [Fact]
    public void CheckPort_PortAboveMax_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _networkService.CheckPort(65536));
    }

    [Fact]
    public void CheckPort_InvalidProtocol_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _networkService.CheckPort(8080, "ftp"));
    }

    [Fact]
    public void CheckPort_PortFree_ReturnsSuccessResult()
    {
        // Arrange
        const int port = 8080;
        const string protocol = "tcp";

        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "check", port.ToString(), protocol }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Port 8080/tcp is free", string.Empty)));

        // Act
        KgsmResult result = _networkService.CheckPort(port, protocol);

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void CheckPort_PortInUse_ReturnsFailureResult()
    {
        // Arrange
        const int port = 8080;
        const string protocol = "tcp";

        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "check", port.ToString(), protocol }))))
            .Returns(new KgsmResult(new ProcessResult(1, "Port 8080/tcp is in use", string.Empty)));

        // Act
        KgsmResult result = _networkService.CheckPort(port, protocol);

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    [Fact]
    public void CheckPort_DefaultProtocolIsTcp_UsesTocpArgument()
    {
        // Arrange
        const int port = 25565;

        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "check", port.ToString(), "tcp" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Port 25565/tcp is free", string.Empty)));

        // Act
        KgsmResult result = _networkService.CheckPort(port);

        // Assert
        Assert.True(result.IsSuccess);
    }

    // -------------------------------------------------------------------------
    // ListUsedPorts
    // -------------------------------------------------------------------------

    [Fact]
    public void ListUsedPorts_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "list-used" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "PORT  PROTOCOL  PROCESS\n8080  tcp       nginx", string.Empty)));

        // Act
        KgsmResult result = _networkService.ListUsedPorts();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Equal(0, result.ExitCode);
    }

    [Fact]
    public void ListUsedPorts_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "list-used" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to list ports")));

        // Act
        KgsmResult result = _networkService.ListUsedPorts();

        // Assert
        Assert.False(result.IsSuccess);
        Assert.Equal(1, result.ExitCode);
    }

    // -------------------------------------------------------------------------
    // FindConflicts
    // -------------------------------------------------------------------------

    [Fact]
    public void FindConflicts_NoConflicts_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "conflicts" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "No port conflicts found", string.Empty)));

        // Act
        KgsmResult result = _networkService.FindConflicts();

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void FindConflicts_ConflictsFound_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Probe(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "conflicts" }))))
            .Returns(new KgsmResult(new ProcessResult(1, "Conflict: valheim and minecraft both use port 2456", string.Empty)));

        // Act
        KgsmResult result = _networkService.FindConflicts();

        // Assert
        Assert.False(result.IsSuccess);
    }

    // -------------------------------------------------------------------------
    // KillPort
    // -------------------------------------------------------------------------

    [Fact]
    public void KillPort_PortZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _networkService.KillPort(0));
    }

    [Fact]
    public void KillPort_PortAboveMax_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _networkService.KillPort(65536));
    }

    [Fact]
    public void KillPort_InvalidProtocol_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _networkService.KillPort(8080, "icmp"));
    }

    [Fact]
    public void KillPort_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const int port = 8080;
        const string protocol = "tcp";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "kill", port.ToString(), protocol }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Process on 8080/tcp killed", string.Empty)));

        // Act
        KgsmResult result = _networkService.KillPort(port, protocol);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void KillPort_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        const int port = 8080;
        const string protocol = "tcp";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ports", "kill", port.ToString(), protocol }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "No process found on port 8080/tcp")));

        // Act
        KgsmResult result = _networkService.KillPort(port, protocol);

        // Assert
        Assert.False(result.IsSuccess);
    }

    // -------------------------------------------------------------------------
    // TestPort
    // -------------------------------------------------------------------------

    [Fact]
    public void TestPort_PortZero_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _networkService.TestPort(0));
    }

    [Fact]
    public void TestPort_PortAboveMax_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _networkService.TestPort(65536));
    }

    [Fact]
    public void TestPort_InvalidProtocol_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() => _networkService.TestPort(8080, "ssh"));
    }

    [Fact]
    public void TestPort_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        const int port = 25565;
        const string protocol = "udp";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "test-port", port.ToString(), protocol }))))
            .Returns(new KgsmResult(new ProcessResult(0, "Port 25565/udp is accessible", string.Empty)));

        // Act
        KgsmResult result = _networkService.TestPort(port, protocol);

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TestPort_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        const int port = 25565;
        const string protocol = "udp";

        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "test-port", port.ToString(), protocol }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Port 25565/udp is not accessible")));

        // Act
        KgsmResult result = _networkService.TestPort(port, protocol);

        // Assert
        Assert.False(result.IsSuccess);
    }

    // -------------------------------------------------------------------------
    // TestAllPorts
    // -------------------------------------------------------------------------

    [Fact]
    public void TestAllPorts_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "test-all" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "All ports are accessible", string.Empty)));

        // Act
        KgsmResult result = _networkService.TestAllPorts();

        // Assert
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void TestAllPorts_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "test-all" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Some ports are not accessible")));

        // Act
        KgsmResult result = _networkService.TestAllPorts();

        // Assert
        Assert.False(result.IsSuccess);
    }

    // -------------------------------------------------------------------------
    // GetIp
    // -------------------------------------------------------------------------

    [Fact]
    public void GetIp_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ip" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "External: 1.2.3.4\nLocal: 192.168.1.100", string.Empty)));

        // Act
        KgsmResult result = _networkService.GetIp();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("1.2.3.4", result.Stdout);
    }

    [Fact]
    public void GetIp_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "ip" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to retrieve IP")));

        // Act
        KgsmResult result = _networkService.GetIp();

        // Assert
        Assert.False(result.IsSuccess);
    }

    // -------------------------------------------------------------------------
    // GetDns
    // -------------------------------------------------------------------------

    [Fact]
    public void GetDns_SuccessfulExecution_ReturnsSuccessResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "dns" }))))
            .Returns(new KgsmResult(new ProcessResult(0, "8.8.8.8\n8.8.4.4", string.Empty)));

        // Act
        KgsmResult result = _networkService.GetDns();

        // Assert
        Assert.True(result.IsSuccess);
        Assert.Contains("8.8.8.8", result.Stdout);
    }

    [Fact]
    public void GetDns_ExecutionFails_ReturnsFailureResult()
    {
        // Arrange
        _mockCommandExecutor
            .Setup(x => x.Execute(It.Is<string[]>(args =>
                args.SequenceEqual(new[] { "network", "dns" }))))
            .Returns(new KgsmResult(new ProcessResult(1, string.Empty, "Failed to retrieve DNS servers")));

        // Act
        KgsmResult result = _networkService.GetDns();

        // Assert
        Assert.False(result.IsSuccess);
    }
}
