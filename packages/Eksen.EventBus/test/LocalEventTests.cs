using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Moq;
using Shouldly;

namespace Eksen.EventBus.Tests;

public class LocalEventTests : EksenUnitTestBase
{
    private readonly Mock<IEventBusTransport> _transport = new();

    private readonly JsonEventSerializer _serializer = new();

    [Fact]
    public async Task PublishAsync_Should_Invoke_Every_Registered_Local_Handler()
    {
        var services = new ServiceCollection();
        services.AddScoped<RecordingLocalHandler>();
        services.AddScoped<SecondRecordingLocalHandler>();
        services.AddScoped<ILocalEventHandler<PayloadCarryingLocalEvent>>(
            sp => sp.GetRequiredService<RecordingLocalHandler>());
        services.AddScoped<ILocalEventHandler<PayloadCarryingLocalEvent>>(
            sp => sp.GetRequiredService<SecondRecordingLocalHandler>());

        var serviceProvider = services.BuildServiceProvider();
        var bus = CreateEventBus(serviceProvider);

        var @event = new PayloadCarryingLocalEvent { Payload = new object() };

        await bus.PublishAsync(@event);

        serviceProvider.GetRequiredService<RecordingLocalHandler>().Received.Count.ShouldBe(1);
        serviceProvider.GetRequiredService<SecondRecordingLocalHandler>().Received.Count.ShouldBe(1);
    }

    [Fact]
    public async Task PublishAsync_Should_Hand_The_Same_Instance_To_The_Handler()
    {
        var services = new ServiceCollection();
        services.AddScoped<RecordingLocalHandler>();
        services.AddScoped<ILocalEventHandler<PayloadCarryingLocalEvent>>(
            sp => sp.GetRequiredService<RecordingLocalHandler>());

        var serviceProvider = services.BuildServiceProvider();
        var bus = CreateEventBus(serviceProvider);

        var payload = new object();
        var @event = new PayloadCarryingLocalEvent { Payload = payload };

        await bus.PublishAsync(@event);

        var received = serviceProvider.GetRequiredService<RecordingLocalHandler>().Received.Single();

        received.ShouldBeSameAs(@event);
        received.Payload.ShouldBeSameAs(payload);
    }

    [Fact]
    public async Task PublishAsync_Should_Let_A_Handler_Write_Back_Through_The_Event()
    {
        var services = new ServiceCollection();
        services.AddScoped<RespondingLocalHandler>();
        services.AddScoped<ILocalEventHandler<PayloadCarryingLocalEvent>>(
            sp => sp.GetRequiredService<RespondingLocalHandler>());

        var serviceProvider = services.BuildServiceProvider();
        var bus = CreateEventBus(serviceProvider);

        var @event = new PayloadCarryingLocalEvent { Payload = new object() };

        await bus.PublishAsync(@event);

        @event.Response.ShouldBe("handled");
    }

    [Fact]
    public async Task PublishAsync_Should_Not_Touch_Transport_Or_Outbox_For_Local_Events()
    {
        var services = new ServiceCollection();
        services.AddScoped<RecordingLocalHandler>();
        services.AddScoped<ILocalEventHandler<PayloadCarryingLocalEvent>>(
            sp => sp.GetRequiredService<RecordingLocalHandler>());

        var serviceProvider = services.BuildServiceProvider();
        var bus = CreateEventBus(serviceProvider, outboxEnabled: true);

        await bus.PublishAsync(new PayloadCarryingLocalEvent { Payload = new object() });

        _transport.Verify(
            t => t.PublishAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<Guid>(),
                It.IsAny<Dictionary<string, string>?>(),
                It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PublishAsync_Should_Be_A_No_Op_When_No_Local_Handler_Is_Registered()
    {
        var serviceProvider = new ServiceCollection().BuildServiceProvider();
        var bus = CreateEventBus(serviceProvider);

        await Should.NotThrowAsync(() => bus.PublishAsync(new PayloadCarryingLocalEvent
        {
            Payload = new object()
        }));
    }

    private DefaultEventBus CreateEventBus(ServiceProvider serviceProvider, bool outboxEnabled = false)
    {
        var options = Options.Create(new EksenEventBusOptions
        {
            AppName = "TestApp",
            Outbox = new OutboxOptions { IsEnabled = outboxEnabled }
        });

        return new DefaultEventBus(
            _transport.Object,
            _serializer,
            options,
            serviceProvider.GetRequiredService<IServiceScopeFactory>(),
            new LocalEventDispatcher(serviceProvider));
    }

    private sealed class PayloadCarryingLocalEvent : LocalEvent
    {
        public required object Payload { get; init; }

        public string? Response { get; set; }
    }

    private sealed class RecordingLocalHandler : ILocalEventHandler<PayloadCarryingLocalEvent>
    {
        public List<PayloadCarryingLocalEvent> Received { get; } = [];

        public Task HandleAsync(PayloadCarryingLocalEvent @event, CancellationToken cancellationToken = default)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class SecondRecordingLocalHandler : ILocalEventHandler<PayloadCarryingLocalEvent>
    {
        public List<PayloadCarryingLocalEvent> Received { get; } = [];

        public Task HandleAsync(PayloadCarryingLocalEvent @event, CancellationToken cancellationToken = default)
        {
            Received.Add(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class RespondingLocalHandler : ILocalEventHandler<PayloadCarryingLocalEvent>
    {
        public Task HandleAsync(PayloadCarryingLocalEvent @event, CancellationToken cancellationToken = default)
        {
            @event.Response = "handled";
            return Task.CompletedTask;
        }
    }
}
