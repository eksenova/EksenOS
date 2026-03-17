using Eksen.EventBus.DeadLetter;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.InMemory.Tests;

public class InMemoryDeadLetterStoreTests : EksenUnitTestBase
{
    private readonly InMemoryDeadLetterStore _store = new();

    private static DeadLetterMessage CreateMessage(DateTime? failedTime = null)
    {
        return new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            OriginalMessageId = Guid.NewGuid(),
            EventType = "TestEvent",
            HandlerType = "TestHandler",
            Payload = "{}",
            CreationTime = DateTime.UtcNow,
            FailedTime = failedTime ?? DateTime.UtcNow,
            TotalRetryCount = 3,
            LastError = "Test error"
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
    public async Task GetMessagesAsync_Should_Return_All_Messages()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage());
        await _store.SaveAsync(CreateMessage());
        await _store.SaveAsync(CreateMessage());

        // Act
        var result = await _store.GetMessagesAsync();

        // Assert
        result.Count.ShouldBe(3);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Order_By_FailedTime_Descending()
    {
        // Arrange
        var older = CreateMessage(failedTime: DateTime.UtcNow.AddMinutes(-10));
        var newer = CreateMessage(failedTime: DateTime.UtcNow);
        await _store.SaveAsync(older);
        await _store.SaveAsync(newer);

        // Act
        var result = await _store.GetMessagesAsync();

        // Assert
        result[0].Id.ShouldBe(newer.Id);
        result[1].Id.ShouldBe(older.Id);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Support_Pagination()
    {
        // Arrange
        for (var i = 0; i < 10; i++)
            await _store.SaveAsync(CreateMessage(failedTime: DateTime.UtcNow.AddMinutes(i)));

        // Act
        var page = await _store.GetMessagesAsync(skip: 2, take: 3);

        // Assert
        page.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RequeueAsync_Should_Mark_As_Requeued()
    {
        // Arrange
        var msg = CreateMessage();
        await _store.SaveAsync(msg);

        // Act
        await _store.RequeueAsync(msg.Id);

        // Assert
        var updated = await _store.GetByIdAsync(msg.Id);
        updated!.IsRequeued.ShouldBeTrue();
        updated.RequeuedTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task RequeueAsync_Should_Do_Nothing_When_Not_Found()
    {
        // Act & Assert (should not throw)
        await _store.RequeueAsync(Guid.NewGuid());
    }

    [Fact]
    public async Task GetCountAsync_Should_Return_Total_Count()
    {
        // Arrange
        await _store.SaveAsync(CreateMessage());
        await _store.SaveAsync(CreateMessage());

        // Act
        var count = await _store.GetCountAsync();

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetCountAsync_Should_Return_Zero_When_Empty()
    {
        // Act
        var count = await _store.GetCountAsync();

        // Assert
        count.ShouldBe(0);
    }
}
