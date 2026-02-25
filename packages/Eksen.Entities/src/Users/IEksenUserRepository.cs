using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Repositories;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Entities.Users;

public interface IEksenUserRepository<TTenant> : IIdRepository<
    IEksenUser<TTenant>,
    EksenUserId,
    System.Ulid,
    EksenUserIncludeOptions<TTenant>>
    where TTenant : IEksenTenant
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
    where TTenant : IEksenTenant
{
    public bool IncludeTenant { get; set; }
}
