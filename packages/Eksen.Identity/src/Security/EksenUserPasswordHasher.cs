using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Microsoft.AspNetCore.Identity;

namespace Eksen.Identity.Security;

public sealed class EksenUserPasswordHasher<TUser, TTenant> : IPasswordHasher<TUser>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public string HashPassword(TUser user, string password)
    {
        var salt = BCrypt.Net.BCrypt.GenerateSalt(workFactor: 10);
        return BCrypt.Net.BCrypt.HashPassword(password, salt);
    }

    public PasswordVerificationResult VerifyHashedPassword(
        TUser user,
        string hashedPassword,
        string providedPassword)
    {
        var passwordValid = BCrypt.Net.BCrypt.Verify(providedPassword, hashedPassword);
        return passwordValid
            ? PasswordVerificationResult.Success
            : PasswordVerificationResult.Failed;
    }
}