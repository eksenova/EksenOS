using Eksen.Entities.Users;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Identity.Abstractions;

public interface IAuthContextUser
{
    public EksenUserId? UserId { get; }

    public EmailAddress? EmailAddress { get; }
}