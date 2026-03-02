using Eksen.Identity.Users;
using Eksen.ValueObjects.Emailing;

namespace Eksen.Identity;

public interface IAuthContextUser
{
    public EksenUserId? UserId { get; }

    public EmailAddress? EmailAddress { get; }
}