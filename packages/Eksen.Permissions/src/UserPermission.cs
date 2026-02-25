using Eksen.Entities;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Ulid;
using Eksen.ValueObjects.Entities;

namespace Eksen.Permissions;

public record UserPermissionId(System.Ulid Value) : UlidEntityId<UserPermissionId>(Value);

public class UserPermission<TUser, TTenant> : IEntity<UserPermissionId, System.Ulid>, IHasTenant<TTenant>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public UserPermissionId Id { get; private set; }

    public TUser User { get; private set; }

    public TTenant Tenant { get; private set; }

    public PermissionDefinition Permission { get; private set; }


    private UserPermission()
    {
        Id = UserPermissionId.Empty;
        User = null!;
        Permission = null!;
        Tenant = null!;
    }

    public UserPermission(
        TUser user,
        PermissionDefinition permissionDefinition,
        TTenant tenant) : this()
    {
        Id = UserPermissionId.NewId();

        User = user;
        Permission = permissionDefinition;
        Tenant = tenant;
    }
}
