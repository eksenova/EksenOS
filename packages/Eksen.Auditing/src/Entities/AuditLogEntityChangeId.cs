using Eksen.Ulid;

namespace Eksen.Auditing.Entities;

public sealed record AuditLogEntityChangeId(System.Ulid Value) : UlidEntityId<AuditLogEntityChangeId>(Value);
