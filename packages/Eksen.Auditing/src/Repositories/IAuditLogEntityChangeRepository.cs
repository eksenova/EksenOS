using Eksen.Auditing.Entities;
using Eksen.Repositories;

namespace Eksen.Auditing.Repositories;

public interface IAuditLogEntityChangeRepository
    : IIdRepository<AuditLogEntityChange, AuditLogEntityChangeId, System.Ulid, AuditLogEntityChangeFilterParameters>
{
    Task<ICollection<AuditLogEntityChange>> GetByAuditLogIdAsync(
        AuditLogId auditLogId,
        CancellationToken cancellationToken = default);

    Task<ICollection<AuditLogEntityChange>> GetByEntityAsync(
        string entityTypeFullName,
        string? entityId = null,
        CancellationToken cancellationToken = default);
}

public record AuditLogEntityChangeFilterParameters : BaseFilterParameters<AuditLogEntityChange>
{
    public AuditLogId? AuditLogId { get; set; }

    public string? EntityTypeFullName { get; set; }

    public string? EntityId { get; set; }

    public EntityChangeType? ChangeType { get; set; }
}