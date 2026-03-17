using Eksen.TestBase;
using Shouldly;

namespace Eksen.DistributedLocks.Tests;

public class EksenDistributedLockOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void DefaultTimeout_Should_Be_Null_By_Default()
    {
        // Arrange & Act
        var options = new EksenDistributedLockOptions();

        // Assert
        options.DefaultTimeout.ShouldBeNull();
    }

    [Fact]
    public void DefaultTimeout_Should_Be_Settable()
    {
        // Arrange
        var options = new EksenDistributedLockOptions();

        // Act
        options.DefaultTimeout = TimeSpan.FromSeconds(30);

        // Assert
        options.DefaultTimeout.ShouldBe(TimeSpan.FromSeconds(30));
    }
}
