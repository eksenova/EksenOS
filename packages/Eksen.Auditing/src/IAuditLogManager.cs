using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;

namespace Eksen.Auditing;

public interface IAuditLogManager
{
    IAuditLogScope? CurrentScope { get; }

    IAuditLogScope BeginScope();

    Task SaveAsync(CancellationToken cancellationToken = default);

    Task<AuditLog?> GetAuditLogByIdAsync(
        AuditLogId auditLogId,
        CancellationToken cancellationToken = default);

    Task<ICollection<AuditLog>> GetAuditLogsAsync(
        AuditLogFilterParameters? filterParameters = null,
        CancellationToken cancellationToken = default);

    Task<ICollection<AuditLogEntityChange>> GetEntityChangesAsync<TEntity>(
        string? entityId = null,
        CancellationToken cancellationToken = default);

    Task<ICollection<AuditLogEntityChange>> GetEntityChangesAsync(
        Type entityType,
        string? entityId = null,
        CancellationToken cancellationToken = default);

    Task<ICollection<AuditLogAction>> GetActionsForAuditLogAsync(
        AuditLogId auditLogId,
        CancellationToken cancellationToken = default);

    Task<ICollection<AuditLogPropertyChange>> GetPropertyChangesForEntityChangeAsync(
        AuditLogEntityChangeId entityChangeId,
        CancellationToken cancellationToken = default);
}