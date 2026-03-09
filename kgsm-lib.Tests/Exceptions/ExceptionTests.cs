namespace TheKrystalShip.KGSM.Tests.Exceptions;

/// <summary>
/// Tests for the KgsmException hierarchy.
/// </summary>
public class ExceptionTests
{
    [Fact]
    public void KgsmException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new KgsmException();

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<KgsmException>(exception);
    }

    [Fact]
    public void KgsmException_MessageConstructor_SetsMessage()
    {
        // Arrange
        const string message = "Test error message";

        // Act
        var exception = new KgsmException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void KgsmException_InnerExceptionConstructor_SetsInnerException()
    {
        // Arrange
        const string message = "Outer exception";
        var innerException = new InvalidOperationException("Inner exception");

        // Act
        var exception = new KgsmException(message, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void BlueprintException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new BlueprintException();

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<BlueprintException>(exception);
        Assert.Null(exception.BlueprintName);
    }

    [Fact]
    public void BlueprintException_MessageConstructor_SetsMessage()
    {
        // Arrange
        const string message = "Blueprint error";

        // Act
        var exception = new BlueprintException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void BlueprintException_MessageAndNameConstructor_SetsBoth()
    {
        // Arrange
        const string message = "Blueprint not found";
        const string blueprintName = "valheim";

        // Act
        var exception = new BlueprintException(message, blueprintName);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(blueprintName, exception.BlueprintName);
    }

    [Fact]
    public void BlueprintException_FullConstructor_SetsAllProperties()
    {
        // Arrange
        const string message = "Blueprint error";
        const string blueprintName = "minecraft";
        var innerException = new InvalidOperationException("Inner");

        // Act
        var exception = new BlueprintException(message, blueprintName, innerException);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(blueprintName, exception.BlueprintName);
        Assert.Same(innerException, exception.InnerException);
    }

    [Fact]
    public void BlueprintException_IsKgsmException()
    {
        // Act
        var exception = new BlueprintException("test");

        // Assert
        Assert.IsAssignableFrom<KgsmException>(exception);
    }

    [Fact]
    public void InstanceException_DefaultConstructor_CreatesInstance()
    {
        // Act
        var exception = new InstanceException();

        // Assert
        Assert.NotNull(exception);
        Assert.IsType<InstanceException>(exception);
        Assert.Null(exception.InstanceName);
    }

    [Fact]
    public void InstanceException_MessageConstructor_SetsMessage()
    {
        // Arrange
        const string message = "Instance error";

        // Act
        var exception = new InstanceException(message);

        // Assert
        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void InstanceException_MessageAndNameConstructor_SetsBoth()
    {
        // Arrange
        const string message = "Instance not found";
        const string instanceName = "my-server";

        // Act
        var exception = new InstanceException(message, instanceName);

        // Assert
        Assert.Equal(message, exception.Message);
        Assert.Equal(instanceName, exception.InstanceName);
    }

    [Fact]
    public void InstanceException_IsKgsmException()
    {
        // Act
        var exception = new InstanceException("test");

        // Assert
        Assert.IsAssignableFrom<KgsmException>(exception);
    }

    [Fact]
    public void KgsmException_CanBeCaught()
    {
        // Arrange
        bool caught = false;

        // Act
        try
        {
            throw new KgsmException("Test exception");
        }
        catch (KgsmException)
        {
            caught = true;
        }

        // Assert
        Assert.True(caught);
    }

    [Fact]
    public void BlueprintException_CanBeCaughtAsKgsmException()
    {
        // Arrange
        bool caught = false;

        // Act
        try
        {
            throw new BlueprintException("Test", "blueprint-name");
        }
        catch (KgsmException ex)
        {
            caught = true;
            Assert.IsType<BlueprintException>(ex);
        }

        // Assert
        Assert.True(caught);
    }

    [Fact]
    public void InstanceException_CanBeCaughtAsKgsmException()
    {
        // Arrange
        bool caught = false;

        // Act
        try
        {
            throw new InstanceException("Test", "instance-name");
        }
        catch (KgsmException ex)
        {
            caught = true;
            Assert.IsType<InstanceException>(ex);
        }

        // Assert
        Assert.True(caught);
    }
}
