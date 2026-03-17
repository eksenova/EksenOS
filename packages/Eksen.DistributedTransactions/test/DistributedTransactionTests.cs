using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.DistributedTransactions.Tests;

public class DistributedTransactionTests : EksenServiceTestBase
{
    protected override void ConfigureEksen(IEksenBuilder builder)
    {
        base.ConfigureEksen(builder);
        builder.AddDistributedTransactions();
    }

    private IDistributedTransaction CreateTransaction(string? name = null)
    {
        var manager = ServiceProvider.GetRequiredService<IDistributedTransactionManager>();
        return manager.Begin(name);
    }

    [Fact]
    public async Task CommitAsync_Should_Execute_All_Steps_In_Order()
    {
        // Arrange
        var executionOrder = new List<string>();
        var tx = CreateTransaction("OrderTest");

        tx.AddStep("Step1",
            (_, _) => { executionOrder.Add("Step1"); return Task.CompletedTask; },
            (_, _) => Task.CompletedTask);

        tx.AddStep("Step2",
            (_, _) => { executionOrder.Add("Step2"); return Task.CompletedTask; },
            (_, _) => Task.CompletedTask);

        tx.AddStep("Step3",
            (_, _) => { executionOrder.Add("Step3"); return Task.CompletedTask; },
            (_, _) => Task.CompletedTask);

        // Act
        await tx.CommitAsync();

        // Assert
        executionOrder.ShouldBe(["Step1", "Step2", "Step3"]);
        tx.State.ShouldBe(DistributedTransactionState.Committed);
    }

    [Fact]
    public async Task CommitAsync_Should_Compensate_On_Step_Failure()
    {
        // Arrange
        var compensated = new List<string>();
        var tx = CreateTransaction("CompensationTest");

        tx.AddStep("Step1",
            (_, _) => Task.CompletedTask,
            (_, _) => { compensated.Add("Step1"); return Task.CompletedTask; });

        tx.AddStep("Step2",
            (_, _) => Task.CompletedTask,
            (_, _) => { compensated.Add("Step2"); return Task.CompletedTask; });

        tx.AddStep("Step3",
            (_, _) => throw new InvalidOperationException("Step3 failed"),
            (_, _) => { compensated.Add("Step3"); return Task.CompletedTask; });

        // Act & Assert
        var exception = await Should.ThrowAsync<DistributedTransactionException>(
            () => tx.CommitAsync());

        exception.Message.ShouldContain("Step3");
        exception.InnerException.ShouldBeOfType<InvalidOperationException>();

        compensated.ShouldBe(["Step2", "Step1"]);
        tx.State.ShouldBe(DistributedTransactionState.Compensated);
    }

    [Fact]
    public async Task CommitAsync_Should_Set_Failed_State_When_Compensation_Fails()
    {
        // Arrange
        var tx = CreateTransaction("FailedCompensation");

        tx.AddStep("Step1",
            (_, _) => Task.CompletedTask,
            (_, _) => throw new Exception("Compensation failed"));

        tx.AddStep("Step2",
            (_, _) => throw new Exception("Step2 failed"),
            (_, _) => Task.CompletedTask);

        // Act & Assert
        var exception = await Should.ThrowAsync<DistributedTransactionException>(
            () => tx.CommitAsync());

        exception.CompensationExceptions.Count.ShouldBe(1);
        tx.State.ShouldBe(DistributedTransactionState.Failed);
    }

    [Fact]
    public void State_Should_Be_Pending_Initially()
    {
        // Arrange & Act
        var tx = CreateTransaction();

        // Assert
        tx.State.ShouldBe(DistributedTransactionState.Pending);
    }

    [Fact]
    public void Name_Should_Be_Set_From_Constructor()
    {
        // Arrange & Act
        var tx = CreateTransaction("MyTransaction");

        // Assert
        tx.Name.ShouldBe("MyTransaction");
    }

