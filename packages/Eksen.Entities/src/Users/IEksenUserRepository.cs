using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Repositories;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Entities.Users;

public interface IEksenUserRepository<TTenant> : IIdRepository<
    IEksenUser<TTenant>,
    EksenUserId,
    System.Ulid,
    EksenUserFilterParameters<TTenant>,
    EksenUserIncludeOptions<TTenant>>
    where TTenant : class, IEksenTenant
{
    Task<IEksenUser<TTenant>?> FindByEmailAddressAsync(
        EmailAddress emailAddress,
        EksenUserIncludeOptions<TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);

    Task<ICollection<EksenUserId>> GetUserIdsForRoleAsync(
        IEksenRole<TTenant> role,
        CancellationToken cancellationToken = default);

    Task<IEksenUser<TTenant>?> FindByIdAsync(
        EksenUserId? userId,
        EksenUserIncludeOptions<TTenant>? includeOptions = null,
        DefaultQueryOptions? queryOptions = null,
        CancellationToken cancellationToken = default);
}

public record EksenUserIncludeOptions<TTenant> : BaseIncludeOptions<IEksenUser<TTenant>>
    where TTenant : class, IEksenTenant
{
    public bool IncludeTenant { get; set; }

    public bool IncludeRole { get; set; }
}

public record EksenUserFilterParameters<TTenant> : DefaultFilterParameters<IEksenUser<TTenant>>
    where TTenant : class, IEksenTenant;