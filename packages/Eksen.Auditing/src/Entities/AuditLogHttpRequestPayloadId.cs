using Eksen.Ulid;

namespace Eksen.Auditing.Entities;

public sealed record AuditLogHttpRequestPayloadId(System.Ulid Value)
    : UlidEntityId<AuditLogHttpRequestPayloadId>(Value);