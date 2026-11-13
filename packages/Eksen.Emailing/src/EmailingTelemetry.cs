using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Runtime.CompilerServices;
using Eksen.Telemetry;

namespace Eksen.Emailing;

/// <summary>
/// The <c>Eksen.Emailing</c> activity source and meter. Spans and metrics never carry recipients, senders or
/// subjects.
/// </summary>
internal sealed class EmailingTelemetry
{
    public const string OutcomeTag = "outcome";

    private static readonly ConditionalWeakTable<IMeterFactory, EmailingTelemetry> PerFactory = new();
    private static readonly Lazy<EmailingTelemetry> SharedInstance = new(() => new EmailingTelemetry(meterFactory: null));

    public EmailingTelemetry(IMeterFactory? meterFactory = null)
    {
        Meter = meterFactory?.Create(EksenTelemetry.Emailing) ?? new Meter(EksenTelemetry.Emailing);
        Sent = Meter.CreateCounter<long>(
            "eksen.email.sent",
            unit: "{email}",
            description: "Emails handed to the SMTP server, by outcome.");
        SendDuration = Meter.CreateHistogram<double>(
            "eksen.email.send.duration",
            unit: "s",
            description: "Duration of one SMTP send.");
    }

    public static ActivitySource ActivitySource { get; } = new(EksenTelemetry.Emailing);

    public static EmailingTelemetry Shared
    {
        get { return SharedInstance.Value; }
    }

    public Meter Meter { get; }

    public Counter<long> Sent { get; }

    public Histogram<double> SendDuration { get; }

    public static EmailingTelemetry For(IMeterFactory? meterFactory)
    {
        return meterFactory is null
            ? Shared
            : PerFactory.GetValue(meterFactory, static factory => new EmailingTelemetry(factory));
    }
}
