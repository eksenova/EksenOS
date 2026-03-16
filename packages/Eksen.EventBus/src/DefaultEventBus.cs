using Eksen.EventBus.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus;

public class DefaultEventBus(
    IEventBusTransport transport,
    IEventSerializer serializer,
    IOptions<EksenEventBusOptions> eventBusOptionsAccessor,
    IServiceScopeFactory scopeFactory) : IEventBus
{
    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        return PublishAsync(@event, options: null, cancellationToken);
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions? options,
        CancellationToken cancellationToken = default)
        where TEvent : class, IIntegrationEvent
    {
        var eventTypeName = EventNameResolver.GetEventName<TEvent>();
        var payload = serializer.Serialize(@event);
        var eventBusOptions = eventBusOptionsAccessor.Value;

        if (eventBusOptions.Outbox.IsEnabled)
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventType = eventTypeName,
                Payload = payload,
                CreationTime = DateTime.UtcNow,
                Status = OutboxMessageStatus.Pending,
                CorrelationId = options?.CorrelationId ?? @event.CorrelationId,
                SourceApp = @event.SourceApp ?? eventBusOptions.AppName,
                TargetApp = options?.TargetApp,
                Headers = options?.Headers
            };

            await outboxStore.SaveAsync(outboxMessage, cancellationToken);
            return;
        }

        await transport.PublishAsync(
            eventTypeName,
            payload,
            options?.CorrelationId ?? @event.CorrelationId,
            @event.SourceApp ?? eventBusOptions.AppName,
            options?.TargetApp,
            @event.EventId,
            options?.Headers,
            cancellationToken);
    }
}
