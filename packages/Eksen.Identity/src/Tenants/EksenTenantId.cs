using Eksen.Ulid;

namespace Eksen.Identity.Tenants;

public sealed record EksenTenantId(System.Ulid Value) : UlidEntityId<EksenTenantId>(Value);