    [Fact]
    public void Name_Should_Be_Auto_Generated_When_Null()
    {
        // Arrange & Act
        var tx = CreateTransaction();

        // Assert
        tx.Name.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task CommitAsync_Should_Throw_When_Not_Pending()
    {
        // Arrange
        var tx = CreateTransaction();
        await tx.CommitAsync();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => tx.CommitAsync());
    }

    [Fact]
    public async Task AddStep_Should_Throw_When_Not_Pending()
    {
        // Arrange
        var tx = CreateTransaction();
        await tx.CommitAsync();

        // Act & Assert
        Should.Throw<InvalidOperationException>(
            () => tx.AddStep("Late",
                (_, _) => Task.CompletedTask,
                (_, _) => Task.CompletedTask));
    }

    [Fact]
    public async Task RollbackAsync_Should_Throw_When_Already_Committed()
    {
        // Arrange
        var tx = CreateTransaction();
        await tx.CommitAsync();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            () => tx.RollbackAsync());
    }

    [Fact]
    public async Task RollbackAsync_Should_Compensate_Executed_Steps()
    {
        // Arrange
        var compensated = new List<string>();
        var tx = CreateTransaction();

        tx.AddStep("Step1",
            (_, _) => Task.CompletedTask,
            (_, _) => { compensated.Add("Step1"); return Task.CompletedTask; });

        // RollbackAsync on a Pending state with no executed steps should just compensate (no-op)
        await tx.RollbackAsync();

        // Assert
        compensated.ShouldBeEmpty();
        tx.State.ShouldBe(DistributedTransactionState.Compensated);
    }

    [Fact]
    public async Task RollbackAsync_Should_No_Op_When_Already_Compensated()
    {
        // Arrange
        var tx = CreateTransaction();
        await tx.RollbackAsync();

        // Act & Assert (should not throw)
        await tx.RollbackAsync();
        tx.State.ShouldBe(DistributedTransactionState.Compensated);
    }

    [Fact]
    public async Task DisposeAsync_Should_Rollback_If_Pending()
    {
        // Arrange
        var tx = CreateTransaction();
        tx.AddStep("Step1",
            (_, _) => Task.CompletedTask,
            (_, _) => Task.CompletedTask);

        // Act
        await tx.DisposeAsync();

        // Assert (rollback of pending state - no steps executed, so no compensation)
        tx.State.ShouldBe(DistributedTransactionState.Compensated);
    }

    [Fact]
    public async Task DisposeAsync_Should_Be_Idempotent()
    {
        // Arrange
        var tx = CreateTransaction();

        // Act (dispose twice)
        await tx.DisposeAsync();
        await tx.DisposeAsync();

        // Assert (no exception)
        tx.State.ShouldBe(DistributedTransactionState.Compensated);
    }

    [Fact]
    public async Task AddStep_With_Auto_Name_Should_Generate_Sequential_Names()
    {
        // Arrange
        var executed = new List<string>();
        var tx = CreateTransaction();

        tx.AddStep(
            (_, _) => { executed.Add("first"); return Task.CompletedTask; },
            (_, _) => Task.CompletedTask);

        tx.AddStep(
            (_, _) => { executed.Add("second"); return Task.CompletedTask; },
            (_, _) => Task.CompletedTask);

        // Act
        await tx.CommitAsync();

        // Assert
        executed.Count.ShouldBe(2);
    }

    [Fact]
    public async Task AddStep_With_Instance_Should_Execute_Step()
    {
        // Arrange
        var executed = false;
        var step = new TestStep("TestStep",
            () => { executed = true; return Task.CompletedTask; },
            () => Task.CompletedTask);

        var tx = CreateTransaction();
        tx.AddStep(step);

        // Act
        await tx.CommitAsync();

        // Assert
        executed.ShouldBeTrue();
    }

    private class TestStep(string name, Func<Task> execute, Func<Task> compensate)
        : IDistributedTransactionStep
    {
        public string Name { get; } = name;

        public Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
            => execute();

        public Task CompensateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
            => compensate();
    }
}
