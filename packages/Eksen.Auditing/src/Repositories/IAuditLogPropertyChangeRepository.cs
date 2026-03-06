using Eksen.Auditing.Entities;
using Eksen.Repositories;

namespace Eksen.Auditing.Repositories;

public interface IAuditLogPropertyChangeRepository
    : IIdRepository<AuditLogPropertyChange, AuditLogPropertyChangeId, System.Ulid, AuditLogPropertyChangeFilterParameters>
{
    Task<ICollection<AuditLogPropertyChange>> GetByEntityChangeIdAsync(
        AuditLogEntityChangeId entityChangeId,
        CancellationToken cancellationToken = default);
}

public record AuditLogPropertyChangeFilterParameters : BaseFilterParameters<AuditLogPropertyChange>
{
    public AuditLogEntityChangeId? EntityChangeId { get; set; }

    public string? PropertyName { get; set; }
}