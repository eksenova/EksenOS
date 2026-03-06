using System.Text.Json;
using Eksen.Auditing.Entities;

namespace Eksen.Auditing;

public sealed class AuditLogScope(AuditLog auditLog) : IAuditLogScope
{
    private readonly Dictionary<string, string> _metadata = new();
    private bool _disposed;

    public AuditLog AuditLog { get; } = auditLog;

    public void AddAction(AuditLogAction action)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AuditLog.AddAction(action);
    }

    public void AddEntityChange(AuditLogEntityChange entityChange)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        AuditLog.AddEntityChange(entityChange);
    }

    public void SetMetadata(string key, string value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _metadata[key] = value;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        if (_metadata.Count > 0)
        {
            var existingMetadata = AuditLog.Metadata != null
                ? JsonSerializer.Deserialize<Dictionary<string, string>>(AuditLog.Metadata) ?? new()
                : new Dictionary<string, string>();

            foreach (var (key, value) in _metadata)
            {
                existingMetadata[key] = value;
            }

            AuditLog.SetMetadata(JsonSerializer.Serialize(existingMetadata));
        }

        _disposed = true;
    }
}