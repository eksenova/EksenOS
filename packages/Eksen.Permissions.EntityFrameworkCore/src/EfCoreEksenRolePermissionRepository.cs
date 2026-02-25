using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore;

public class EfCoreEksenRolePermissionRepository<TDbContext, TRole, TTenant>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, EksenRolePermission<TRole, TTenant>, EksenRolePermissionId, System.Ulid>(dbContext),
        IEksenRolePermissionRepository<TRole, TTenant>
    where TDbContext : EksenDbContext
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public async Task<ICollection<PermissionDefinition>> GetByRoleIdAsync(EksenRoleId roleId, CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable()
            .Where(x => x.Role.Id == roleId)
            .Select(x => x.PermissionDefinition);

        return await queryable.ToListAsync(cancellationToken);
    }

    public async Task<ICollection<PermissionDefinition>> GetByRoleIdsAsync(
        ICollection<EksenRoleId> roleIds,
        CancellationToken cancellationToken = default)
    {
        var queryable = GetQueryable()
            .Where(x => roleIds.Contains(x.Role.Id))
            .Select(x => x.PermissionDefinition);

        return await queryable.ToListAsync(cancellationToken);
    }
}