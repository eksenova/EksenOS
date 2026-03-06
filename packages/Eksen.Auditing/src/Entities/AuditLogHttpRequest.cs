using Eksen.ValueObjects.Entities;

namespace Eksen.Auditing.Entities;

public class AuditLogHttpRequest : IEntity<AuditLogHttpRequestId, System.Ulid>
{
    public AuditLogHttpRequestId Id { get; private set; }

    public AuditLogId AuditLogId { get; private set; }

    public string Method { get; private set; }

    public string Host { get; private set; }

    public string Path { get; private set; }

    public string? QueryString { get; private set; }

    public string? Scheme { get; private set; }

    public string? Protocol { get; private set; }

    public int? StatusCode { get; private set; }

    public string? UserAgent { get; private set; }

    public string? ContentType { get; private set; }

    public AuditLogHttpRequestPayload? Payload { get; private set; }

    private AuditLogHttpRequest()
    {
        Id = null!;
        AuditLogId = null!;
        Method = null!;
        Host = null!;
        Path = null!;
    }

    public AuditLogHttpRequest(
        AuditLogId auditLogId,
        string method,
        string host,
        string path,
        string? queryString,
        string? scheme,
        string? protocol,
        string? userAgent,
        string? contentType)
    {
        Id = AuditLogHttpRequestId.NewId();
        AuditLogId = auditLogId;
        Method = method;
        Host = host;
        Path = path;
        QueryString = queryString;
        Scheme = scheme;
        Protocol = protocol;
        UserAgent = userAgent;
        ContentType = contentType;
    }

    public void SetStatusCode(int statusCode)
        => StatusCode = statusCode;

    public void SetPayload(AuditLogHttpRequestPayload payload)
        => Payload = payload;
}