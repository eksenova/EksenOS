using Eksen.Entities.Tenants;
using Eksen.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore;

public abstract class EfCoreEksenTenantRepository<TDbContext, TTenant, TFilterParameters, TIncludeOptions>(TDbContext dbContext)
    : EfCoreIdRepository<TDbContext, TTenant, EksenTenantId, System.Ulid, TFilterParameters, TIncludeOptions>(dbContext),
        IEksenTenantRepository<TTenant, TFilterParameters, TIncludeOptions>
    where TDbContext : EksenDbContext
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenTenantFilterParameters<TTenant>, new()
    where TIncludeOptions : EksenTenantIncludeOptions<TTenant>, new();