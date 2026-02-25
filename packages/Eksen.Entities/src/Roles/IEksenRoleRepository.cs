using Eksen.Entities.Tenants;
using Eksen.Repositories;

namespace Eksen.Entities.Roles;

public interface IEksenRoleRepository<TTenant> : IIdRepository<
    IEksenRole<TTenant>,
    EksenRoleId,
    System.Ulid,
    EksenRoleFilterParameters<TTenant>,
    EksenRoleIncludeOptions<TTenant>>
    where TTenant : class, IEksenTenant;

public record EksenRoleFilterParameters<TTenant> : BaseFilterParameters<IEksenRole<TTenant>>
    where TTenant : class, IEksenTenant
{
    public string? SearchFilter { get; set; }
}

public record EksenRoleIncludeOptions<TTenant> : BaseIncludeOptions<IEksenRole<TTenant>>
    where TTenant : class, IEksenTenant
{
    public bool IncludeTenant { get; set; }
}

