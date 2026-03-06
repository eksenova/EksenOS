using Eksen.Auditing.Entities;
using Eksen.Auditing.Repositories;
using Eksen.Identity;
using Microsoft.Extensions.Options;

namespace Eksen.Auditing;

public class AuditLogManager(
    IAuthContext authContext,
    IAuditLogRepository auditLogRepository,
    IAuditLogActionRepository auditLogActionRepository,
    IAuditLogEntityChangeRepository auditLogEntityChangeRepository,
    IAuditLogPropertyChangeRepository auditLogPropertyChangeRepository,
    IOptions<EksenAuditingOptions> options) : IAuditLogManager
{
    private AuditLogScope? _currentScope;

    public IAuditLogScope? CurrentScope
    {
        get { return _currentScope; }
    }

    public IAuditLogScope BeginScope()
    {
        var auditLog = new AuditLog(
            authContext.User?.UserId,
            authContext.Tenant?.TenantId,
            sourceIpAddress: null,
            sourcePort: null,
            correlationId: null);

        _currentScope = new AuditLogScope(auditLog);
        return _currentScope;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        if (_currentScope == null || !options.Value.IsEnabled)
            return;

        await auditLogRepository.InsertAsync(_currentScope.AuditLog, autoSave: true, cancellationToken);
        _currentScope = null;
    }

    public async Task<AuditLog?> GetAuditLogByIdAsync(
        AuditLogId auditLogId,
        CancellationToken cancellationToken = default)
    {
        return await auditLogRepository.FindAsync(auditLogId, cancellationToken: cancellationToken);
    }

    public async Task<ICollection<AuditLog>> GetAuditLogsAsync(
        AuditLogFilterParameters? filterParameters = null,
        CancellationToken cancellationToken = default)
    {
        return await auditLogRepository.GetListAsync(
            filterParameters,
            cancellationToken: cancellationToken);
    }

    public Task<ICollection<AuditLogEntityChange>> GetEntityChangesAsync<TEntity>(
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        return GetEntityChangesAsync(typeof(TEntity), entityId, cancellationToken);
    }

    public async Task<ICollection<AuditLogEntityChange>> GetEntityChangesAsync(
        Type entityType,
        string? entityId = null,
        CancellationToken cancellationToken = default)
    {
        return await auditLogEntityChangeRepository.GetByEntityAsync(
            entityType.FullName!,
            entityId,
            cancellationToken);
    }

    public async Task<ICollection<AuditLogAction>> GetActionsForAuditLogAsync(
        AuditLogId auditLogId,
        CancellationToken cancellationToken = default)
    {
        return await auditLogActionRepository.GetByAuditLogIdAsync(auditLogId, cancellationToken);
    }

    public async Task<ICollection<AuditLogPropertyChange>> GetPropertyChangesForEntityChangeAsync(
        AuditLogEntityChangeId entityChangeId,
        CancellationToken cancellationToken = default)
    {
        return await auditLogPropertyChangeRepository.GetByEntityChangeIdAsync(entityChangeId, cancellationToken);
    }
}