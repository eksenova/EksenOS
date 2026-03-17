using Eksen.TestBase;
using Shouldly;

namespace Eksen.DistributedLocks.SqlServer.Tests;

public class SqlServerDistributedLockOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void DefaultConfigSectionPath_Should_Be_Correct()
    {
        // Arrange & Act & Assert
        SqlServerDistributedLockOptions.DefaultConfigSectionPath
            .ShouldBe("Eksen:DistributedLocks:SqlServer");
    }

    [Fact]
    public void ConnectionString_Should_Default_To_Empty()
    {
        // Arrange & Act
        var options = new SqlServerDistributedLockOptions();

        // Assert
        options.ConnectionString.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConnectionString_Should_Be_Settable()
    {
        // Arrange
        var options = new SqlServerDistributedLockOptions();

        // Act
        options.ConnectionString = "Server=localhost;Database=test";

        // Assert
        options.ConnectionString.ShouldBe("Server=localhost;Database=test");
    }
}
