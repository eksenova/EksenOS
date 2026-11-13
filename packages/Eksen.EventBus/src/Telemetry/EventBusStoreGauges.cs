using System.Diagnostics.Metrics;
using Eksen.EventBus.DeadLetter;
using Eksen.EventBus.Inbox;
using Eksen.EventBus.Outbox;
using Eksen.Telemetry;
using Eksen.Telemetry.Background;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.EventBus.Telemetry;

internal sealed record EventBusStoreSnapshot(
    OutboxMessageStats? Outbox,
    InboxMessageStats? Inbox,
    int? DeadLetters);

/// <summary>
/// Publishes the outbox, inbox and dead-letter backlogs as gauges. The stores are queried by a background refresh
/// every 30 seconds; the gauge callbacks only read the cached snapshot. Nothing is queried or observed unless
/// telemetry export is enabled and at least one store is registered.
/// </summary>
internal sealed class EventBusStoreGauges : CachedGaugeSampler<EventBusStoreSnapshot>
{
    public static readonly TimeSpan RefreshInterval = TimeSpan.FromSeconds(30);

    private readonly bool _isActive;

    public EventBusStoreGauges(
        IServiceScopeFactory scopeFactory,
        IServiceProviderIsService serviceInspector,
        IOptions<EksenTelemetryOptions> telemetryOptions,
        EventBusTelemetry telemetry,
        ILogger<EventBusStoreGauges> logger)
        : this(
            scopeFactory,
            serviceInspector.IsService(typeof(IOutboxStore)),
            serviceInspector.IsService(typeof(IInboxStore)),
            serviceInspector.IsService(typeof(IDeadLetterStore)),
            telemetryOptions.Value.IsExportEnabled,
            telemetry,
            logger)
    {
    }

    internal EventBusStoreGauges(
        IServiceScopeFactory scopeFactory,
        bool hasOutbox,
        bool hasInbox,
        bool hasDeadLetters,
        bool exportEnabled,
        EventBusTelemetry telemetry,
        ILogger logger)
        : base(
            "eksen.eventbus.stores",
            cancellationToken => RefreshAsync(scopeFactory, hasOutbox, hasInbox, hasDeadLetters, cancellationToken),
            RefreshInterval,
            logger)
    {
        _isActive = exportEnabled && (hasOutbox || hasInbox || hasDeadLetters);
        if (!_isActive)
        {
            return;
        }

        var meter = telemetry.Meter;
        if (hasOutbox)
        {
            meter.CreateObservableGauge(
                "eksen.eventbus.outbox.messages",
                ObserveOutbox,
                unit: "{message}",
                description: "Outbox messages by state.");
        }

        if (hasInbox)
        {
            meter.CreateObservableGauge(
                "eksen.eventbus.inbox.messages",
                ObserveInbox,
                unit: "{message}",
                description: "Inbox messages by state.");
        }

        if (hasDeadLetters)
        {
            meter.CreateObservableGauge(
                "eksen.eventbus.dead_letter.messages",
                ObserveDeadLetters,
                unit: "{message}",
                description: "Messages held in the dead-letter store.");
        }
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        return _isActive ? base.ExecuteAsync(stoppingToken) : Task.CompletedTask;
    }

    private IEnumerable<Measurement<int>> ObserveOutbox()
    {
        if (!TryGetValue(out var snapshot) || snapshot?.Outbox is not { } stats)
        {
            return [];
        }

        return
        [
            State(stats.Pending, "pending"),
            State(stats.Processing, "processing"),
            State(stats.Processed, "processed"),
            State(stats.Failed, "failed")
        ];
    }

    private IEnumerable<Measurement<int>> ObserveInbox()
    {
        if (!TryGetValue(out var snapshot) || snapshot?.Inbox is not { } stats)
        {
            return [];
        }

        return
        [
            State(stats.Pending, "pending"),
            State(stats.Processing, "processing"),
            State(stats.Processed, "processed"),
            State(stats.Failed, "failed")
        ];
    }

    private IEnumerable<Measurement<int>> ObserveDeadLetters()
    {
        if (!TryGetValue(out var snapshot) || snapshot?.DeadLetters is not { } count)
        {
            return [];
        }

        return [new Measurement<int>(count)];
    }

    private static Measurement<int> State(int value, string state)
    {
        return new Measurement<int>(value, new KeyValuePair<string, object?>(EventBusTelemetry.StateTag, state));
    }

    private static async Task<EventBusStoreSnapshot> RefreshAsync(
        IServiceScopeFactory scopeFactory,
        bool hasOutbox,
        bool hasInbox,
        bool hasDeadLetters,
        CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var services = scope.ServiceProvider;

        var outbox = hasOutbox
            ? await services.GetRequiredService<IOutboxStore>().GetStatsAsync(cancellationToken)
            : null;
        var inbox = hasInbox
            ? await services.GetRequiredService<IInboxStore>().GetStatsAsync(cancellationToken)
            : null;
        int? deadLetters = hasDeadLetters
            ? await services.GetRequiredService<IDeadLetterStore>().GetCountAsync(cancellationToken)
            : null;

        return new EventBusStoreSnapshot(outbox, inbox, deadLetters);
    }
}
