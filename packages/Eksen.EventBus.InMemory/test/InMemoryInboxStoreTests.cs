using Eksen.EventBus.Inbox;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.InMemory.Tests;

public class InMemoryInboxStoreTests : EksenUnitTestBase
{
    private readonly InMemoryInboxStore _store = new();

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
        var exists = await _store.ExistsAsync(msg.EventId, msg.HandlerType);
        exists.ShouldBeTrue();
    }

    [Fact]
    public async Task ExistsAsync_Should_Return_False_When_Not_Found()
    {
        // Act
        var exists = await _store.ExistsAsync(Guid.NewGuid(), "NonExistent");

        // Assert
        exists.ShouldBeFalse();
    }

    [Fact]
    public async Task ExistsAsync_Should_Match_Both_EventId_And_HandlerType()
    {
        // Arrange
        var eventId = Guid.NewGuid();
        var msg = CreateMessage(eventId: eventId, handlerType: "MyHandler");
        await _store.SaveAsync(msg);

        // Act & Assert
        (await _store.ExistsAsync(eventId, "MyHandler")).ShouldBeTrue();
        (await _store.ExistsAsync(eventId, "OtherHandler")).ShouldBeFalse();
        (await _store.ExistsAsync(Guid.NewGuid(), "MyHandler")).ShouldBeFalse();
    }

    [Fact]
    public async Task GetPendingAsync_Should_Return_Only_Pending_Messages()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Processed));

        // Act
        var result = await _store.GetPendingAsync(10);

        // Assert
        result.Count.ShouldBe(2);
        result.ShouldAllBe(m => m.Status == InboxMessageStatus.Pending);
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
    }

    [Fact]
    public async Task GetPendingAsync_Should_Respect_BatchSize()
    {
        // Arrange
        for (var i = 0; i < 5; i++)
            await _store.SaveAsync(CreateMessage());

        // Act
        var result = await _store.GetPendingAsync(2);

        // Assert
        result.Count.ShouldBe(2);
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
        var messages = await _store.GetMessagesAsync(InboxMessageStatus.Processed);
        messages.Count.ShouldBe(1);
        messages[0].Status.ShouldBe(InboxMessageStatus.Processed);
        messages[0].ProcessedTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task MarkAsFailedAsync_Should_Update_Status_And_Error()
    {
        // Arrange
        var msg = CreateMessage();
        await _store.SaveAsync(msg);

        // Act
        await _store.MarkAsFailedAsync(msg.Id, "Boom!");

        // Assert
        var messages = await _store.GetMessagesAsync(InboxMessageStatus.Failed);
        messages.Count.ShouldBe(1);
        messages[0].LastError.ShouldBe("Boom!");
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
        await _store.IncrementRetryCountAsync(msg.Id);

        // Assert
        var messages = await _store.GetMessagesAsync();
        messages.Single(m => m.Id == msg.Id).RetryCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetStatsAsync_Should_Return_Correct_Counts()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Processing));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Processing));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Processed));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Failed));

        // Act
        var stats = await _store.GetStatsAsync();

        // Assert
        stats.Pending.ShouldBe(1);
        stats.Processing.ShouldBe(2);
        stats.Processed.ShouldBe(1);
        stats.Failed.ShouldBe(1);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Return_All_When_No_Filter()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Processed));

        // Act
        var result = await _store.GetMessagesAsync();

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Filter_By_Status()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Pending));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Processed));
        await _store.SaveAsync(CreateMessage(InboxMessageStatus.Processed));

        // Act
        var result = await _store.GetMessagesAsync(InboxMessageStatus.Processed);

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Support_Pagination()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
            await _store.SaveAsync(CreateMessage(creationTime: DateTime.UtcNow.AddMinutes(i)));

        // Act
        var page = await _store.GetMessagesAsync(skip: 3, take: 4);

        // Assert
        page.Count.ShouldBe(4);
    }

    [Fact]
    public async Task MarkAsProcessedAsync_Should_Do_Nothing_When_Not_Found()
    {
        await _store.MarkAsProcessedAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task MarkAsFailedAsync_Should_Do_Nothing_When_Not_Found()
    {
        await _store.MarkAsFailedAsync(Guid.NewGuid(), "error");
    }
}
