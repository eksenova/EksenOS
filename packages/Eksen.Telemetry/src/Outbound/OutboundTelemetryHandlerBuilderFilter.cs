using Eksen.Telemetry.Redaction;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Telemetry.Outbound;

/// <summary>
/// Appends an <see cref="OutboundLoggingHandler"/> to every <see cref="IHttpClientFactory"/> client, naming it
/// after the client so the client name becomes the default <c>eksen.outbound.target</c>, and passing it the
/// client's configured <see cref="HttpClient.Timeout"/> so timeouts are told apart from caller cancellations.
/// </summary>
internal sealed class OutboundTelemetryHandlerBuilderFilter(
    ILoggerFactory loggerFactory,
    HeaderRedactor headerRedactor,
    OutboundHttpMetrics metrics,
    IUrlPathScrubber urlPathScrubber,
    IOutboundBodyRedactor bodyRedactor,
    IOptionsMonitor<EksenTelemetryOptions> telemetryOptions,
    IOptionsMonitor<OutboundBodyCaptureOptions> bodyCaptureOptions,
    IOptionsMonitor<HttpClientFactoryOptions> clientFactoryOptions
) : IHttpMessageHandlerBuilderFilter
{
    public Action<HttpMessageHandlerBuilder> Configure(Action<HttpMessageHandlerBuilder> next)
    {
        return builder =>
        {
            next(builder);

            builder.AdditionalHandlers.Add(new OutboundLoggingHandler(
                loggerFactory.CreateLogger<OutboundLoggingHandler>(),
                headerRedactor,
                metrics,
                urlPathScrubber,
                bodyRedactor,
                telemetryOptions,
                bodyCaptureOptions,
                string.IsNullOrEmpty(builder.Name) ? null : builder.Name,
                ResolveClientTimeout(builder.Name)));
        };
    }

    /// <summary>
    /// Replays the client's <see cref="HttpClientFactoryOptions.HttpClientActions"/> on a probe client to read the
    /// timeout the factory will give it. A timeout set on the client after it was created is not seen; such callers
    /// can use <see cref="OutboundRequestOptions.WithOutboundTimeout"/>.
    /// </summary>
    internal TimeSpan? ResolveClientTimeout(string? name)
    {
        try
        {
            var options = clientFactoryOptions.Get(name ?? Options.DefaultName);
            using var probe = new HttpClient(new ProbeHandler(), disposeHandler: true);
            foreach (var action in options.HttpClientActions)
            {
                action(probe);
            }

            return probe.Timeout;
        }
        catch (Exception)
        {
            // A client action that needs a real client must not break handler construction; the default applies.
            return null;
        }
    }

    private sealed class ProbeHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The timeout probe client never sends requests.");
        }
    }
}
