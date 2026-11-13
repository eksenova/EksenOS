using System.Collections.Concurrent;
using Eksen.EventBus.BackgroundServices;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.EventBus.EntityFrameworkCore.Tests;

/// <summary>
/// Outbox leasing on a database file shared by independent publishers, as several hosts share one table.
/// </summary>
public class EfCoreOutboxClaimTests : IAsyncLifetime
{
    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"eksen-outbox-{Guid.NewGuid():N}.db");

    public async ValueTask InitializeAsync()
    {
        await using var context = CreateContext();
        await context.Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await using (var context = CreateContext())
        {
            await context.Database.EnsureDeletedAsync();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
    }

    [Fact]
    public async Task Two_Pollers_Publish_Every_Row_Exactly_Once()
    {
        var eventIds = new List<Guid>();
        await using (var context = CreateContext())
        {
            var store = new EfCoreOutboxStore(context);
            for (var i = 0; i < 60; i++)
            {
                var message = CreateMessage(DateTime.UtcNow.AddSeconds(i));
                eventIds.Add(message.EventId);
                await store.SaveAsync(message, cancellationToken: TestContext.Current.CancellationToken);
            }
        }

        var published = new ConcurrentDictionary<Guid, int>();
        var transport = new Mock<IEventBusTransport>();
        transport.Setup(x => x.PublishAsync(
                It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(), It.IsAny<string?>(),
                It.IsAny<Guid>(), It.IsAny<Dictionary<string, string>?>(), It.IsAny<CancellationToken>()))
            .Returns<string, string, string?, string?, string?, Guid, Dictionary<string, string>?, CancellationToken>(
                async (_, _, _, _, _, eventId, _, _) =>
                {
                    published.AddOrUpdate(eventId, 1, (_, count) => count + 1);
                    await Task.Delay(2);
                });

        var options = Options.Create(new EksenEventBusOptions
        {
            Outbox = new OutboxOptions { BatchSize = 7, PollingInterval = TimeSpan.FromMilliseconds(10) }
        });
        await using var firstHost = CreateHost();
        await using var secondHost = CreateHost();
        var first = CreateProcessor(firstHost, transport.Object, options);
        var second = CreateProcessor(secondHost, transport.Object, options);

        await first.StartAsync(CancellationToken.None);
        await second.StartAsync(CancellationToken.None);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
        {
            while (await CountProcessedAsync() < eventIds.Count)
            {
                await Task.Delay(20, timeout.Token);
            }
        }

        await first.StopAsync(CancellationToken.None);
        await second.StopAsync(CancellationToken.None);

        published.Keys.ShouldBe(eventIds, ignoreOrder: true);
        published.Values.ShouldAllBe(count => count == 1);
    }

    [Fact]
    public async Task Concurrent_Claims_Are_Disjoint()
    {
        await using (var context = CreateContext())
        {
            var store = new EfCoreOutboxStore(context);
            for (var i = 0; i < 30; i++)
            {
                await store.SaveAsync(CreateMessage(DateTime.UtcNow.AddSeconds(i)), cancellationToken: TestContext.Current.CancellationToken);
            }
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 4).Select(async _ =>
        {
            await using var context = CreateContext();
            return await new EfCoreOutboxStore(context).ClaimAsync(10, TimeSpan.FromMinutes(1));
        }));

        var claimed = claims.SelectMany(batch => batch.Select(message => message.Id)).ToList();
        claimed.Count.ShouldBe(30);
        claimed.Distinct().Count().ShouldBe(30);
        claims.ShouldAllBe(batch => batch.All(message =>
            message.Status == OutboxMessageStatus.Processing && message.LeaseToken != null && message.LeaseExpiresAt != null));
    }

    [Fact]
    public async Task Claim_Honours_Next_Attempt_Time_And_Lease_Expiry()
    {
        await using var context = CreateContext();
        var store = new EfCoreOutboxStore(context);
        var due = CreateMessage(DateTime.UtcNow);
        var backedOff = CreateMessage(DateTime.UtcNow);
        await store.SaveAsync(due, cancellationToken: TestContext.Current.CancellationToken);
        await store.SaveAsync(backedOff, cancellationToken: TestContext.Current.CancellationToken);
        await store.ScheduleRetryAsync(backedOff.Id, DateTime.UtcNow.AddMinutes(5), "broker down", cancellationToken: TestContext.Current.CancellationToken);

        var firstClaim = await store.ClaimAsync(10, TimeSpan.FromMilliseconds(200), cancellationToken: TestContext.Current.CancellationToken);
        firstClaim.Select(message => message.Id).ShouldBe([due.Id]);
        (await store.ClaimAsync(10, TimeSpan.FromMinutes(1), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeEmpty();

        // The lease ran out without the row being completed: the row is claimable again.
        await Task.Delay(300, cancellationToken: TestContext.Current.CancellationToken);
        (await store.ClaimAsync(10, TimeSpan.FromMinutes(1), cancellationToken: TestContext.Current.CancellationToken)).Select(message => message.Id).ShouldBe([due.Id]);

        var retried = await store.GetByIdAsync(backedOff.Id, cancellationToken: TestContext.Current.CancellationToken);
        retried!.Status.ShouldBe(OutboxMessageStatus.Pending);
        retried.RetryCount.ShouldBe(1);
        retried.LastError.ShouldBe("broker down");
        retried.NextAttemptTime.ShouldNotBeNull();
    }

    [Fact]
    public async Task Processed_Outbox_And_Inbox_Rows_Are_Purged_After_Their_Window()
    {
        await using var context = CreateContext();
        var outbox = new EfCoreOutboxStore(context);
        var inbox = new EfCoreInboxStore(context);
        var old = CreateMessage(DateTime.UtcNow.AddDays(-10));
        var fresh = CreateMessage(DateTime.UtcNow);
        var pending = CreateMessage(DateTime.UtcNow.AddDays(-10));
        foreach (var message in new[] { old, fresh, pending })
        {
            await outbox.SaveAsync(message, cancellationToken: TestContext.Current.CancellationToken);
        }

        await outbox.MarkAsProcessedAsync(old.Id, cancellationToken: TestContext.Current.CancellationToken);
        await outbox.MarkAsProcessedAsync(fresh.Id, cancellationToken: TestContext.Current.CancellationToken);
        await context.OutboxMessages.Where(m => m.Id == old.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.ProcessedTime, DateTime.UtcNow.AddDays(-8)), cancellationToken: TestContext.Current.CancellationToken);
        var oldInbox = new InboxMessage
        {
            Id = Guid.NewGuid(), EventId = Guid.NewGuid(), EventType = "E", HandlerType = "H", Payload = "{}",
            CreationTime = DateTime.UtcNow.AddDays(-9), ProcessedTime = DateTime.UtcNow.AddDays(-8), Status = InboxMessageStatus.Processed
        };
        var failedInbox = new InboxMessage
        {
            Id = Guid.NewGuid(), EventId = Guid.NewGuid(), EventType = "E", HandlerType = "H", Payload = "{}",
            CreationTime = DateTime.UtcNow.AddDays(-9), Status = InboxMessageStatus.Failed
        };
        await inbox.SaveAsync(oldInbox, cancellationToken: TestContext.Current.CancellationToken);
        await inbox.SaveAsync(failedInbox, cancellationToken: TestContext.Current.CancellationToken);

        var services = new ServiceCollection();
        services.AddScoped<IOutboxStore>(_ => outbox);
        services.AddScoped<IInboxStore>(_ => inbox);
        var cleanup = new EventBusCleanupBackgroundService(
            services.BuildServiceProvider().GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new EksenEventBusOptions()),
            NullLogger<EventBusCleanupBackgroundService>.Instance);

        await cleanup.StartAsync(CancellationToken.None);
        using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)))
        {
            while (await context.OutboxMessages.AnyAsync(m => m.Id == old.Id, cancellationToken: TestContext.Current.CancellationToken) || await context.InboxMessages.AnyAsync(m => m.Id == oldInbox.Id, cancellationToken: TestContext.Current.CancellationToken))
            {
                await Task.Delay(20, timeout.Token);
            }
        }

        await cleanup.StopAsync(CancellationToken.None);
        (await context.OutboxMessages.Select(m => m.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).ShouldBe([fresh.Id, pending.Id], ignoreOrder: true);
        (await context.InboxMessages.Select(m => m.Id).ToListAsync(cancellationToken: TestContext.Current.CancellationToken)).ShouldBe([failedInbox.Id]);
    }

    private static OutboxMessage CreateMessage(DateTime creationTime)
    {
        return new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventId = Guid.NewGuid(),
            EventType = "Orders.Created",
            Payload = "{}",
            CreationTime = creationTime,
            Status = OutboxMessageStatus.Pending
        };
    }

    private EventBusDbContext CreateContext()
    {
        return new EventBusDbContext(new DbContextOptionsBuilder<EventBusDbContext>()
            .UseSqlite($"Data Source={_databasePath};Default Timeout=30")
            .Options);
    }

    private ServiceProvider CreateHost()
    {
        var services = new ServiceCollection();
        services.AddScoped(_ => CreateContext());
        services.AddScoped<IOutboxStore>(provider => new EfCoreOutboxStore(provider.GetRequiredService<EventBusDbContext>()));
        return services.BuildServiceProvider();
    }

    private static OutboxProcessorBackgroundService CreateProcessor(
        ServiceProvider host,
        IEventBusTransport transport,
        IOptions<EksenEventBusOptions> options)
    {
        return new OutboxProcessorBackgroundService(
            host.GetRequiredService<IServiceScopeFactory>(),
            transport,
            options,
            NullLogger<OutboxProcessorBackgroundService>.Instance);
    }

    private async Task<int> CountProcessedAsync()
    {
        await using var context = CreateContext();
        return await context.OutboxMessages.CountAsync(m => m.Status == (int)OutboxMessageStatus.Processed);
    }
}
