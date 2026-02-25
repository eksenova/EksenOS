using Eksen.Entities.Tenants;
using Eksen.Repositories;

namespace Eksen.Entities.Roles;

public interface IEksenRoleRepository<TRole, TTenant> : IIdRepository<
    TRole,
    EksenRoleId,
    System.Ulid,
    EksenRoleFilterParameters<TRole, TTenant>,
    EksenRoleIncludeOptions<TRole, TTenant>>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant;

public record EksenRoleFilterParameters<TRole, TTenant> : BaseFilterParameters<TRole>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public string? SearchFilter { get; set; }
}

public record EksenRoleIncludeOptions<TRole, TTenant> : BaseIncludeOptions<TRole>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public bool IncludeTenant { get; set; }
}

