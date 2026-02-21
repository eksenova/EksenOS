using Eksen.Entities.Tenants;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Entities;

namespace Eksen.Entities.Users;

public interface IEksenUser<out TTenant> : IEntity<EksenUserId, System.Ulid>, IMayHaveTenant<TTenant>
    where TTenant : IEksenTenant
{
    EmailAddress? EmailAddress { get; }

    bool IsActive { get; }

    bool IsPasswordChangeRequired { get; }
}