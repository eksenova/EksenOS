using Eksen.ValueObjects.Entities;

namespace Eksen.Auditing.Entities;

public class AuditLogAction : IEntity<AuditLogActionId, System.Ulid>
{
    public AuditLogActionId Id { get; private set; }

    public AuditLogId AuditLogId { get; private set; }

    public DateTime LogTime { get; private set; }

    public string ServiceType { get; private set; }

    public string MethodName { get; private set; }

    public string? Parameters { get; private set; }

    public string? ReturnValue { get; private set; }

    public string? ExceptionMessage { get; private set; }

    public TimeSpan Duration { get; private set; }

    public string? Metadata { get; private set; }

    private AuditLogAction()
    {
        Id = null!;
        AuditLogId = null!;
        ServiceType = null!;
        MethodName = null!;
    }

    public AuditLogAction(
        AuditLogId auditLogId,
        string serviceType,
        string methodName,
        string? parameters)
    {
        Id = AuditLogActionId.NewId();
        AuditLogId = auditLogId;
        LogTime = DateTime.UtcNow;
        ServiceType = serviceType;
        MethodName = methodName;
        Parameters = parameters;
    }

    public void SetReturnValue(string? returnValue)
        => ReturnValue = returnValue;

    public void SetException(string? exceptionMessage)
        => ExceptionMessage = exceptionMessage;

    public void SetDuration(TimeSpan duration)
        => Duration = duration;

    public void SetMetadata(string? metadata)
        => Metadata = metadata;
}