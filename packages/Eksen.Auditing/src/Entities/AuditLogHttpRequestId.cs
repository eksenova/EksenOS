using Eksen.Ulid;

namespace Eksen.Auditing.Entities;

public sealed record AuditLogHttpRequestId(System.Ulid Value) : UlidEntityId<AuditLogHttpRequestId>(Value);