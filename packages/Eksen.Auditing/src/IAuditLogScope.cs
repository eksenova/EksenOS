using Eksen.Auditing.Entities;

namespace Eksen.Auditing;

public interface IAuditLogScope : IDisposable
{
    AuditLog AuditLog { get; }

    void AddAction(AuditLogAction action);

    void AddEntityChange(AuditLogEntityChange entityChange);

    void SetMetadata(string key, string value);
}