using Eksen.Entities;
using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Ulid;
using Eksen.ValueObjects.Entities;

namespace Eksen.Permissions;

public record RolePermissionId(System.Ulid Value) : UlidEntityId<RolePermissionId>(Value);

public class RolePermission<TRole, TTenant> : IEntity<RolePermissionId, System.Ulid>, IHasTenant<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public RolePermissionId Id { get; private set; }

    public TRole Role { get; private set; }

    public TTenant Tenant { get; private set; }

    public PermissionDefinition Permission { get; private set; }


    private RolePermission()
    {
        Id = RolePermissionId.Empty;
        Role = null!;
        Permission = null!;
        Tenant = null!;
    }

    public RolePermission(
        TRole role,
        PermissionDefinition permissionDefinition,
        TTenant tenant) : this()
    {
        Id = RolePermissionId.NewId();

        Role = role;
        Permission = permissionDefinition;
        Tenant = tenant;
    }
}