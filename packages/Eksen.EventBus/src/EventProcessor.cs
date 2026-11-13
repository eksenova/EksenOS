using Eksen.UnitOfWork;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.ExceptionServices;
using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Retry;
using Eksen.EventBus.Telemetry;
using Eksen.Telemetry;
using Polly;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus;

public interface IEventProcessor
{
    /// <summary>
    /// Runs every handler registered for <paramref name="eventTypeName"/>, treating this as the final attempt: a
    /// handler that still fails after the in-process retries is dead-lettered (or its error is rethrown).
    /// </summary>
    Task ProcessAsync(
        string eventTypeName,
        string payload,
        string? correlationId,
        string? sourceApp,
        Guid eventId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs the one handler named by <see cref="EventDelivery.HandlerTypeName"/>. Throws
    /// <see cref="PoisonMessageException"/> for a message that can never be handled and no dead-letter store took
    /// it, and rethrows the handler's error when it fails and the delivery is not final (the transport redelivers it)
    /// or no dead-letter store took it.
    /// </summary>
    Task<EventProcessingOutcome> ProcessAsync(EventDelivery delivery, CancellationToken cancellationToken = default);
}

public class EventProcessor(
    IServiceScopeFactory scopeFactory,
    IEventHandlerRegistry registry,
    IEventRetryPipelineProvider retryPipelineProvider,
    IOptions<EksenEventBusOptions> options,
    ILogger<EventProcessor> logger,
    IEventSerializer serializer,
    IMeterFactory? meterFactory) : IEventProcessor
{
    private readonly EventBusTelemetry _telemetry = EventBusTelemetry.For(meterFactory);

    public EventProcessor(
        IServiceScopeFactory scopeFactory,
        IEventHandlerRegistry registry,
        IEventRetryPipelineProvider retryPipelineProvider,
        IOptions<EksenEventBusOptions> options,
        ILogger<EventProcessor> logger,
        IEventSerializer serializer)
        : this(scopeFactory, registry, retryPipelineProvider, options, logger, serializer, meterFactory: null)
    {
    }

    public EventProcessor(
        IServiceScopeFactory scopeFactory,
        IEventHandlerRegistry registry,
        IEventRetryPipelineProvider retryPipelineProvider,
        IOptions<EksenEventBusOptions> options,
        ILogger<EventProcessor> logger)
        : this(scopeFactory, registry, retryPipelineProvider, options, logger, new JsonEventSerializer(), meterFactory: null)
    {
    }

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
            var delivery = new EventDelivery
            {
                EventTypeName = eventTypeName,
                Payload = payload,
                EventId = eventId,
                HandlerTypeName = descriptor.HandlerTypeName,
                CorrelationId = correlationId,
                SourceApp = sourceApp
            };

            await ProcessHandlerAsync(descriptor, delivery, cancellationToken);
        }
    }

    public async Task<EventProcessingOutcome> ProcessAsync(
        EventDelivery delivery,
        CancellationToken cancellationToken = default)
    {
        var descriptor = registry.GetHandlers(delivery.EventTypeName)
            .FirstOrDefault(handler => handler.HandlerTypeName == delivery.HandlerTypeName);

        if (descriptor == null)
        {
            var poison = new PoisonMessageException(
                $"No handler {delivery.HandlerTypeName} is registered for event type {delivery.EventTypeName}.");
            logger.LogError(
                "Event {EventType} ({EventId}) has no registered handler {HandlerType}",
                delivery.EventTypeName,
                delivery.EventId,
                delivery.HandlerTypeName);

            await using var scope = scopeFactory.CreateAsyncScope();
            if (await SendToDeadLetterAsync(scope.ServiceProvider, Activity.Current, delivery, poison, inProcessAttempts: 0))
            {
                return EventProcessingOutcome.DeadLettered;
            }

            throw poison;
        }

        return await ProcessHandlerAsync(descriptor, delivery, cancellationToken);
    }

    private async Task<EventProcessingOutcome> ProcessHandlerAsync(
        EventHandlerDescriptor descriptor,
        EventDelivery delivery,
        CancellationToken cancellationToken)
    {
        // The span is a child of the transport's consumer span, which is Activity.Current when a transport calls in.
        using var activity = EventBusActivitySource.Instance.StartActivity(
            $"{descriptor.HandlerTypeName} handle",
            ActivityKind.Internal,
            parentContext: default,
            tags:
            [
                new KeyValuePair<string, object?>(EksenSemanticConventions.MessagingSystem, EventBusActivitySource.MessagingSystemName),
                new KeyValuePair<string, object?>(EksenSemanticConventions.MessagingOperationType, "process"),
                new KeyValuePair<string, object?>(EksenSemanticConventions.MessagingConsumerGroupName, descriptor.HandlerTypeName),
                new KeyValuePair<string, object?>(EksenSemanticConventions.EksenHandler, descriptor.HandlerTypeName),
                new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventType, descriptor.EventTypeName),
                new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventId, delivery.EventId.ToString())
            ]);

        if (delivery.Attempt > 1)
        {
            activity?.SetTag("eksen.delivery.attempt", delivery.Attempt);
        }

        var startedAt = Stopwatch.GetTimestamp();
        var result = new HandlerResult(HandlerOutcome.Failed, Failure: null);

        try
        {
            result = await ExecuteHandlerAsync(activity, descriptor, delivery, cancellationToken);
            return result.Outcome switch
            {
                HandlerOutcome.Duplicate => EventProcessingOutcome.Duplicate,
                HandlerOutcome.DeadLettered => EventProcessingOutcome.DeadLettered,
                _ => EventProcessingOutcome.Processed
            };
        }
        catch (Exception exception)
        {
            result = new HandlerResult(HandlerOutcome.Failed, exception);
            throw;
        }
        finally
        {
            RecordHandlerOutcome(activity, descriptor, result.Outcome, result.Failure, startedAt);
        }
    }

    private async Task<HandlerResult> ExecuteHandlerAsync(
        Activity? activity,
        EventHandlerDescriptor descriptor,
        EventDelivery delivery,
        CancellationToken cancellationToken)
    {
        // A payload that does not deserialize can never succeed: it skips the retries.
        object @event;
        try
        {
            @event = serializer.Deserialize(delivery.Payload, descriptor.EventType)
                ?? throw new PoisonMessageException($"Event {descriptor.EventTypeName} deserialized to null.");
        }
        catch (Exception exception) when (exception is not PoisonMessageException)
        {
            var poison = new PoisonMessageException(
                $"Failed to deserialize event {descriptor.EventTypeName}: {exception.Message}",
                exception);
            return await HandleFailureAsync(activity, descriptor, delivery, poison, inProcessAttempts: 0, cancellationToken);
        }
        catch (PoisonMessageException poison)
        {
            return await HandleFailureAsync(activity, descriptor, delivery, poison, inProcessAttempts: 0, cancellationToken);
        }

        var pipeline = retryPipelineProvider.GetPipeline();
        var duplicate = false;
        var attempts = 0;
        var resilienceContext = ResilienceContextPool.Shared.Get(cancellationToken);
        resilienceContext.Properties.Set(EventBusTelemetry.EventTypeProperty, descriptor.EventTypeName);
        resilienceContext.Properties.Set(EventBusTelemetry.HandlerProperty, descriptor.HandlerTypeName);
        try
        {
            await pipeline.ExecuteAsync(async context =>
            {
                attempts++;
                duplicate = await HandleOnceAsync(descriptor, delivery, @event, context.CancellationToken);
            }, resilienceContext);

            return new HandlerResult(duplicate ? HandlerOutcome.Duplicate : HandlerOutcome.Processed, Failure: null);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return await HandleFailureAsync(activity, descriptor, delivery, exception, attempts, cancellationToken);
        }
        finally
        {
            ResilienceContextPool.Shared.Return(resilienceContext);
        }
    }

    /// <summary>
    /// One attempt: claim the inbox record, run the handler and complete the claim, inside a unit of work when one is
    /// registered. Returns true when the delivery is a duplicate.
    /// </summary>
    private async Task<bool> HandleOnceAsync(
        EventHandlerDescriptor descriptor,
        EventDelivery delivery,
        object @event,
        CancellationToken cancellationToken)
    {
        await using var handlerScope = scopeFactory.CreateAsyncScope();
        var services = handlerScope.ServiceProvider;
        var manager = services.GetService<IUnitOfWorkManager>();
        await using var unitOfWork = manager?.BeginScope(cancellationToken: cancellationToken);
        var inboxStore = options.Value.Inbox.IsEnabled ? services.GetService<IInboxStore>() : null;
        InboxMessage? inboxMessage = null;
        try
        {
            if (inboxStore != null)
            {
                var claim = await inboxStore.TryClaimAsync(new InboxMessage
                {
                    Id = Guid.NewGuid(),
                    EventId = delivery.EventId,
                    EventType = descriptor.EventTypeName,
                    HandlerType = descriptor.HandlerTypeName,
                    Payload = delivery.Payload,
                    CreationTime = DateTime.UtcNow,
                    Status = InboxMessageStatus.Processing,
                    CorrelationId = delivery.CorrelationId,
                    SourceApp = delivery.SourceApp
                }, cancellationToken);

                switch (claim.Status)
                {
                    case InboxClaimStatus.AlreadyProcessed:
                    {
                        return true;
                    }
                    case InboxClaimStatus.InProgress:
                    {
                        throw new InboxClaimConflictException(delivery.EventId, descriptor.HandlerTypeName);
                    }
                }

                inboxMessage = claim.Message;
            }

            var handler = services.GetRequiredService(descriptor.HandlerType);
            var handleMethod = descriptor.HandlerType.GetMethod(nameof(IEventHandler<IntegrationEvent>.HandleAsync))
                ?? throw new InvalidOperationException($"Handler {descriptor.HandlerTypeName} does not implement HandleAsync.");
            // A handler that throws before its first await surfaces its own exception, not a TargetInvocationException.
            await (Task)handleMethod.Invoke(
                handler,
                System.Reflection.BindingFlags.DoNotWrapExceptions,
                binder: null,
                [@event, cancellationToken],
                culture: null)!;

            var transactionalInbox = inboxStore?.EnlistsInUnitOfWork ?? true;
            if (inboxStore != null && inboxMessage != null && transactionalInbox)
            {
                await inboxStore.CompleteAsync(inboxMessage, cancellationToken);
            }

            if (unitOfWork != null)
            {
                await unitOfWork.CommitAsync(cancellationToken);
            }

            // A store outside the transaction is completed only once the handler's work is committed.
            if (inboxStore != null && inboxMessage != null && !transactionalInbox)
            {
                await inboxStore.CompleteAsync(inboxMessage, cancellationToken);
            }

            return false;
        }
        catch (Exception exception)
        {
            try
            {
                if (unitOfWork != null)
                {
                    await unitOfWork.RollbackAsync(CancellationToken.None);
                }

                if (inboxStore != null && inboxMessage != null && (unitOfWork == null || !inboxStore.EnlistsInUnitOfWork))
                {
                    await inboxStore.ReleaseAsync(inboxMessage, exception.Message, CancellationToken.None);
                }
            }
            catch (Exception rollbackException)
            {
                exception.Data["RollbackException"] = rollbackException;
            }

            throw;
        }
    }

    private async Task<HandlerResult> HandleFailureAsync(
        Activity? activity,
        EventHandlerDescriptor descriptor,
        EventDelivery delivery,
        Exception exception,
        int inProcessAttempts,
        CancellationToken cancellationToken)
    {
        await using var failureScope = scopeFactory.CreateAsyncScope();
        var services = failureScope.ServiceProvider;
        var inboxStore = options.Value.Inbox.IsEnabled ? services.GetService<IInboxStore>() : null;
        if (inboxStore != null && exception is not PoisonMessageException)
        {
            var existing = await inboxStore.FindAsync(delivery.EventId, descriptor.HandlerTypeName, cancellationToken);
            if (existing?.Status == InboxMessageStatus.Processed)
            {
                return new HandlerResult(HandlerOutcome.Duplicate, Failure: null);
            }
        }

        activity.RecordException(exception);
        var poison = exception is PoisonMessageException;
        if (!delivery.IsFinalAttempt && !poison)
        {
            logger.LogWarning(exception,
                "Event {EventType} ({EventId}) failed in handler {HandlerType} on attempt {Attempt}; the transport redelivers it later",
                descriptor.EventTypeName, delivery.EventId, descriptor.HandlerTypeName, delivery.Attempt);
            ExceptionDispatchInfo.Throw(exception);
        }

        logger.LogError(exception, "Failed to process event {EventType} with handler {HandlerType}",
            descriptor.EventTypeName, descriptor.HandlerTypeName);
        if (!await SendToDeadLetterAsync(services, activity, delivery, exception, inProcessAttempts))
        {
            ExceptionDispatchInfo.Throw(exception);
        }

        return new HandlerResult(HandlerOutcome.DeadLettered, exception);
    }

    private void RecordHandlerOutcome(
        Activity? activity,
        EventHandlerDescriptor descriptor,
        HandlerOutcome outcome,
        Exception? failure,
        long startedAt)
    {
        var outcomeName = outcome switch
        {
            HandlerOutcome.Duplicate => "duplicate",
            HandlerOutcome.Failed => "failed",
            HandlerOutcome.DeadLettered => "dead_lettered",
            _ => "processed"
        };

        var eventType = new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventType, descriptor.EventTypeName);
        var handler = new KeyValuePair<string, object?>(EksenSemanticConventions.EksenHandler, descriptor.HandlerTypeName);

        _telemetry.ConsumedMessages.Add(
            1,
            eventType,
            handler,
            new KeyValuePair<string, object?>(EventBusTelemetry.OutcomeTag, outcomeName));

        var durationTags = new TagList { eventType, handler };
        if (failure is not null)
        {
            durationTags.Add(EksenSemanticConventions.ErrorType, failure.GetType().FullName ?? failure.GetType().Name);
        }

        _telemetry.ProcessDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, durationTags);

        activity.SetOutcome(outcomeName);
        switch (outcome)
        {
            case HandlerOutcome.Duplicate:
                _telemetry.InboxDuplicates.Add(1, eventType, handler);
                activity?.AddEvent(new ActivityEvent("inbox.duplicate"));
                break;
        }
    }

    private readonly record struct HandlerResult(HandlerOutcome Outcome, Exception? Failure);

    private enum HandlerOutcome
    {
        Processed,
        Duplicate,
        Failed,
        DeadLettered
    }

    private async Task<bool> SendToDeadLetterAsync(
        IServiceProvider serviceProvider,
        Activity? activity,
        EventDelivery delivery,
        Exception exception,
        int inProcessAttempts)
    {
        if (!options.Value.DeadLetter.IsEnabled)
        {
            return false;
        }

        var deadLetterStore = serviceProvider.GetService<IDeadLetterStore>();
        if (deadLetterStore == null)
        {
            return false;
        }

        var now = DateTime.UtcNow;
        var deadLetterMessage = new DeadLetterMessage
        {
            Id = Guid.NewGuid(),
            OriginalMessageId = delivery.EventId,
            EventType = delivery.EventTypeName,
            HandlerType = delivery.HandlerTypeName,
            Payload = delivery.Payload,
            CreationTime = delivery.FirstFailureTime ?? now,
            FailedTime = now,
            // Earlier deliveries each ran the handler with its in-process retries; this one ran it inProcessAttempts times.
            TotalRetryCount = Math.Max(0, ((delivery.Attempt - 1) * (options.Value.Retry.MaxRetryAttempts + 1)) + inProcessAttempts - 1),
            LastError = exception.Message.Length <= 2048 ? exception.Message : exception.Message[..2048],
            CorrelationId = delivery.CorrelationId,
            SourceApp = delivery.SourceApp,
            TargetApp = delivery.TargetApp
        };

        await deadLetterStore.SaveAsync(deadLetterMessage);

        var traceId = (activity ?? Activity.Current)?.TraceId.ToHexString();
        _telemetry.DeadLetters.Add(
            1,
            new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventType, delivery.EventTypeName),
            new KeyValuePair<string, object?>(EksenSemanticConventions.EksenHandler, delivery.HandlerTypeName));
        activity?.AddEvent(new ActivityEvent("dead_letter", tags: new ActivityTagsCollection
        {
            { "eksen.dead_letter.id", deadLetterMessage.Id.ToString() },
            { EksenSemanticConventions.ErrorType, exception.GetType().FullName ?? exception.GetType().Name }
        }));
        logger.LogError(
            "Event {EventType} ({EventId}) moved to the dead-letter store as {DeadLetterId} after handler {HandlerType} failed; trace {TraceId}",
            delivery.EventTypeName,
            delivery.EventId,
            deadLetterMessage.Id,
            delivery.HandlerTypeName,
            traceId ?? "none");

        var notifier = serviceProvider.GetService<IDeadLetterNotifier>();
        if (notifier != null)
        {
            try
            {
                await notifier.NotifyAsync(deadLetterMessage);
            }
            catch (Exception notifyEx)
            {
                logger.LogError(notifyEx, "Failed to send dead letter notification for event {EventType}", delivery.EventTypeName);
            }
        }

        return true;
    }
}
