using Eksen.Entities;
using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Ulid;
using Eksen.ValueObjects.Entities;

namespace Eksen.Permissions;

public record EksenUserRoleId(System.Ulid Value) : UlidEntityId<EksenUserRoleId>(Value);

public class EksenUserRole<TUser, TRole, TTenant> : IEntity<EksenUserRoleId, System.Ulid>, IHasTenant<TTenant>
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public EksenUserRoleId Id { get; private set; }

    public TUser User { get; private set; }

    public TRole Role { get; private set; }

    public TTenant Tenant { get; private set; }

    private EksenUserRole()
    {
        Id = EksenUserRoleId.Empty;
        User = null!;
        Role = null!;
        Tenant = null!;
    }

    public EksenUserRole(
        TUser user,
        TRole role,
        TTenant tenant) : this()
    {
        Id = EksenUserRoleId.NewId();

        User = user;
        Role = role;
        Tenant = tenant;
    }
}