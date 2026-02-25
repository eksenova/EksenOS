using Eksen.Entities.Tenants;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Entities;
using Eksen.ValueObjects.Hashing;

namespace Eksen.Entities.Users;

public interface IEksenUser<out TTenant> : IEntity<EksenUserId, System.Ulid>, IMayHaveTenant<TTenant>
    where TTenant : class, IEksenTenant
{
    EmailAddress? EmailAddress { get; }

    PasswordHash? PasswordHash { get; }

    bool IsActive { get; }

    bool IsPasswordChangeRequired { get; }

    void SetPasswordHash(PasswordHash? passwordHash);

    void SetActive(bool isActive);
}