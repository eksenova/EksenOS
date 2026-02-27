using Eksen.Entities.Roles;
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

public interface IEksenUserRepository<TUser, TTenant, in TFilterParameters, in TIncludeOptions> : IIdRepository<
    TUser,
    EksenUserId,
    System.Ulid,
    TFilterParameters,
    TIncludeOptions>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
    where TFilterParameters : EksenUserFilterParameters<TUser, TTenant>, new()
    where TIncludeOptions : EksenUserIncludeOptions<TUser, TTenant>, new()
{
    Task<IEksenUser<TTenant>?> FindByEmailAddressAsync(
        EmailAddress emailAddress,
        EksenUserIncludeOptions<TUser, TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<ICollection<EksenUserId>> GetUserIdsForRoleAsync(
        IEksenRole<TTenant> role,
        CancellationToken cancellationToken = default);

    Task<IEksenUser<TTenant>?> FindByIdAsync(
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

    public bool IncludeRole { get; set; }
}

public record EksenUserFilterParameters<TUser, TTenant> : DefaultFilterParameters<TUser>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant;