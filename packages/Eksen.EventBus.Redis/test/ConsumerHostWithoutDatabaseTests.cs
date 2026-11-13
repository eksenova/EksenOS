using System.Collections.Concurrent;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.RabbitMq;
using Eksen.TestBase;
using Eksen.UnitOfWork;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using StackExchange.Redis;

namespace Eksen.EventBus.Redis.Tests;

/// <summary>
/// A consumer-only host, like the Discord bot: event bus, RabbitMQ and the Redis inbox; no database, no outbox, no
/// unit of work.
/// </summary>
public class ConsumerHostWithoutDatabaseTests(ContainersFixture containers)
    : EksenUnitTestBase, IClassFixture<ContainersFixture>
{
    [Fact]
    public async Task Host_Starts_And_Handles_Each_EventId_Once()
    {
        var appName = "consumer-" + Guid.NewGuid().ToString("N");
        var exchange = "exchange-" + Guid.NewGuid().ToString("N");
        var builder = Host.CreateApplicationBuilder();
        builder.Services.AddSingleton<ReceivedEvents>();
        builder.Services.AddEksen(eksen => eksen.AddEventBus(bus => bus
            .Configure(options => options.AppName = appName)
            .Subscribe<MemberJoined, MemberJoinedHandler>()
            .UseRabbitMq(rabbit => rabbit.Configure(options =>
            {
                options.HostName = containers.RabbitHostName;
                options.Port = containers.RabbitPort;
                options.ExchangeName = exchange;
                options.DeadLetterExchangeName = exchange + ".dlx";
            }))
            .UseRedisInbox(options => options.Configuration = containers.RedisConfiguration)));
        builder.Services.Configure<HostOptions>(options => options.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.StopHost);
        builder.Services.AddLogging();
        using var host = builder.Build();

        host.Services.GetService<IUnitOfWorkManager>().ShouldBeNull();
        host.Services.GetRequiredService<IInboxStore>().ShouldBeOfType<RedisInboxStore>();

        await host.StartAsync(TestContext.Current.CancellationToken);
        try
        {
            // The listener declares its queues in the background.
            var transport = host.Services.GetRequiredService<RabbitMqEventBusTransport>();
            var received = host.Services.GetRequiredService<ReceivedEvents>();
            var joined = new MemberJoined { Member = "ada" };
            await PublishUntilConsumedAsync(host, joined, received);

            // The broker delivers the same event again (a redelivery, a second publish of the same event).
            await using (var scope = host.Services.CreateAsyncScope())
            {
                await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(joined, TestContext.Current.CancellationToken);
            }

            await transport.PublishAsync(
                EventNameResolver.GetEventName<MemberJoined>(),
                new JsonEventSerializer().Serialize(joined),
                null, null, null, joined.EventId, null, TestContext.Current.CancellationToken);
            await Task.Delay(1000, TestContext.Current.CancellationToken);

            received.EventIds.ShouldBe([joined.EventId]);
            var multiplexer = host.Services.GetRequiredService<IConnectionMultiplexer>();
            var key = $"eksen:inbox:{appName}:h:{typeof(MemberJoinedHandler).FullName}:{joined.EventId:N}";
            (await multiplexer.GetDatabase().StringGetAsync(key)).ToString().ShouldContain("\"processed\"");
        }
        finally
        {
            await host.StopAsync(CancellationToken.None);
        }
    }

    private static async Task PublishUntilConsumedAsync(IHost host, MemberJoined joined, ReceivedEvents received)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (received.EventIds.IsEmpty)
        {
            try
            {
                await using var scope = host.Services.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IEventBus>().PublishAsync(joined, timeout.Token);
            }
            catch (RabbitMQ.Client.Exceptions.PublishException)
            {
                // Not routable until the listener bound its queue.
            }

            await Task.Delay(200, timeout.Token);
        }
    }

    public sealed class MemberJoined : IntegrationEvent
    {
        public string Member { get; init; } = "";
    }

    public sealed class ReceivedEvents
    {
        public ConcurrentQueue<Guid> EventIds { get; } = new();
    }

    public sealed class MemberJoinedHandler(ReceivedEvents received) : IEventHandler<MemberJoined>
    {
        public Task HandleAsync(MemberJoined @event, CancellationToken cancellationToken = default)
        {
            received.EventIds.Enqueue(@event.EventId);
            return Task.CompletedTask;
        }
    }
}
