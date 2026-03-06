using Eksen.ValueObjects.Entities;

namespace Eksen.Auditing.Entities;

public class AuditLogHttpRequestPayload : IEntity<AuditLogHttpRequestPayloadId, System.Ulid>
{
    public AuditLogHttpRequestPayloadId Id { get; private set; }

    public AuditLogHttpRequestId HttpRequestId { get; private set; }

    public string? RequestBody { get; private set; }

    public string? ContentType { get; private set; }

    public long? Size { get; private set; }

    private AuditLogHttpRequestPayload()
    {
        Id = null!;
        HttpRequestId = null!;
    }

    public AuditLogHttpRequestPayload(
        AuditLogHttpRequestId httpRequestId,
        string? requestBody,
        string? contentType,
        long? size)
    {
        Id = AuditLogHttpRequestPayloadId.NewId();
        HttpRequestId = httpRequestId;
        RequestBody = requestBody;
        ContentType = contentType;
        Size = size;
    }
}