using Eksen.Repositories;

namespace Eksen.Entities.Tenants;

public interface IEksenTenantRepository<TTenant>
    : IEksenTenantRepository<TTenant, EksenTenantFilterParameters<TTenant>, EksenTenantIncludeOptions<TTenant>>
    where TTenant : class, IEksenTenant;

public interface IEksenTenantRepository<TTenant, in TFilterParameters, in TIncludeOptions> : IIdRepository<
    TTenant,
    EksenTenantId,
    System.Ulid,
    TFilterParameters,
    TIncludeOptions>
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenTenantFilterParameters<TTenant>, new()
    where TIncludeOptions : EksenTenantIncludeOptions<TTenant>, new();

public record EksenTenantFilterParameters<TTenant> : BaseFilterParameters<TTenant>
    where TTenant : class, IEksenTenant
{
    public string? SearchFilter { get; set; }
}

public record EksenTenantIncludeOptions<TTenant> : BaseIncludeOptions<TTenant>
    where TTenant : class, IEksenTenant;
