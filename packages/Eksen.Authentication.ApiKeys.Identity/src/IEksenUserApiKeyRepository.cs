using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.Repositories;

namespace Eksen.Authentication.ApiKeys.Identity;

public interface IEksenUserApiKeyRepository<TUser, TTenant>
    : IIdRepository<EksenUserApiKey<TUser, TTenant>, EksenUserApiKeyId, System.Ulid>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    Task<EksenUserApiKey<TUser, TTenant>?> FindByKeyValueAsync(
        ApiKeyValue keyValue,
        CancellationToken cancellationToken = default);

    Task<ICollection<EksenUserApiKey<TUser, TTenant>>> GetByUserIdAsync(
        EksenUserId userId,
        CancellationToken cancellationToken = default);
}
