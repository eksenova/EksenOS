using Eksen.EventBus.Inbox;
using Eksen.TestBase;
using Microsoft.Extensions.Options;
using Shouldly;
using StackExchange.Redis;

namespace Eksen.EventBus.Redis.Tests;

public class RedisInboxStoreTests(ContainersFixture containers) : EksenUnitTestBase, IClassFixture<ContainersFixture>, IAsyncLifetime
{
    private ConnectionMultiplexer _connection = null!;

    private static CancellationToken Ct
    {
        get { return TestContext.Current.CancellationToken; }
    }

    public async ValueTask InitializeAsync()
    {
        _connection = await ConnectionMultiplexer.ConnectAsync(containers.RedisConfiguration);
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Claim_Is_Exclusive_Until_Completed_Then_The_Event_Is_A_Duplicate()
    {
        var store = CreateStore("app-" + Guid.NewGuid().ToString("N"));
        var first = CreateMessage(Guid.NewGuid());
        var second = CreateMessage(first.EventId);

        (await store.TryClaimAsync(first, Ct)).Status.ShouldBe(InboxClaimStatus.Claimed);
        var concurrent = await store.TryClaimAsync(second, Ct);
        concurrent.Status.ShouldBe(InboxClaimStatus.InProgress);
        concurrent.Message!.Id.ShouldBe(first.Id);

        await store.CompleteAsync(first, Ct);

        var duplicate = await store.TryClaimAsync(second, Ct);
        duplicate.Status.ShouldBe(InboxClaimStatus.AlreadyProcessed);
        (await store.FindAsync(first.EventId, first.HandlerType, Ct))!.Status.ShouldBe(InboxMessageStatus.Processed);
    }

    [Fact]
    public async Task Keys_Are_Scoped_By_App_And_Handler_And_Expire_After_The_Configured_Windows()
    {
        var appName = "app-" + Guid.NewGuid().ToString("N");
        var store = CreateStore(appName, processingTimeout: TimeSpan.FromMinutes(2), idempotencyWindow: TimeSpan.FromHours(3));
        var message = CreateMessage(Guid.NewGuid());
        var key = $"test:inbox:{appName}:h:{message.HandlerType}:{message.EventId:N}";

        await store.TryClaimAsync(message, Ct);
        var database = _connection.GetDatabase();
        var processingTtl = await database.KeyTimeToLiveAsync(key);
        processingTtl.ShouldNotBeNull();
        processingTtl.Value.ShouldBeInRange(TimeSpan.FromMinutes(1.9), TimeSpan.FromMinutes(2));

        await store.CompleteAsync(message, Ct);
        var processedTtl = await database.KeyTimeToLiveAsync(key);
        processedTtl!.Value.ShouldBeInRange(TimeSpan.FromHours(2.9), TimeSpan.FromHours(3));

        // Another handler, or the same handler in another app, has its own record.
        (await store.TryClaimAsync(CreateMessage(message.EventId, "Other.Handler"), Ct)).Status.ShouldBe(InboxClaimStatus.Claimed);
        (await CreateStore("other-" + appName).TryClaimAsync(CreateMessage(message.EventId), Ct)).Status.ShouldBe(InboxClaimStatus.Claimed);
    }

    [Fact]
    public async Task Released_Claim_Can_Be_Claimed_Again_But_A_Stale_Release_Keeps_The_New_Claim()
    {
        var store = CreateStore("app-" + Guid.NewGuid().ToString("N"));
        var first = CreateMessage(Guid.NewGuid());
        await store.TryClaimAsync(first, Ct);

        await store.ReleaseAsync(first, "handler failed", Ct);

        var second = CreateMessage(first.EventId);
        (await store.TryClaimAsync(second, Ct)).Status.ShouldBe(InboxClaimStatus.Claimed);

        // The first consumer's release arrives late: it must not drop the second consumer's claim.
        await store.ReleaseAsync(first, "late", Ct);
        (await store.TryClaimAsync(CreateMessage(first.EventId), Ct)).Status.ShouldBe(InboxClaimStatus.InProgress);
    }

    [Fact]
    public async Task Expired_Processing_Claim_Becomes_Claimable()
    {
        var store = CreateStore("app-" + Guid.NewGuid().ToString("N"), processingTimeout: TimeSpan.FromMilliseconds(300));
        var abandoned = CreateMessage(Guid.NewGuid());
        await store.TryClaimAsync(abandoned, Ct);

        await Task.Delay(600, TestContext.Current.CancellationToken);

        (await store.TryClaimAsync(CreateMessage(abandoned.EventId), Ct)).Status.ShouldBe(InboxClaimStatus.Claimed);
    }

    [Fact]
    public async Task Stats_And_Listing_Scan_This_Apps_Records()
    {
        var store = CreateStore("app-" + Guid.NewGuid().ToString("N"));
        var processed = CreateMessage(Guid.NewGuid());
        await store.TryClaimAsync(processed, Ct);
        await store.CompleteAsync(processed, Ct);
        await store.TryClaimAsync(CreateMessage(Guid.NewGuid()), Ct);

        var stats = await store.GetStatsAsync(Ct);
        stats.Processed.ShouldBe(1);
        stats.Processing.ShouldBe(1);
        (await store.GetMessagesAsync(InboxMessageStatus.Processed, cancellationToken: Ct)).ShouldHaveSingleItem().EventId.ShouldBe(processed.EventId);
        (await store.DeleteProcessedAsync(DateTime.UtcNow.AddDays(30), 100, Ct)).ShouldBe(0);
    }

    private RedisInboxStore CreateStore(
        string appName,
        TimeSpan? processingTimeout = null,
        TimeSpan? idempotencyWindow = null)
    {
        return new RedisInboxStore(
            _connection,
            Options.Create(new RedisInboxOptions
            {
                KeyPrefix = "test:inbox",
                ProcessingTimeout = processingTimeout ?? TimeSpan.FromMinutes(5)
            }),
            Options.Create(new EksenEventBusOptions
            {
                AppName = appName,
                Inbox = new InboxOptions { IdempotencyWindow = idempotencyWindow ?? TimeSpan.FromDays(7) }
            }));
    }

    private static InboxMessage CreateMessage(Guid eventId, string handlerType = "Orders.Handler")
    {
        return new InboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = eventId,
            EventType = "Orders.Created",
            HandlerType = handlerType,
            Payload = "{}",
            CreationTime = DateTime.UtcNow,
            Status = InboxMessageStatus.Processing
        };
    }
}
