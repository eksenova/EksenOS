using Eksen.Ulid;

namespace Eksen.Auditing.Entities;

public sealed record AuditLogId(System.Ulid Value) : UlidEntityId<AuditLogId>(Value);
