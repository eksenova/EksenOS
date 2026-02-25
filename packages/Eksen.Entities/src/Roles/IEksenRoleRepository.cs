using Eksen.Entities.Tenants;
using Eksen.Repositories;

namespace Eksen.Entities.Roles;

public interface IEksenRoleRepository<TTenant> : IRepository<IEksenRole<TTenant>,
    EksenRoleFilterParameters<TTenant>,
    EksenRoleIncludeOptions<TTenant>>
    where TTenant : IEksenTenant;

public record EksenRoleFilterParameters<TTenant> : BaseFilterParameters<IEksenRole<TTenant>>
    where TTenant : IEksenTenant
{
    public string? SearchFilter { get; set; }
}

public record EksenRoleIncludeOptions<TTenant> : BaseIncludeOptions<IEksenRole<TTenant>>
    where TTenant : IEksenTenant
{
    public bool IncludeTenant { get; set; }
}

