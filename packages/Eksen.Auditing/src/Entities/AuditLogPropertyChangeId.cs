using Eksen.Ulid;

namespace Eksen.Auditing.Entities;

public sealed record AuditLogPropertyChangeId(System.Ulid Value) : UlidEntityId<AuditLogPropertyChangeId>(Value);