using Eksen.EventBus.Outbox;
using Shouldly;

namespace Eksen.EventBus.EntityFrameworkCore.Tests;

public class EfCoreOutboxStoreTests : EventBusEfCoreTestBase
{
    private EfCoreOutboxStore CreateStore() => new(DbContext);

    private static OutboxMessage CreateMessage(
        OutboxMessageStatus status = OutboxMessageStatus.Pending,
        DateTime? creationTime = null,
        Dictionary<string, string>? headers = null)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "TestEvent",
            Payload = "{}",
            CreationTime = creationTime ?? DateTime.UtcNow,
            Status = status,
            CorrelationId = "corr-1",
            SourceApp = "src",
            TargetApp = "tgt",
            Headers = headers
        };
    }

    [Fact]
    public async Task SaveAsync_Should_Persist_Message()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage();

        // Act
        await store.SaveAsync(msg);

        // Assert
        var retrieved = await store.GetByIdAsync(msg.Id);
        retrieved.ShouldNotBeNull();
        retrieved.EventType.ShouldBe("TestEvent");
        retrieved.CorrelationId.ShouldBe("corr-1");
    }

    [Fact]
    public async Task SaveAsync_Should_Serialize_Headers()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage(headers: new Dictionary<string, string> { ["key"] = "value" });

        // Act
        await store.SaveAsync(msg);

        // Assert
        var retrieved = await store.GetByIdAsync(msg.Id);
        retrieved!.Headers.ShouldNotBeNull();
        retrieved.Headers["key"].ShouldBe("value");
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var result = await store.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPendingAsync_Should_Return_Only_Pending()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Processed));

        // Act
        var result = await store.GetPendingAsync(10);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(m => m.Status == OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_Should_Order_By_CreationTime()
    {
        // Arrange
        var store = CreateStore();
        var older = CreateMessage(creationTime: DateTime.UtcNow.AddMinutes(-5));
        var newer = CreateMessage(creationTime: DateTime.UtcNow);
        await store.SaveAsync(newer);
        await store.SaveAsync(older);

        // Act
        var result = await store.GetPendingAsync(10);

        // Assert
        result[0].Id.ShouldBe(older.Id);
    }

    [Fact]
    public async Task GetPendingAsync_Should_Respect_BatchSize()
    {
        // Arrange
        var store = CreateStore();
        for (var i = 0; i < 5; i++)
            await store.SaveAsync(CreateMessage());

        // Act
        var result = await store.GetPendingAsync(2);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_Should_Update_Status()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage();
        await store.SaveAsync(msg);

        // Act
        await store.MarkAsProcessedAsync(msg.Id);

        // Assert - detach and refetch to see the update
        DbContext.ChangeTracker.Clear();
        var updated = await store.GetByIdAsync(msg.Id);
        updated!.Status.ShouldBe(OutboxMessageStatus.Processed);
        updated.ProcessedTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkAsFailedAsync_Should_Update_Status_And_Error()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage();
        await store.SaveAsync(msg);

        // Act
        await store.MarkAsFailedAsync(msg.Id, "Failed!");

        // Assert
        DbContext.ChangeTracker.Clear();
        var updated = await store.GetByIdAsync(msg.Id);
        updated!.Status.ShouldBe(OutboxMessageStatus.Failed);
        updated.LastError.ShouldBe("Failed!");
    }

    [Fact]
    public async Task IncrementRetryCountAsync_Should_Increment()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage();
        await store.SaveAsync(msg);

        // Act
        await store.IncrementRetryCountAsync(msg.Id);
        await store.IncrementRetryCountAsync(msg.Id);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updated = await store.GetByIdAsync(msg.Id);
        updated!.RetryCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetStatsAsync_Should_Return_Correct_Counts()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Processed));
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Failed));

        // Act
        var stats = await store.GetStatsAsync();

        // Assert
        stats.Pending.ShouldBe(2);
        stats.Processed.ShouldBe(1);
        stats.Failed.ShouldBe(1);
    }

    [Fact]
    public async Task GetStatsAsync_Should_Return_Zeros_When_Empty()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var stats = await store.GetStatsAsync();

        // Assert
        stats.Pending.ShouldBe(0);
        stats.Processing.ShouldBe(0);
        stats.Processed.ShouldBe(0);
        stats.Failed.ShouldBe(0);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Return_All_When_No_Filter()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Processed));

        // Act
        var result = await store.GetMessagesAsync();

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Filter_By_Status()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(OutboxMessageStatus.Processed));

        // Act
        var result = await store.GetMessagesAsync(OutboxMessageStatus.Pending);

        // Assert
        result.Count.ShouldBe(1);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Support_Pagination()
    {
        // Arrange
        var store = CreateStore();
        for (var i = 0; i < 10; i++)
            await store.SaveAsync(CreateMessage(creationTime: DateTime.UtcNow.AddMinutes(i)));

        // Act
        var page = await store.GetMessagesAsync(skip: 2, take: 3);

        // Assert
        page.Count.ShouldBe(3);
    }

    [Fact]
    public async Task SaveAsync_Should_Handle_Null_Headers()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage(headers: null);

        // Act
        await store.SaveAsync(msg);

        // Assert
        var retrieved = await store.GetByIdAsync(msg.Id);
        retrieved!.Headers.ShouldBeNull();
    }
}
