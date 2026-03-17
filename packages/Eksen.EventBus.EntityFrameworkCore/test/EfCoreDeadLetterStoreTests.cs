using Eksen.EventBus.DeadLetter;
using Shouldly;

namespace Eksen.EventBus.EntityFrameworkCore.Tests;

public class EfCoreDeadLetterStoreTests : EventBusEfCoreTestBase
{
    private EfCoreDeadLetterStore CreateStore() => new(DbContext);

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
            LastError = "Test error",
            CorrelationId = "corr-1",
            SourceApp = "src",
            TargetApp = "tgt"
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
    public async Task GetMessagesAsync_Should_Return_All_Messages()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage());
        await store.SaveAsync(CreateMessage());

        // Act
        var result = await store.GetMessagesAsync();

        // Assert
        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Order_By_FailedTime_Desc()
    {
        // Arrange
        var store = CreateStore();
        var older = CreateMessage(failedTime: DateTime.UtcNow.AddMinutes(-10));
        var newer = CreateMessage(failedTime: DateTime.UtcNow);
        await store.SaveAsync(older);
        await store.SaveAsync(newer);

        // Act
        var result = await store.GetMessagesAsync();

        // Assert
        result[0].Id.ShouldBe(newer.Id);
    }

    [Fact]
    public async Task GetMessagesAsync_Should_Support_Pagination()
    {
        // Arrange
        var store = CreateStore();
        for (var i = 0; i < 10; i++)
            await store.SaveAsync(CreateMessage(failedTime: DateTime.UtcNow.AddMinutes(i)));

        // Act
        var page = await store.GetMessagesAsync(skip: 2, take: 3);

        // Assert
        page.Count.ShouldBe(3);
    }

    [Fact]
    public async Task RequeueAsync_Should_Mark_As_Requeued()
    {
        // Arrange
        var store = CreateStore();
        var msg = CreateMessage();
        await store.SaveAsync(msg);

        // Act
        await store.RequeueAsync(msg.Id);

        // Assert
        DbContext.ChangeTracker.Clear();
        var updated = await store.GetByIdAsync(msg.Id);
        updated!.IsRequeued.ShouldBeTrue();
        updated.RequeuedTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task GetCountAsync_Should_Exclude_Requeued_Messages()
    {
        // Arrange
        var store = CreateStore();
        await store.SaveAsync(CreateMessage());
        await store.SaveAsync(CreateMessage());
        var toRequeue = CreateMessage();
        await store.SaveAsync(toRequeue);
        await store.RequeueAsync(toRequeue.Id);

        // Act
        var count = await store.GetCountAsync();

        // Assert
        count.ShouldBe(2);
    }

    [Fact]
    public async Task GetCountAsync_Should_Return_Zero_When_Empty()
    {
        // Arrange
        var store = CreateStore();

        // Act
        var count = await store.GetCountAsync();

        // Assert
        count.ShouldBe(0);
    }

    [Fact]
    public async Task SaveAsync_Should_Persist_All_Fields()
    {
        // Arrange
        var store = CreateStore();
        var msg = new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            OriginalMessageId = Guid.NewGuid(),
            EventType = "MyEvent",
            HandlerType = "MyHandler",
            Payload = "{\"data\":1}",
            CreationTime = DateTime.UtcNow.AddMinutes(-5),
            FailedTime = DateTime.UtcNow,
            TotalRetryCount = 7,
            LastError = "Some error",
            CorrelationId = "correlation-42",
            SourceApp = "AppA",
            TargetApp = "AppB"
        };

        // Act
        await store.SaveAsync(msg);

        // Assert
        var retrieved = await store.GetByIdAsync(msg.Id);
        retrieved.ShouldNotBeNull();
        retrieved.OriginalMessageId.ShouldBe(msg.OriginalMessageId);
        retrieved.EventType.ShouldBe("MyEvent");
        retrieved.HandlerType.ShouldBe("MyHandler");
        retrieved.Payload.ShouldBe("{\"data\":1}");
        retrieved.TotalRetryCount.ShouldBe(7);
        retrieved.LastError.ShouldBe("Some error");
        retrieved.CorrelationId.ShouldBe("correlation-42");
        retrieved.SourceApp.ShouldBe("AppA");
        retrieved.TargetApp.ShouldBe("AppB");
        retrieved.IsRequeued.ShouldBeFalse();
    }
}
