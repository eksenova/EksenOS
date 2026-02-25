using Eksen.Entities;
using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Ulid;
using Eksen.ValueObjects.Entities;

namespace Eksen.Permissions;

public record EksenRolePermissionId(System.Ulid Value) : UlidEntityId<EksenRolePermissionId>(Value);

public class EksenRolePermission<TRole, TTenant> : IEntity<EksenRolePermissionId, System.Ulid>, IHasTenant<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public EksenRolePermissionId Id { get; private set; }

    public TRole Role { get; private set; }

    public TTenant Tenant { get; private set; }

    public PermissionDefinition PermissionDefinition { get; private set; }


    private EksenRolePermission()
    {
        Id = EksenRolePermissionId.Empty;
        Role = null!;
        PermissionDefinition = null!;
        Tenant = null!;
    }

    public EksenRolePermission(
        TRole role,
        PermissionDefinition permissionDefinition,
        TTenant tenant) : this()
    {
        Id = EksenRolePermissionId.NewId();

        Role = role;
        PermissionDefinition = permissionDefinition;
        Tenant = tenant;
    }
}