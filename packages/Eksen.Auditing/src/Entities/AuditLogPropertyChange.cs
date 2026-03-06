using Eksen.ValueObjects.Entities;

namespace Eksen.Auditing.Entities;

public class AuditLogPropertyChange : IEntity<AuditLogPropertyChangeId, System.Ulid>
{
    public AuditLogPropertyChangeId Id { get; private set; }

    public AuditLogEntityChangeId EntityChangeId { get; private set; }

    public string PropertyName { get; private set; }

    public string? PropertyTypeFullName { get; private set; }

    public string? OriginalValue { get; private set; }

    public string? NewValue { get; private set; }

    private AuditLogPropertyChange()
    {
        Id = null!;
        EntityChangeId = null!;
        PropertyName = null!;
    }

    public AuditLogPropertyChange(
        AuditLogEntityChangeId entityChangeId,
        string propertyName,
        string? propertyTypeFullName,
        string? originalValue,
        string? newValue)
    {
        Id = AuditLogPropertyChangeId.NewId();
        EntityChangeId = entityChangeId;
        PropertyName = propertyName;
        PropertyTypeFullName = propertyTypeFullName;
        OriginalValue = originalValue;
        NewValue = newValue;
    }
}