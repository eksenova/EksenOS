using Eksen.TestBase;
using Shouldly;

namespace Eksen.DistributedTransactions.Tests;

public class DistributedTransactionExceptionTests : EksenUnitTestBase
{
    [Fact]
    public void Constructor_With_Message_Should_Set_Message()
    {
        // Arrange & Act
        var exception = new DistributedTransactionException("test message");

        // Assert
        exception.Message.ShouldBe("test message");
        exception.CompensationExceptions.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_With_Message_And_InnerException_Should_Set_Both()
    {
        // Arrange
        var inner = new InvalidOperationException("inner");

        // Act
        var exception = new DistributedTransactionException("test message", inner);

        // Assert
        exception.Message.ShouldBe("test message");
        exception.InnerException.ShouldBe(inner);
        exception.CompensationExceptions.ShouldBeEmpty();
    }

    [Fact]
    public void Constructor_With_CompensationExceptions_Should_Store_Them()
    {
        // Arrange
        var inner = new InvalidOperationException("step failed");
        var compensationErrors = new List<Exception>
        {
            new Exception("comp1"),
            new Exception("comp2")
        };

        // Act
        var exception = new DistributedTransactionException(
            "test message", inner, compensationErrors);

        // Assert
        exception.CompensationExceptions.Count.ShouldBe(2);
        exception.InnerException.ShouldBe(inner);
    }

    [Fact]
    public void Constructor_With_CompensationExceptions_Without_Inner_Should_Work()
    {
        // Arrange
        var compensationErrors = new List<Exception> { new Exception("comp") };

        // Act
        var exception = new DistributedTransactionException(
            "test message", compensationErrors);

        // Assert
        exception.Message.ShouldBe("test message");
        exception.CompensationExceptions.Count.ShouldBe(1);
        exception.InnerException.ShouldBeNull();
    }
}
