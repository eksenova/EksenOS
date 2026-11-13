using Eksen.EventBus.Outbox;
using Eksen.TestBase;
using Shouldly;

namespace Eksen.EventBus.InMemory.Tests;

public class InMemoryOutboxClaimTests : EksenUnitTestBase
{
    [Fact]
    public async Task Concurrent_Claims_Lease_Each_Row_Once_And_Honour_Backoff()
    {
        var store = new InMemoryOutboxStore();
        var ids = new List<Guid>();
        for (var i = 0; i < 40; i++)
        {
            var message = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = Guid.NewGuid(),
                EventType = "Orders.Created",
                Payload = "{}",
                CreationTime = DateTime.UtcNow.AddSeconds(i),
                Status = OutboxMessageStatus.Pending
            };
            ids.Add(message.Id);
            await store.SaveAsync(message, cancellationToken: TestContext.Current.CancellationToken);
        }

        var claims = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Task.Run(() => store.ClaimAsync(10, TimeSpan.FromMinutes(1)))));
        var claimed = claims.SelectMany(batch => batch.Select(message => message.Id)).ToList();
        claimed.ShouldBe(ids, ignoreOrder: true);

        await store.ScheduleRetryAsync(ids[0], DateTime.UtcNow.AddMinutes(1), "broker down", cancellationToken: TestContext.Current.CancellationToken);
        await store.MarkAsProcessedAsync(ids[1], cancellationToken: TestContext.Current.CancellationToken);
        (await store.ClaimAsync(100, TimeSpan.FromMinutes(1), cancellationToken: TestContext.Current.CancellationToken)).ShouldBeEmpty();
        (await store.DeleteProcessedAsync(DateTime.UtcNow.AddSeconds(1), 100, cancellationToken: TestContext.Current.CancellationToken)).ShouldBe(1);
        (await store.GetByIdAsync(ids[0], cancellationToken: TestContext.Current.CancellationToken))!.RetryCount.ShouldBe(1);
    }
}
