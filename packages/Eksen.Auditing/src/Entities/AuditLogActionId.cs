using Eksen.Ulid;

namespace Eksen.Auditing.Entities;

public sealed record AuditLogActionId(System.Ulid Value) : UlidEntityId<AuditLogActionId>(Value);
