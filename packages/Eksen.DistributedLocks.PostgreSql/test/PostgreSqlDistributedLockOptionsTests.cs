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

    [Theory]
    [InlineData("order:123", "order")]
    [InlineData("order:456", "order")]
    [InlineData("merchant-wallet/8f2c", "merchant-wallet")]
    [InlineData("payout|2026-11", "payout")]
    [InlineData("nightly-report", "nightly-report")]
    [InlineData(":123", "other")]
    public void Default_Metric_Lock_Name_Keeps_The_Prefix(string name, string expected)
    {
        PostgreSqlDistributedLockOptions.DefaultMetricLockName(name).ShouldBe(expected);
        new PostgreSqlDistributedLockOptions().ResolveMetricLockName(name).ShouldBe(expected);
    }

    [Fact]
    public void Metric_Lock_Name_Selector_Replaces_The_Default()
    {
        var options = new PostgreSqlDistributedLockOptions { MetricLockNameSelector = name => name.StartsWith("order") ? "orders" : "" };

        options.ResolveMetricLockName("order:123").ShouldBe("orders");
        options.ResolveMetricLockName("invoice:1").ShouldBe("other");
    }
}
