using Eksen.Repositories;

namespace Eksen.Entities.Tenants;

public interface IEksenTenantRepository<TTenant> : IIdRepository<
    TTenant,
    EksenTenantId,
    System.Ulid,
    EksenTenantFilterParameters<TTenant>,
    EksenTenantIncludeOptions<TTenant>>
    where TTenant : class, IEksenTenant;

public record EksenTenantFilterParameters<TTenant> : BaseFilterParameters<TTenant>
    where TTenant : class, IEksenTenant
{
    public string? SearchFilter { get; set; }
}

public record EksenTenantIncludeOptions<TTenant> : BaseIncludeOptions<TTenant>
    where TTenant : class, IEksenTenant;
