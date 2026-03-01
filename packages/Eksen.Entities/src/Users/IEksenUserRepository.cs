using Eksen.Entities.Tenants;
using Eksen.Repositories;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Entities.Users;

public interface IEksenUserRepository<TUser, TTenant>
    : IIdRepository<
        TUser,
        EksenUserId,
        System.Ulid,
        EksenUserFilterParameters<TUser, TTenant>,
        EksenUserIncludeOptions<TUser, TTenant>
    >
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    Task<TUser?> FindByEmailAddressAsync(
        EmailAddress emailAddress,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<TUser?> FindByIdAsync(
        EksenUserId? userId,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);
}

public record EksenUserIncludeOptions<TUser, TTenant> : BaseIncludeOptions<TUser>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public bool IncludeTenant { get; set; }
}

public record EksenUserFilterParameters<TUser, TTenant> : BaseFilterParameters<TUser>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public string? SearchFilter { get; set; }
}