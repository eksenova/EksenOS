using Eksen.TestBase;
using Shouldly;

namespace Eksen.DistributedTransactions.Tests;

public class DistributedUnitOfWorkOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void PostCommitTimeout_Should_Default_To_5_Minutes()
    {
        // Arrange & Act
        var options = new DistributedUnitOfWorkOptions();

        // Assert
        options.PostCommitTimeout.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void PostCommitTimeout_Should_Be_Settable()
    {
        // Arrange
        var options = new DistributedUnitOfWorkOptions();

        // Act
        options.PostCommitTimeout = TimeSpan.FromMinutes(10);

        // Assert
        options.PostCommitTimeout.ShouldBe(TimeSpan.FromMinutes(10));
    }
}
