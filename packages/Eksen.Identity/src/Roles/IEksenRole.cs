using Eksen.Identity.Tenants;
using Eksen.ValueObjects.Entities;

namespace Eksen.Identity.Roles;

public interface IEksenRole<out TTenant> : IEntity<EksenRoleId, System.Ulid>, IMayHaveTenant<TTenant>
    where TTenant : class, IEksenTenant
{
    RoleName Name { get; }

    void SetName(RoleName roleName);
}
