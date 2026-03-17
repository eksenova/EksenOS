using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Shouldly;

namespace Eksen.DistributedLocks.PostgreSql.Tests;

public class PostgreSqlDistributedLockOptionsTests : EksenUnitTestBase
{
    [Fact]
    public void DefaultConfigSectionPath_Should_Be_Correct()
    {
        // Arrange & Act & Assert
        PostgreSqlDistributedLockOptions.DefaultConfigSectionPath
            .ShouldBe("Eksen:DistributedLocks:PostgreSql");
    }

    [Fact]
    public void ConnectionString_Should_Default_To_Empty()
    {
        // Arrange & Act
        var options = new PostgreSqlDistributedLockOptions();

        // Assert
        options.ConnectionString.ShouldBe(string.Empty);
    }

    [Fact]
    public void ConnectionString_Should_Be_Settable()
    {
        // Arrange
        var options = new PostgreSqlDistributedLockOptions();

        // Act
        options.ConnectionString = "Host=localhost;Database=test";

        // Assert
        options.ConnectionString.ShouldBe("Host=localhost;Database=test");
    }
}
