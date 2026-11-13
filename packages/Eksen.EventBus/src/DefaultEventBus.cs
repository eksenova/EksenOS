using System.Diagnostics;
using Eksen.EventBus.Outbox;
using Eksen.EventBus.Telemetry;
using Eksen.Telemetry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus;

public class DefaultEventBus(
    IEventBusTransport transport,
    IEventSerializer serializer,
    IOptions<EksenEventBusOptions> eventBusOptionsAccessor,
    IServiceScopeFactory scopeFactory,
    ILocalEventDispatcher localEventDispatcher,
    IServiceProvider? serviceProvider) : IEventBus
{
    public DefaultEventBus(
        IEventBusTransport transport,
        IEventSerializer serializer,
        IOptions<EksenEventBusOptions> eventBusOptionsAccessor,
        IServiceScopeFactory scopeFactory,
        ILocalEventDispatcher localEventDispatcher)
        : this(transport, serializer, eventBusOptionsAccessor, scopeFactory, localEventDispatcher, null)
    {
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        return PublishAsync(@event, options: null, cancellationToken);
    }

    public async Task PublishAsync<TEvent>(
        TEvent @event,
        PublishOptions? options,
        CancellationToken cancellationToken = default)
        where TEvent : class, IEvent
    {
        if (@event is ILocalEvent localEvent)
        {
            await localEventDispatcher.DispatchAsync(localEvent, typeof(TEvent), cancellationToken);
            return;
        }

        var eventTypeName = EventNameResolver.GetEventName<TEvent>();
        var payload = serializer.Serialize(@event);
        var eventBusOptions = eventBusOptionsAccessor.Value;

        // The trace context is captured now, while the caller's span is current. The outbox processor publishes
        // later from a background loop that has no ambient span.
        using var activity = StartCreateActivity(eventTypeName, @event.EventId);
        var headers = EventBusActivitySource.WithTraceContext(activity ?? Activity.Current, options?.Headers);

        if (eventBusOptions.Outbox.IsEnabled)
        {
            var outboxMessage = new OutboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = @event.EventId,
                EventType = eventTypeName,
                Payload = payload,
                CreationTime = DateTime.UtcNow,
                Status = OutboxMessageStatus.Pending,
                CorrelationId = options?.CorrelationId ?? @event.CorrelationId,
                SourceApp = @event.SourceApp ?? eventBusOptions.AppName,
                TargetApp = options?.TargetApp,
                Headers = headers
            };

            // A host without an outbox store (a consumer-only host with no database) publishes directly.
            if (serviceProvider != null)
            {
                var outboxStore = serviceProvider.GetService<IOutboxStore>();
                if (outboxStore != null)
                {
                    await outboxStore.SaveAsync(outboxMessage, cancellationToken);
                    return;
                }
            }
            else
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var outboxStore = scope.ServiceProvider.GetService<IOutboxStore>();
                if (outboxStore != null)
                {
                    await outboxStore.SaveAsync(outboxMessage, cancellationToken);
                    return;
                }
            }
        }

        await transport.PublishAsync(
            eventTypeName,
            payload,
            options?.CorrelationId ?? @event.CorrelationId,
            @event.SourceApp ?? eventBusOptions.AppName,
            options?.TargetApp,
            @event.EventId,
            headers,
            cancellationToken);
    }

    internal static Activity? StartCreateActivity(string eventTypeName, Guid eventId)
    {
        var source = EventBusActivitySource.Instance;
        if (!source.HasListeners())
        {
            return null;
        }

        return source.StartActivity(
            $"create {eventTypeName}",
            ActivityKind.Producer,
            parentContext: default,
            tags:
            [
                new KeyValuePair<string, object?>(EksenSemanticConventions.MessagingSystem, EventBusActivitySource.MessagingSystemName),
                new KeyValuePair<string, object?>(EksenSemanticConventions.MessagingOperationType, "create"),
                new KeyValuePair<string, object?>(EksenSemanticConventions.MessagingOperationName, "create"),
                new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventType, eventTypeName),
                new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventId, eventId.ToString())
            ]);
    }
}
