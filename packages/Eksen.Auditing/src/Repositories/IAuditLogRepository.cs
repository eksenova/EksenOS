using Eksen.Auditing.Entities;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.Repositories;

namespace Eksen.Auditing.Repositories;

public interface IAuditLogRepository
    : IIdRepository<AuditLog, AuditLogId, System.Ulid, AuditLogFilterParameters>;

public record AuditLogFilterParameters : BaseFilterParameters<AuditLog>
{
    public EksenUserId? UserId { get; set; }

    public EksenTenantId? TenantId { get; set; }

    public DateTime? FromTime { get; set; }

    public DateTime? ToTime { get; set; }

    public string? CorrelationId { get; set; }

    public string? SearchFilter { get; set; }
}