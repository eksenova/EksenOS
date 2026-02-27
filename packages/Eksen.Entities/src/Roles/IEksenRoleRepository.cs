using Eksen.Entities.Tenants;
using Eksen.Repositories;

namespace Eksen.Entities.Roles;

public interface IEksenRoleRepository<TRole, TTenant> : IEksenRoleRepository<
    TRole,
    TTenant,
    EksenRoleFilterParameters<TRole, TTenant>,
    EksenRoleIncludeOptions<TRole, TTenant>
>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant;

public interface IEksenRoleRepository<TRole, TTenant, in TFilterParameters, in TIncludeOptions> : IIdRepository<
    TRole,
    EksenRoleId,
    System.Ulid,
    TFilterParameters,
    TIncludeOptions
>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenRoleFilterParameters<TRole, TTenant>, new()
    where TIncludeOptions : EksenRoleIncludeOptions<TRole, TTenant>, new();

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

