using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.ValueObjects.Entities;

namespace Eksen.Auditing.Entities;

public class AuditLog : IEntity<AuditLogId, System.Ulid>
{
    public AuditLogId Id { get; private set; }

    public DateTime LogTime { get; private set; }

    public EksenUserId? UserId { get; private set; }

    public EksenTenantId? TenantId { get; private set; }

    public string? SourceIpAddress { get; private set; }

    public int? SourcePort { get; private set; }

    public string? CorrelationId { get; private set; }

    public TimeSpan? Duration { get; private set; }

    public string? ExceptionMessage { get; private set; }

    public string? Metadata { get; private set; }

    public AuditLogHttpRequest? HttpRequest { get; private set; }

    public void SetSourceIpAddress(string? sourceIpAddress)
        => SourceIpAddress = sourceIpAddress;

    public void SetSourcePort(int? sourcePort)
        => SourcePort = sourcePort;

    public void SetCorrelationId(string? correlationId)
        => CorrelationId = correlationId;

    public ICollection<AuditLogAction> Actions { get; private set; } = [];

    public ICollection<AuditLogEntityChange> EntityChanges { get; private set; } = [];

    private AuditLog() { Id = null!; }

    public AuditLog(
        EksenUserId? userId,
        EksenTenantId? tenantId,
        string? sourceIpAddress,
        int? sourcePort,
        string? correlationId)
    {
        Id = AuditLogId.NewId();
        LogTime = DateTime.UtcNow;
        UserId = userId;
        TenantId = tenantId;
        SourceIpAddress = sourceIpAddress;
        SourcePort = sourcePort;
        CorrelationId = correlationId;
    }

    public void SetDuration(TimeSpan duration)
        => Duration = duration;

    public void SetException(string? exceptionMessage)
        => ExceptionMessage = exceptionMessage;

    public void SetMetadata(string? metadata)
        => Metadata = metadata;

    public void SetHttpRequest(AuditLogHttpRequest httpRequest)
        => HttpRequest = httpRequest;

    public void AddAction(AuditLogAction action)
        => Actions.Add(action);

    public void AddEntityChange(AuditLogEntityChange entityChange)
        => EntityChanges.Add(entityChange);
}
