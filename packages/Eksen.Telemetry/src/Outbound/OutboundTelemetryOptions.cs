namespace Eksen.Telemetry.Outbound;

public static class OutboundRequestOptions
{
    /// <summary>
    /// <see cref="HttpRequestMessage.Options"/> key naming the logical target of a call (for example <c>paypal</c>).
    /// </summary>
    public static readonly HttpRequestOptionsKey<string> Target = new(EksenSemanticConventions.EksenOutboundTarget);

    /// <summary>
    /// <see cref="HttpRequestMessage.Options"/> key that opts a single request in (or out) of body capture.
    /// </summary>
    public static readonly HttpRequestOptionsKey<bool> CaptureBodies = new("eksen.outbound.capture_bodies");

    /// <summary>
    /// <see cref="HttpRequestMessage.Options"/> key carrying the timeout the caller applies to this request, for
    /// callers that set <see cref="HttpClient.Timeout"/> outside the client factory options or cancel with their
    /// own timer. Lets the outbound telemetry classify the resulting cancellation as <c>timeout</c>.
    /// </summary>
    public static readonly HttpRequestOptionsKey<TimeSpan> Timeout = new("eksen.outbound.timeout");

    public static HttpRequestMessage WithOutboundTimeout(this HttpRequestMessage request, TimeSpan timeout)
    {
        request.Options.Set(Timeout, timeout);
        return request;
    }

    public static HttpRequestMessage WithOutboundTarget(this HttpRequestMessage request, string target)
    {
        request.Options.Set(Target, target);
        return request;
    }

    public static HttpRequestMessage WithOutboundBodyCapture(this HttpRequestMessage request, bool capture = true)
    {
        request.Options.Set(CaptureBodies, capture);
        return request;
    }
}

/// <summary>
/// Named HTTP clients whose request and response bodies may be captured. Capture also requires
/// <see cref="EksenTelemetryOptions.CaptureOutboundBodies"/>.
/// </summary>
public sealed class OutboundBodyCaptureOptions
{
    public HashSet<string> Clients { get; } = new(StringComparer.Ordinal);
}
