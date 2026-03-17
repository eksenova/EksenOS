using Eksen.TestBase;
using Shouldly;

namespace Eksen.DistributedLocks.Tests;

public class NotAcquiredDistributedLockHandleTests : EksenUnitTestBase
{
    [Fact]
    public void IsAcquired_Should_Be_False()
    {
        // Arrange & Act
        var handle = new NotAcquiredDistributedLockHandle("test-lock");

        // Assert
        handle.IsAcquired.ShouldBeFalse();
    }

    [Fact]
    public void Name_Should_Be_Set_From_Constructor()
    {
        // Arrange & Act
        var handle = new NotAcquiredDistributedLockHandle("my-lock");

        // Assert
        handle.Name.ShouldBe("my-lock");
    }

    [Fact]
    public async Task DisposeAsync_Should_Complete_Without_Error()
    {
        // Arrange
        var handle = new NotAcquiredDistributedLockHandle("test-lock");

        // Act & Assert
        await handle.DisposeAsync();
    }
}
