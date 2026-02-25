using Eksen.Entities;
using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Ulid;
using Eksen.ValueObjects.Entities;

namespace Eksen.Permissions;

public record UserRoleId(System.Ulid Value) : UlidEntityId<UserRoleId>(Value);

public class UserRole<TUser, TRole, TTenant> : IEntity<UserRoleId, System.Ulid>, IHasTenant<TTenant>
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public UserRoleId Id { get; private set; }

    public TUser User { get; private set; }

    public TRole Role { get; private set; }

    public TTenant Tenant { get; private set; }

    private UserRole()
    {
        Id = UserRoleId.Empty;
        User = null!;
        Role = null!;
        Tenant = null!;
    }

    public UserRole(
        TUser user,
        TRole role,
        TTenant tenant) : this()
    {
        Id = UserRoleId.NewId();

        User = user;
        Role = role;
        Tenant = tenant;
    }
}