using Eksen.EventBus.Inbox;
using Shouldly;

namespace Eksen.EventBus.EntityFrameworkCore.Tests;

public class EfCoreInboxStoreTests : EventBusEfCoreTestBase
{
    private EfCoreInboxStore CreateStore() => new(DbContext);

    private static InboxMessage CreateMessage(
        InboxMessageStatus status = InboxMessageStatus.Pending,
        Guid? eventId = null,
        string handlerType = "TestHandler",
        DateTime? creationTime = null)
    {
        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId ?? Guid.NewGuid(),
            EventType = "TestEvent",
            HandlerType = handlerType,
            Payload = "{}",
            CreationTime = creationTime ?? DateTime.UtcNow,
            Status = status,
            CorrelationId = "corr-1",
            SourceApp = "src"
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
        var exists = await store.ExistsAsync(msg.EventId, msg.HandlerType);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_Should_Return_False_When_Not_Found()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var exists = await store.ExistsAsync(Guid.NewGuid(), "NonExistent");

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_Should_Match_EventId_And_HandlerType()
    {
        // Arrange
        var store = CreateStore();
        var eventId = Guid.NewGuid();
        await store.SaveAsync(CreateMessage(eventId: eventId, handlerType: "MyHandler"));

        // Act & Assert
        (await store.ExistsAsync(eventId, "MyHandler")).ShouldBeTrue();
        (await store.ExistsAsync(eventId, "OtherHandler")).ShouldBeFalse();
        (await store.ExistsAsync(Guid.NewGuid(), "MyHandler")).ShouldBeFalse();
    }

    [Fact]
    public async Task GetPendingAsync_Should_Return_Only_Pending()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(InboxMessageStatus.Processed));

        // Act
        var result = await store.GetPendingAsync(10);

        // Assert
        result.Count.ShouldBe(2);
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
    public async Task MarkAsProcessedAsync_Should_Update_Status()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage();
        await store.SaveAsync(msg);

        // Act
        await store.MarkAsProcessedAsync(msg.Id);

        // Assert
        DbContext.ChangeTracker.Clear();
        var messages = await store.GetMessagesAsync(InboxMessageStatus.Processed);
        messages.Count.ShouldBe(1);
        messages[0].ProcessedTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkAsFailedAsync_Should_Update_Status_And_Error()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage();
        await store.SaveAsync(msg);

        // Act
        await store.MarkAsFailedAsync(msg.Id, "Error occurred");

        // Assert
        DbContext.ChangeTracker.Clear();
        var messages = await store.GetMessagesAsync(InboxMessageStatus.Failed);
        messages.Count.ShouldBe(1);
        messages[0].LastError.ShouldBe("Error occurred");
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
        var messages = await store.GetMessagesAsync();
        messages.Single(m => m.Id == msg.Id).RetryCount.ShouldBe(2);
    }

    [Fact]
    public async Task GetStatsAsync_Should_Return_Correct_Counts()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(InboxMessageStatus.Processed));
        await store.SaveAsync(CreateMessage(InboxMessageStatus.Failed));

        // Act
        var stats = await store.GetStatsAsync();

        // Assert
        stats.Pending.ShouldBe(1);
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
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Filter_By_Status()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await store.SaveAsync(CreateMessage(InboxMessageStatus.Processed));

        // Act
        var result = await store.GetMessagesAsync(InboxMessageStatus.Pending);

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
}
