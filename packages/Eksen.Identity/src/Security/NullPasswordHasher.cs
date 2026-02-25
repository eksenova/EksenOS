using Microsoft.AspNetCore.Identity;

namespace Eksen.Identity.Security;

internal sealed class NullPasswordHasher<T> : IPasswordHasher<T> where T : class
{
    public string HashPassword(T user, string password)
    {
        throw new NotSupportedException();
    }

    public PasswordVerificationResult VerifyHashedPassword(T user, string hashedPassword, string providedPassword)
    {
        throw new NotSupportedException();

    }
}