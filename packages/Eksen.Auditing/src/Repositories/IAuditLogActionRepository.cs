using Eksen.Auditing.Entities;
using Eksen.Repositories;

namespace Eksen.Auditing.Repositories;

public interface IAuditLogActionRepository
    : IIdRepository<AuditLogAction, AuditLogActionId, System.Ulid, AuditLogActionFilterParameters>
{
    Task<ICollection<AuditLogAction>> GetByAuditLogIdAsync(
        AuditLogId auditLogId,
        CancellationToken cancellationToken = default);
}

public record AuditLogActionFilterParameters : BaseFilterParameters<AuditLogAction>
{
    public AuditLogId? AuditLogId { get; set; }

    public string? ServiceType { get; set; }

    public string? MethodName { get; set; }
}