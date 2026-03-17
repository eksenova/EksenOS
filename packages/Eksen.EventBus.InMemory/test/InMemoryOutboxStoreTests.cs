using Eksen.EventBus.Outbox;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.InMemory.Tests;

public class InMemoryOutboxStoreTests : EksenUnitTestBase
{
    private readonly InMemoryOutboxStore _store = new();

    private static OutboxMessage CreateMessage(
        OutboxMessageStatus status = OutboxMessageStatus.Pending,
        DateTime? creationTime = null)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "TestEvent",
            Payload = "{}",
            CreationTime = creationTime ?? DateTime.UtcNow,
            Status = status
        };
    }

    [Fact]
    public async Task SaveAsync_Should_Store_Message()
    {
        // Arrange
        var msg = CreateMessage();

        // Act
        await _store.SaveAsync(msg);

        // Assert
        var retrieved = await _store.GetByIdAsync(msg.Id);
        retrieved.ShouldNotBeNull();
        retrieved.Id.ShouldBe(msg.Id);
    }

    [Fact]
    public async Task GetByIdAsync_Should_Return_Null_When_Not_Found()
    {
        // Act
        var result = await _store.GetByIdAsync(Guid.NewGuid());

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetPendingAsync_Should_Return_Only_Pending_Messages()
    {
        // Arrange
        var pending1 = CreateMessage(OutboxMessageStatus.Pending);
        var pending2 = CreateMessage(OutboxMessageStatus.Pending);
        var processed = CreateMessage(OutboxMessageStatus.Processed);
        await _store.SaveAsync(pending1);
        await _store.SaveAsync(pending2);
        await _store.SaveAsync(processed);

        // Act
        var result = await _store.GetPendingAsync(10);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(m => m.Status == OutboxMessageStatus.Pending);
    }

    [Fact]
    public async Task GetPendingAsync_Should_Order_By_CreationTime()
    {
        // Arrange
        var older = CreateMessage(creationTime: DateTime.UtcNow.AddMinutes(-5));
        var newer = CreateMessage(creationTime: DateTime.UtcNow);
        await _store.SaveAsync(newer);
        await _store.SaveAsync(older);

        // Act
        var result = await _store.GetPendingAsync(10);

        // Assert
        result[0].Id.ShouldBe(older.Id);
        result[1].Id.ShouldBe(newer.Id);
    }

    [Fact]
    public async Task GetPendingAsync_Should_Respect_BatchSize()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
            await _store.SaveAsync(CreateMessage());

        // Act
        var result = await _store.GetPendingAsync(3);

        // Assert
        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_Should_Update_Status_And_ProcessedTime()
    {
        // Arrange
        var msg = CreateMessage();
        await _store.SaveAsync(msg);

        // Act
        await _store.MarkAsProcessedAsync(msg.Id);

        // Assert
        var updated = await _store.GetByIdAsync(msg.Id);
        updated!.Status.ShouldBe(OutboxMessageStatus.Processed);
        updated.ProcessedTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkAsFailedAsync_Should_Update_Status_And_Error()
    {
        // Arrange
        var msg = CreateMessage();
        await _store.SaveAsync(msg);

        // Act
        await _store.MarkAsFailedAsync(msg.Id, "Something went wrong");

        // Assert
        var updated = await _store.GetByIdAsync(msg.Id);
        updated!.Status.ShouldBe(OutboxMessageStatus.Failed);
        updated.LastError.ShouldBe("Something went wrong");
    }

    [Fact]
    public async Task IncrementRetryCountAsync_Should_Increment()
    {
        // Arrange
        var msg = CreateMessage();
        await _store.SaveAsync(msg);

        // Act
        await _store.IncrementRetryCountAsync(msg.Id);
        await _store.IncrementRetryCountAsync(msg.Id);

        // Assert
        var updated = await _store.GetByIdAsync(msg.Id);
        updated!.RetryCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetStatsAsync_Should_Return_Correct_Counts()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Processing));
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Processed));
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Failed));

        // Act
        var stats = await _store.GetStatsAsync();

        // Assert
        stats.Pending.ShouldBe(2);
        stats.Processing.ShouldBe(1);
        stats.Processed.ShouldBe(1);
        stats.Failed.ShouldBe(1);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Return_All_When_No_Filter()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Processed));

        // Act
        var result = await _store.GetMessagesAsync();

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Filter_By_Status()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Processed));
        await _store.SaveAsync(CreateMessage(OutboxMessageStatus.Processed));

        // Act
        var result = await _store.GetMessagesAsync(OutboxMessageStatus.Processed);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(m => m.Status == OutboxMessageStatus.Processed);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Support_Pagination()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
            await _store.SaveAsync(CreateMessage(creationTime: DateTime.UtcNow.AddMinutes(i)));

        // Act
        var page = await _store.GetMessagesAsync(skip: 2, take: 3);

        // Assert
        page.Count.ShouldBe(3);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_Should_Do_Nothing_When_Not_Found()
    {
        // Act & Assert (should not throw)
        await _store.MarkAsProcessedAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task MarkAsFailedAsync_Should_Do_Nothing_When_Not_Found()
    {
        // Act & Assert (should not throw)
        await _store.MarkAsFailedAsync(Guid.NewGuid(), "error");
    }

    [Fact]
    public async Task IncrementRetryCountAsync_Should_Do_Nothing_When_Not_Found()
    {
        // Act & Assert (should not throw)
        await _store.IncrementRetryCountAsync(Guid.NewGuid());
    }
}
