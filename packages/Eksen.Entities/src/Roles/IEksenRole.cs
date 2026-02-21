using Eksen.Entities.Tenants;
using Eksen.ValueObjects.Entities;

namespace Eksen.Entities.Roles;

public interface IEksenRole<out TTenant> : IEntity<EksenRoleId, System.Ulid>, IMayHaveTenant<TTenant>
    where TTenant : IEksenTenant
{
    RoleName Name { get; }
}
