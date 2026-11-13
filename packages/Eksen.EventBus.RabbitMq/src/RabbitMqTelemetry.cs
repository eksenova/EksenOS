using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;

namespace Eksen.EventBus.RabbitMq;

/// <summary>
/// The <c>Eksen.EventBus.RabbitMq</c> activity source and meter.
/// </summary>
internal sealed class RabbitMqTelemetry
{
    public const string MessagingSystemName = "rabbitmq";

    private static readonly ConditionalWeakTable<IMeterFactory, RabbitMqTelemetry> PerFactory = new();
    private static readonly Lazy<RabbitMqTelemetry> SharedInstance = new(() => new RabbitMqTelemetry(meterFactory: null));

    public RabbitMqTelemetry(IMeterFactory? meterFactory = null)
    {
        IsFactoryOwned = meterFactory is not null;
        Meter = meterFactory?.Create(EksenTelemetry.EventBusRabbitMq) ?? new Meter(EksenTelemetry.EventBusRabbitMq);
        Reconnects = Meter.CreateCounter<long>(
            "eksen.rabbitmq.reconnects",
            unit: "{reconnect}",
            description: "Connections to RabbitMQ re-established after a loss, by automatic recovery or by reconnecting.");
    }

    public static ActivitySource ActivitySource { get; } = new(EksenTelemetry.EventBusRabbitMq);

    public static RabbitMqTelemetry Shared
    {
        get { return SharedInstance.Value; }
    }

    /// <summary>
    /// True when the meter belongs to a DI container's <see cref="IMeterFactory"/> and is disposed with it, so
    /// observable instruments registered on it do not outlive their owner.
    /// </summary>
    public bool IsFactoryOwned { get; }

    public Meter Meter { get; }

    public Counter<long> Reconnects { get; }

    public static RabbitMqTelemetry For(IMeterFactory? meterFactory)
    {
        return meterFactory is null
            ? Shared
            : PerFactory.GetValue(meterFactory, static factory => new RabbitMqTelemetry(factory));
    }
}
