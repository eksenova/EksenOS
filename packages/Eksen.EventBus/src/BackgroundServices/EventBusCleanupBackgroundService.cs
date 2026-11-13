using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus.BackgroundServices;

/// <summary>
/// Purges processed outbox rows older than <see cref="OutboxOptions.ProcessedRetention"/> and processed inbox records
/// older than <see cref="InboxOptions.IdempotencyWindow"/>, every <see cref="CleanupOptions.Interval"/>. Stores that are
/// not registered are skipped, so a host without a database runs nothing here.
/// </summary>
public class EventBusCleanupBackgroundService(
    IServiceScopeFactory scopeFactory,
    IOptions<EksenEventBusOptions> options,
    ILogger<EventBusCleanupBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cleanupOptions = options.Value.Cleanup;
        if (!cleanupOptions.IsEnabled)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanupAsync(DateTime.UtcNow, stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Error purging processed event bus messages");
            }

            await Task.Delay(cleanupOptions.Interval, stoppingToken);
        }
    }

    internal async Task<(int Outbox, int Inbox)> CleanupAsync(DateTime now, CancellationToken cancellationToken)
    {
        var busOptions = options.Value;
        var batchSize = Math.Max(1, busOptions.Cleanup.BatchSize);
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;
        var inspector = services.GetService<IServiceProviderIsService>();

        var outboxDeleted = 0;
        if (busOptions.Outbox.IsEnabled && IsRegistered<IOutboxStore>(services, inspector))
        {
            var outboxStore = services.GetRequiredService<IOutboxStore>();
            var cutoff = now - busOptions.Outbox.ProcessedRetention;
            outboxDeleted = await DeleteInBatchesAsync(
                take => outboxStore.DeleteProcessedAsync(cutoff, take, cancellationToken),
                batchSize,
                cancellationToken);
        }

        var inboxDeleted = 0;
        if (busOptions.Inbox.IsEnabled && IsRegistered<IInboxStore>(services, inspector))
        {
            var inboxStore = services.GetRequiredService<IInboxStore>();
            var cutoff = now - busOptions.Inbox.IdempotencyWindow;
            inboxDeleted = await DeleteInBatchesAsync(
                take => inboxStore.DeleteProcessedAsync(cutoff, take, cancellationToken),
                batchSize,
                cancellationToken);
        }

        if (outboxDeleted > 0 || inboxDeleted > 0)
        {
            logger.LogInformation(
                "Purged {OutboxCount} processed outbox and {InboxCount} processed inbox messages",
                outboxDeleted,
                inboxDeleted);
        }

        return (outboxDeleted, inboxDeleted);
    }

    private static bool IsRegistered<TService>(IServiceProvider services, IServiceProviderIsService? inspector)
        where TService : class
    {
        return inspector?.IsService(typeof(TService)) ?? services.GetService<TService>() != null;
    }

    private static async Task<int> DeleteInBatchesAsync(
        Func<int, Task<int>> deleteBatch,
        int batchSize,
        CancellationToken cancellationToken)
    {
        var total = 0;
        while (!cancellationToken.IsCancellationRequested)
        {
            var deleted = await deleteBatch(batchSize);
            total += deleted;
            if (deleted < batchSize)
            {
                break;
            }
        }

        return total;
    }
}
