using System.Text.Json;
using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Retry;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus;

public interface IEventProcessor
{
    Task ProcessAsync(
        string eventTypeName,
        string payload,
        string? correlationId,
        string? sourceApp,
        Guid eventId,
        CancellationToken cancellationToken = default);
}

public class EventProcessor(
    IServiceScopeFactory scopeFactory,
    IEventHandlerRegistry registry,
    IEventRetryPipelineProvider retryPipelineProvider,
    IOptions<EksenEventBusOptions> options,
    ILogger<EventProcessor> logger) : IEventProcessor
{
    public async Task ProcessAsync(
        string eventTypeName,
        string payload,
        string? correlationId,
        string? sourceApp,
        Guid eventId,
        CancellationToken cancellationToken = default)
    {
        var descriptors = registry.GetHandlers(eventTypeName);

        if (descriptors.Count == 0)
        {
            logger.LogWarning("No handlers registered for event type {EventType}", eventTypeName);
            return;
        }

        foreach (var descriptor in descriptors)
        {
            await ProcessHandlerAsync(
                descriptor,
                payload,
                correlationId,
                sourceApp,
                eventId,
                cancellationToken);
        }
    }

    private async Task ProcessHandlerAsync(
        EventHandlerDescriptor descriptor,
        string payload,
        string? correlationId,
        string? sourceApp,
        Guid eventId,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();

        var inboxStore = scope.ServiceProvider.GetService<IInboxStore>();
        if (inboxStore != null && options.Value.Inbox.IsEnabled)
        {
            var exists = await inboxStore.ExistsAsync(eventId, descriptor.HandlerTypeName, cancellationToken);
            if (exists)
            {
                logger.LogDebug(
                    "Event {EventId} already processed by handler {HandlerType}",
                    eventId,
                    descriptor.HandlerTypeName);
                return;
            }

            var inboxMessage = new InboxMessage
            {
                Id = Guid.NewGuid(),
                EventId = eventId,
                EventType = descriptor.EventTypeName,
                HandlerType = descriptor.HandlerTypeName,
                Payload = payload,
                CreationTime = DateTime.UtcNow,
                Status = InboxMessageStatus.Processing,
                CorrelationId = correlationId,
                SourceApp = sourceApp
            };

            await inboxStore.SaveAsync(inboxMessage, cancellationToken);
        }

        var @event = JsonSerializer.Deserialize(payload, descriptor.EventType);
        if (@event == null)
        {
            logger.LogError("Failed to deserialize event {EventType} with payload {Payload}", descriptor.EventTypeName, payload);
            return;
        }

        var pipeline = retryPipelineProvider.GetPipeline();

        try
        {
            await pipeline.ExecuteAsync(async ct =>
            {
                await using var handlerScope = scopeFactory.CreateAsyncScope();
                var handler = handlerScope.ServiceProvider.GetRequiredService(descriptor.HandlerType);

                var handleMethod = descriptor.HandlerType
                    .GetMethod(nameof(IEventHandler<IntegrationEvent>.HandleAsync));

                if (handleMethod == null)
                {
                    throw new InvalidOperationException(
                        $"Handler {descriptor.HandlerTypeName} does not implement HandleAsync");
                }

                var task = (Task)handleMethod.Invoke(handler, [@event, ct])!;
                await task;
            }, cancellationToken);

            if (inboxStore != null && options.Value.Inbox.IsEnabled)
            {
                var existing = await FindInboxMessageAsync(inboxStore, eventId, descriptor.HandlerTypeName, cancellationToken);
                if (existing != null)
                    await inboxStore.MarkAsProcessedAsync(existing.Id, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process event {EventType} with handler {HandlerType}",
                descriptor.EventTypeName, descriptor.HandlerTypeName);

            if (inboxStore != null && options.Value.Inbox.IsEnabled)
            {
                var existing = await FindInboxMessageAsync(inboxStore, eventId, descriptor.HandlerTypeName, cancellationToken);
                if (existing != null)
                    await inboxStore.MarkAsFailedAsync(existing.Id, ex.Message, cancellationToken);
            }

            await SendToDeadLetterAsync(scope.ServiceProvider, descriptor, payload, correlationId, sourceApp, eventId, ex);
        }
    }

    private async Task SendToDeadLetterAsync(
        IServiceProvider serviceProvider,
        EventHandlerDescriptor descriptor,
        string payload,
        string? correlationId,
        string? sourceApp,
        Guid eventId,
        Exception exception)
    {
        if (!options.Value.DeadLetter.IsEnabled)
            return;

        var deadLetterStore = serviceProvider.GetService<IDeadLetterStore>();
        if (deadLetterStore == null)
            return;

        var deadLetterMessage = new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            OriginalMessageId = eventId,
            EventType = descriptor.EventTypeName,
            HandlerType = descriptor.HandlerTypeName,
            Payload = payload,
            CreationTime = DateTime.UtcNow,
            FailedTime = DateTime.UtcNow,
            TotalRetryCount = options.Value.Retry.MaxRetryAttempts,
            LastError = exception.Message,
            CorrelationId = correlationId,
            SourceApp = sourceApp
        };

        await deadLetterStore.SaveAsync(deadLetterMessage);

        var notifier = serviceProvider.GetService<IDeadLetterNotifier>();
        if (notifier != null)
        {
            try
            {
                await notifier.NotifyAsync(deadLetterMessage);
            }
            catch (Exception notifyEx)
            {
                logger.LogError(notifyEx, "Failed to send dead letter notification for event {EventType}", descriptor.EventTypeName);
            }
        }
    }

    private static async Task<InboxMessage?> FindInboxMessageAsync(
        IInboxStore store,
        Guid eventId,
        string handlerType,
        CancellationToken cancellationToken)
    {
        var messages = await store.GetMessagesAsync(
            InboxMessageStatus.Processing,
            skip: 0,
            take: 1,
            cancellationToken);

        return messages.FirstOrDefault(m => m.EventId == eventId && m.HandlerType == handlerType);
    }
}
