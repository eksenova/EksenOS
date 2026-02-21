using Eksen.Ulid;

namespace Eksen.Entities.Tenants;

public sealed record EksenTenantId(System.Ulid Value) : UlidEntityId<EksenTenantId>(Value);