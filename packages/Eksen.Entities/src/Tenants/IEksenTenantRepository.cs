using Eksen.Repositories;

namespace Eksen.Entities.Tenants;

public interface IEksenTenantRepository : IRepository<
    IEksenTenant,
    EksenTenantFilterParameters,
    EksenTenantIncludeOptions>;

public record EksenTenantFilterParameters : BaseFilterParameters<IEksenTenant>
{
    public string? SearchFilter { get; set; }
}

public record EksenTenantIncludeOptions : BaseIncludeOptions<IEksenTenant>;

