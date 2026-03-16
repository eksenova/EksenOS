using Eksen.EventBus.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus.BackgroundServices;

public class OutboxProcessorBackgroundService(
    IServiceScopeFactory scopeFactory,
    IEventBusTransport transport,
    IOptions<EksenEventBusOptions> options,
    ILogger<OutboxProcessorBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var outboxOptions = options.Value.Outbox;

        if (!outboxOptions.IsEnabled)
        {
            logger.LogInformation("Outbox processing is disabled");
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

    private async Task ProcessBatchAsync(OutboxOptions outboxOptions, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var outboxStore = scope.ServiceProvider.GetRequiredService<IOutboxStore>();

        var messages = await outboxStore.GetPendingAsync(outboxOptions.BatchSize, cancellationToken);

        foreach (var message in messages)
        {
            try
            {
                await transport.PublishAsync(
                    message.EventType,
                    message.Payload,
                    message.CorrelationId,
                    message.SourceApp,
                    message.TargetApp,
                    message.Id,
                    message.Headers,
                    cancellationToken);

                await outboxStore.MarkAsProcessedAsync(message.Id, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Failed to process outbox message {MessageId}", message.Id);
                await outboxStore.IncrementRetryCountAsync(message.Id, cancellationToken);

                if (message.RetryCount >= options.Value.DeadLetter.MaxRetryAttemptsBeforeDeadLetter)
                {
                    await outboxStore.MarkAsFailedAsync(message.Id, ex.Message, cancellationToken);
                }
            }
        }
    }
}
