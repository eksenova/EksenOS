using Eksen.ValueObjects.Entities;

namespace Eksen.Entities.Tenants;

public interface IEksenTenant : IEntity<EksenTenantId, System.Ulid>
{
    TenantName Name { get; }

    bool IsActive { get; }
}