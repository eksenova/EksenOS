namespace Eksen.Telemetry.Redaction;

/// <summary>
/// Application hook that redacts provider-specific secrets from captured outbound bodies after the generic
/// <see cref="SensitiveBodyRedactor"/> ran.
/// </summary>
public interface IOutboundBodyRedactor
{
    string Redact(HttpRequestMessage request, string body, bool isResponse);
}

public sealed class NullOutboundBodyRedactor : IOutboundBodyRedactor
{
    public string Redact(HttpRequestMessage request, string body, bool isResponse)
    {
        return body;
    }
}
