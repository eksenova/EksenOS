using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore;

public class EfCoreEksenRoleRepository<TDbContext, TRole, TTenant, TFilterParameters, TIncludeOptions>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, TRole, EksenRoleId, System.Ulid, TFilterParameters, TIncludeOptions>(dbContext),
        IEksenRoleRepository<TRole, TTenant, TFilterParameters, TIncludeOptions>
    where TDbContext : EksenDbContext
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenRoleFilterParameters<TRole, TTenant>, new()
    where TIncludeOptions : EksenRoleIncludeOptions<TRole, TTenant>, new();