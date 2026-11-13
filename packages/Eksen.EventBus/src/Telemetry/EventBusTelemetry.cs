using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;
using Polly;

namespace Eksen.EventBus.Telemetry;

/// <summary>
/// Instruments of the <c>Eksen.EventBus</c> meter.
/// </summary>
internal sealed class EventBusTelemetry
{
    public const string OutcomeTag = "outcome";
    public const string StateTag = "state";
    public const string RetryAttemptTag = "eksen.retry.attempt";

    public static readonly ResiliencePropertyKey<string> EventTypeProperty = new(EksenSemanticConventions.EksenEventType);
    public static readonly ResiliencePropertyKey<string> HandlerProperty = new(EksenSemanticConventions.EksenHandler);

    private static readonly ConditionalWeakTable<IMeterFactory, EventBusTelemetry> PerFactory = new();
    private static readonly Lazy<EventBusTelemetry> SharedInstance = new(() => new EventBusTelemetry(meterFactory: null));

    public EventBusTelemetry(IMeterFactory? meterFactory = null)
    {
        Meter = meterFactory?.Create(EksenTelemetry.EventBus) ?? new Meter(EksenTelemetry.EventBus);

        OutboxLag = Meter.CreateHistogram<double>(
            "eksen.eventbus.outbox.lag",
            unit: "s",
            description: "Time between an event being written to the outbox and its publication.");
        SentMessages = Meter.CreateCounter<long>(
            "messaging.client.sent.messages",
            unit: "{message}",
            description: "Messages the outbox processor handed to the transport.");
        OperationDuration = Meter.CreateHistogram<double>(
            "messaging.client.operation.duration",
            unit: "s",
            description: "Duration of a transport publish issued by the outbox processor.");
        ConsumedMessages = Meter.CreateCounter<long>(
            "messaging.client.consumed.messages",
            unit: "{message}",
            description: "Messages delivered to an event handler, by outcome.");
        ProcessDuration = Meter.CreateHistogram<double>(
            "messaging.process.duration",
            unit: "s",
            description: "Duration of an event handler, retries included.");
        HandlerRetries = Meter.CreateCounter<long>(
            "eksen.eventbus.handler.retries",
            unit: "{retry}",
            description: "Event handler retries scheduled by the retry pipeline.");
        InboxDuplicates = Meter.CreateCounter<long>(
            "eksen.eventbus.inbox.duplicates",
            unit: "{message}",
            description: "Deliveries skipped because the inbox already holds the event for the handler.");
        DeadLetters = Meter.CreateCounter<long>(
            "eksen.eventbus.dead_letters",
            unit: "{message}",
            description: "Events moved to the dead-letter store.");
    }

    public static EventBusTelemetry Shared
    {
        get { return SharedInstance.Value; }
    }

    public Meter Meter { get; }

    public Histogram<double> OutboxLag { get; }

    public Counter<long> SentMessages { get; }

    public Histogram<double> OperationDuration { get; }

    public Counter<long> ConsumedMessages { get; }

    public Histogram<double> ProcessDuration { get; }

    public Counter<long> HandlerRetries { get; }

    public Counter<long> InboxDuplicates { get; }

    public Counter<long> DeadLetters { get; }

    /// <summary>
    /// Returns the instruments bound to <paramref name="meterFactory"/>, created once per factory, or the shared
    /// instruments when no factory is available.
    /// </summary>
    public static EventBusTelemetry For(IMeterFactory? meterFactory)
    {
        return meterFactory is null
            ? Shared
            : PerFactory.GetValue(meterFactory, static factory => new EventBusTelemetry(factory));
    }
}
