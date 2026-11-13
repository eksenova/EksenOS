using System.Diagnostics.Metrics;

namespace Eksen.Telemetry.Outbound;

/// <summary>
/// Instruments of the <c>Eksen.Http.Outbound</c> meter, recorded by <see cref="OutboundLoggingHandler"/>.
/// </summary>
public sealed class OutboundHttpMetrics
{
    public const string MeterName = EksenTelemetry.HttpOutbound;

    public OutboundHttpMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter = meterFactory.Create(MeterName);

        Requests = Meter.CreateCounter<long>(
            name: "eksen.outbound.http.requests",
            unit: "{request}",
            description: "Outbound HTTP requests. Tagged with server.address, http.request.method, eksen.outbound.target and either http.response.status_code (completed) or error.type (transport failure).");

        RequestDuration = Meter.CreateHistogram<double>(
            name: "eksen.outbound.http.request.duration",
            unit: "s",
            description: "Outbound HTTP request duration. Tagged with server.address, http.request.method and eksen.outbound.target.");

        BytesSent = Meter.CreateCounter<long>(
            name: "eksen.outbound.http.bytes.sent",
            unit: "By",
            description: "Request body bytes sent on outbound HTTP calls.");

        BytesReceived = Meter.CreateCounter<long>(
            name: "eksen.outbound.http.bytes.received",
            unit: "By",
            description: "Response body bytes received on outbound HTTP calls.");

        InFlight = Meter.CreateUpDownCounter<long>(
            name: "eksen.outbound.http.in_flight",
            unit: "{request}",
            description: "Outbound HTTP requests currently in flight. Tagged with server.address and eksen.outbound.target.");
    }

    public Meter Meter { get; }

    public Counter<long> Requests { get; }

    public Histogram<double> RequestDuration { get; }

    public Counter<long> BytesSent { get; }

    public Counter<long> BytesReceived { get; }

    public UpDownCounter<long> InFlight { get; }
}
