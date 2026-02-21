using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Repositories;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Entities.Users;

public interface IEksenUserRepository<TTenant> : IRepository<IEksenUser<TTenant>,
    EksenUserFilterParameters<TTenant>,
    EksenUserIncludeOptions<TTenant>,
    EksenUserQueryOptions>
    where TTenant : IEksenTenant
{
    Task<IEksenUser<TTenant>?> FindByEmailAddressAsync(
        EmailAddress emailAddress,
        EksenUserIncludeOptions<TTenant>? includeOptions = null,
        EksenUserQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<ICollection<EksenUserId>> GetUserIdsForRoleAsync(
        IEksenRole<TTenant> role,
        CancellationToken cancellationToken = default);

    Task<IEksenUser<TTenant>?> FindByIdAsync(
        EksenUserId? userId,
        EksenUserIncludeOptions<TTenant>? includeOptions = null,
        EksenUserQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);
}

public record EksenUserFilterParameters<TTenant> : BaseFilterParameters<IEksenUser<TTenant>>
    where TTenant : IEksenTenant
{
    public string? SearchFilter { get; set; }
}

public record EksenUserIncludeOptions<TTenant> : BaseIncludeOptions<IEksenUser<TTenant>>
    where TTenant : IEksenTenant
{
    public bool IncludeTenant { get; set; }
}

public record EksenUserQueryOptions : BaseQueryOptions;
