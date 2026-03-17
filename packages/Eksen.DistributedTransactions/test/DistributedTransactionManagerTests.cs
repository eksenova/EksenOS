using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.DistributedTransactions.Tests;

public class DistributedTransactionManagerTests : EksenServiceTestBase
{
    protected override void ConfigureEksen(IEksenBuilder builder)
    {
        base.ConfigureEksen(builder);
        builder.AddDistributedTransactions();
    }

    [Fact]
    public void Begin_Should_Return_Transaction_With_Custom_Name()
    {
        // Arrange
        var manager = ServiceProvider.GetRequiredService<IDistributedTransactionManager>();

        // Act
        var tx = manager.Begin("MyTx");

        // Assert
        tx.ShouldNotBeNull();
        tx.Name.ShouldBe("MyTx");
        tx.State.ShouldBe(DistributedTransactionState.Pending);
    }

    [Fact]
    public void Begin_Should_Generate_Name_When_Null()
    {
        // Arrange
        var manager = ServiceProvider.GetRequiredService<IDistributedTransactionManager>();

        // Act
        var tx = manager.Begin();

        // Assert
        tx.ShouldNotBeNull();
        tx.Name.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Begin_Should_Return_Unique_Transactions()
    {
        // Arrange
        var manager = ServiceProvider.GetRequiredService<IDistributedTransactionManager>();

        // Act
        var tx1 = manager.Begin();
        var tx2 = manager.Begin();

        // Assert
        tx1.Name.ShouldNotBe(tx2.Name);
    }
}
