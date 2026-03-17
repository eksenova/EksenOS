using Eksen.TestBase;
using Shouldly;

namespace Eksen.DistributedTransactions.Tests;

public class DistributedTransactionStateTests : EksenUnitTestBase
{
    [Fact]
    public void Should_Have_All_Expected_Values()
    {
        // Arrange & Act
        var values = Enum.GetValues<DistributedTransactionState>();

        // Assert
        values.ShouldContain(DistributedTransactionState.Pending);
        values.ShouldContain(DistributedTransactionState.Executing);
        values.ShouldContain(DistributedTransactionState.Committed);
        values.ShouldContain(DistributedTransactionState.Compensating);
        values.ShouldContain(DistributedTransactionState.Compensated);
        values.ShouldContain(DistributedTransactionState.Failed);
        values.Length.ShouldBe(6);
    }
}
