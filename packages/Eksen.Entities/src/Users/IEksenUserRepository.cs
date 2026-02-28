using Eksen.Entities.Tenants;
using Eksen.Repositories;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Entities.Users;

public interface IEksenUserRepository<TUser, TTenant> : IEksenUserRepository<
    TUser,
    TTenant,
    EksenUserFilterParameters<TUser, TTenant>,
    EksenUserIncludeOptions<TUser, TTenant>
>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant;

public interface IEksenUserRepository<TUser, TTenant, in TFilterParameters, in TIncludeOptions>
    : IIdRepository<
        TUser,
        EksenUserId,
        System.Ulid,
        TFilterParameters,
        TIncludeOptions
    >
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenUserFilterParameters<TUser, TTenant>, new()
    where TIncludeOptions : EksenUserIncludeOptions<TUser, TTenant>, new()
{
    Task<TUser?> FindByEmailAddressAsync(
        EmailAddress emailAddress,
        TIncludeOptions? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<TUser?> FindByIdAsync(
        EksenUserId? userId,
        TIncludeOptions? includeOptions = null,
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