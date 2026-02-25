using Eksen.Entities;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Ulid;
using Eksen.ValueObjects.Entities;

namespace Eksen.Permissions;

public record EksenUserPermissionId(System.Ulid Value) : UlidEntityId<EksenUserPermissionId>(Value);

public class EksenUserPermission<TUser, TTenant> : IEntity<EksenUserPermissionId, System.Ulid>, IHasTenant<TTenant>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public EksenUserPermissionId Id { get; private set; }

    public TUser User { get; private set; }

    public TTenant Tenant { get; private set; }

    public PermissionDefinition PermissionDefinition { get; private set; }


    private EksenUserPermission()
    {
        Id = EksenUserPermissionId.Empty;
        User = null!;
        PermissionDefinition = null!;
        Tenant = null!;
    }

    public EksenUserPermission(
        TUser user,
        PermissionDefinition permissionDefinition,
        TTenant tenant) : this()
    {
        Id = EksenUserPermissionId.NewId();

        User = user;
        PermissionDefinition = permissionDefinition;
        Tenant = tenant;
    }
}
