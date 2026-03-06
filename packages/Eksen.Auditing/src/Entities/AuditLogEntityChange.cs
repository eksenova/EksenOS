using Eksen.ValueObjects.Entities;

namespace Eksen.Auditing.Entities;

public class AuditLogEntityChange : IEntity<AuditLogEntityChangeId, System.Ulid>
{
    public AuditLogEntityChangeId Id { get; private set; }

    public AuditLogId AuditLogId { get; private set; }

    public DateTime ChangeTime { get; private set; }

    public EntityChangeType ChangeType { get; private set; }

    public string EntityTypeFullName { get; private set; }

    public string? EntityId { get; private set; }

    public ICollection<AuditLogPropertyChange> PropertyChanges { get; private set; } = [];

    private AuditLogEntityChange()
    {
        Id = null!;
        AuditLogId = null!;
        EntityTypeFullName = null!;
    }

    public AuditLogEntityChange(
        AuditLogId auditLogId,
        EntityChangeType changeType,
        string entityTypeFullName,
        string? entityId)
    {
        Id = AuditLogEntityChangeId.NewId();
        AuditLogId = auditLogId;
        ChangeTime = DateTime.UtcNow;
        ChangeType = changeType;
        EntityTypeFullName = entityTypeFullName;
        EntityId = entityId;
    }

    public void AddPropertyChange(AuditLogPropertyChange propertyChange)
        => PropertyChanges.Add(propertyChange);
}