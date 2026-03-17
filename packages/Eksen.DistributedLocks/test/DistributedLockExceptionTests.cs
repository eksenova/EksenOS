using Eksen.TestBase;
using Shouldly;

namespace Eksen.DistributedLocks.Tests;

public class DistributedLockExceptionTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_With_Message_Should_Set_Message()
    {
        // Arrange & Act
        var exception = new DistributedLockException("lock failed");

        // Assert
        exception.Message.ShouldBe("lock failed");
        exception.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_With_Message_And_InnerException_Should_Set_Both()
    {
        // Arrange
        var inner = new TimeoutException("timed out");

        // Act
        var exception = new DistributedLockException("lock failed", inner);

        // Assert
        exception.Message.ShouldBe("lock failed");
        exception.InnerException.ShouldBe(inner);
    }
}
