using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.EventBus.Outbox;
using Eksen.EventBus.Retry;
using Eksen.EventBus.Telemetry;
using Eksen.Telemetry;
using Eksen.Telemetry.Propagation;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus.BackgroundServices;

public class OutboxProcessorBackgroundService(
    IServiceScopeFactory scopeFactory,
    IEventBusTransport transport,
    IOptions<EksenEventBusOptions> options,
    ILogger<OutboxProcessorBackgroundService> logger,
    IMeterFactory? meterFactory) : BackgroundService
{
    private readonly EventBusTelemetry _telemetry = EventBusTelemetry.For(meterFactory);

    public OutboxProcessorBackgroundService(
        IServiceScopeFactory scopeFactory,
        IEventBusTransport transport,
        IOptions<EksenEventBusOptions> options,
        ILogger<OutboxProcessorBackgroundService> logger)
        : this(scopeFactory, transport, options, logger, meterFactory: null)
    {
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outboxOptions = options.Value.Outbox;

        if (!outboxOptions.IsEnabled)
        {
            logger.LogInformation("Outbox processing is disabled");
            return;
        }

        if (!HasOutboxStore())
        {
            logger.LogInformation("Outbox processing is not running: no outbox store is registered");
            return;
        }

        logger.LogInformation(
            "Outbox processor started. Polling interval: {Interval}s, Batch size: {BatchSize}",
            outboxOptions.PollingInterval.TotalSeconds,
            outboxOptions.BatchSize);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessBatchAsync(outboxOptions, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(outboxOptions.PollingInterval, stoppingToken);
        }
    }

    private bool HasOutboxStore()
    {
        using var scope = scopeFactory.CreateScope();
        var inspector = scope.ServiceProvider.GetService<IServiceProviderIsService>();
        return inspector?.IsService(typeof(IOutboxStore)) ?? scope.ServiceProvider.GetService<IOutboxStore>() != null;
    }

    internal async Task ProcessBatchAsync(OutboxOptions outboxOptions, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        // Claiming leases the rows to this host, so several hosts polling the same table never publish a row together.
        var messages = await outboxStore.ClaimAsync(outboxOptions.BatchSize, outboxOptions.LeaseDuration, cancellationToken);
        if (messages.Count == 0)
        {
            // An idle poll emits no span.
            return;
        }

        using var pollActivity = EksenActivity.StartRoot(
            EventBusActivitySource.Instance,
            "outbox poll",
            ActivityKind.Internal,
            tags:
            [
                new KeyValuePair<string, object?>(EksenSemanticConventions.MessagingSystem, EventBusActivitySource.MessagingSystemName),
                new KeyValuePair<string, object?>("messaging.batch.message_count", messages.Count)
            ]);

        foreach (var message in messages)
        {
            try
            {
                await PublishMessageAsync(outboxStore, message, pollActivity, cancellationToken);
            }
            finally
            {
                // A publish span started from a stored remote context does not make the poll span current again
                // when it stops, so restore it for the next message.
                if (pollActivity is not null)
                {
                    Activity.Current = pollActivity;
                }
            }
        }
    }

    private async Task PublishMessageAsync(
        IOutboxStore outboxStore,
        OutboxMessage message,
        Activity? pollActivity,
        CancellationToken cancellationToken)
    {
        using var activity = StartPublishActivity(message, pollActivity);

        // The consumer is parented on this producer span; the context stored at publish time stays the producer's
        // own parent.
        var headers = EventBusActivitySource.WithTraceContext(activity, message.Headers);

        try
        {
            var startedAt = Stopwatch.GetTimestamp();
            try
            {
                await transport.PublishAsync(
                    message.EventType,
                    message.Payload,
                    message.CorrelationId,
                    message.SourceApp,
                    message.TargetApp,
                    message.EventId,
                    headers,
                    cancellationToken);
            }
            catch (Exception publishError)
            {
                RecordPublish(message, startedAt, publishError);
                throw;
            }

            RecordPublish(message, startedAt, error: null);
            _telemetry.OutboxLag.Record(
                Math.Max(0, (DateTime.UtcNow - message.CreationTime).TotalSeconds),
                new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventType, message.EventType));

            await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            activity.RecordException(ex);
            logger.LogError(ex, "Failed to publish outbox message {MessageId}", message.Id);

            var outboxOptions = options.Value.Outbox;
            // The message still holds the count read before this attempt.
            var attempts = message.RetryCount + 1;
            if (attempts >= outboxOptions.MaxPublishAttempts)
            {
                await outboxStore.IncrementRetryCountAsync(message.Id, cancellationToken);
                await outboxStore.MarkAsFailedAsync(message.Id, Truncate(ex.Message), cancellationToken);
                return;
            }

            var delay = ExponentialBackoff.GetDelay(
                attempts,
                outboxOptions.RetryInitialDelay,
                outboxOptions.RetryMaxDelay,
                outboxOptions.RetryBackoffMultiplier);
            await outboxStore.ScheduleRetryAsync(message.Id, DateTime.UtcNow + delay, Truncate(ex.Message), cancellationToken);
        }
    }

    private static string Truncate(string error)
    {
        return error.Length <= 2048 ? error : error[..2048];
    }

    private static Activity? StartPublishActivity(OutboxMessage message, Activity? pollActivity)
    {
        var source = EventBusActivitySource.Instance;
        if (!source.HasListeners())
        {
            return null;
        }

        var tags = new ActivityTagsCollection
        {
            { EksenSemanticConventions.MessagingSystem, EventBusActivitySource.MessagingSystemName },
            { EksenSemanticConventions.MessagingOperationType, "send" },
            { EksenSemanticConventions.MessagingOperationName, "publish" },
            { EksenSemanticConventions.MessagingMessageId, message.EventId.ToString() },
            { EksenSemanticConventions.EksenEventId, message.EventId.ToString() },
            { EksenSemanticConventions.EksenEventType, message.EventType },
            { EksenSemanticConventions.MessagingDestinationName, message.TargetApp ?? message.EventType }
        };

        var storedContext = TraceContextPropagation.Extract(message.Headers, out _);
        if (storedContext == default)
        {
            return source.StartActivity(
                $"publish {message.EventType}",
                ActivityKind.Producer,
                pollActivity?.Context ?? default,
                tags);
        }

        ActivityLink[]? links = pollActivity is null ? null : [new ActivityLink(pollActivity.Context)];
        return source.StartActivity(
            $"publish {message.EventType}",
            ActivityKind.Producer,
            storedContext,
            tags,
            links);
    }

    private void RecordPublish(OutboxMessage message, long startedAt, Exception? error)
    {
        var eventType = new KeyValuePair<string, object?>(EksenSemanticConventions.EksenEventType, message.EventType);

        _telemetry.SentMessages.Add(
            1,
            eventType,
            new KeyValuePair<string, object?>(EventBusTelemetry.OutcomeTag, error is null ? "ok" : "failed"));

        var durationTags = new TagList
        {
            eventType,
            { EksenSemanticConventions.MessagingOperationName, "publish" }
        };
        if (error is not null)
        {
            durationTags.Add(EksenSemanticConventions.ErrorType, error.GetType().FullName ?? error.GetType().Name);
        }

        _telemetry.OperationDuration.Record(Stopwatch.GetElapsedTime(startedAt).TotalSeconds, durationTags);
    }
}
