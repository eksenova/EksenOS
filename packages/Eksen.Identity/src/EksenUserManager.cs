using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Identity;

public class EksenUserManager<TUser, TTenant>(
    IUserStore<TUser> store,
    IOptions<IdentityOptions> optionsAccessor,
    IPasswordHasher<TUser> passwordHasher,
    IEnumerable<IUserValidator<TUser>> userValidators,
    IEnumerable<IPasswordValidator<TUser>> passwordValidators,
    ILookupNormalizer keyNormalizer,
    IdentityErrorDescriber errors,
    IServiceProvider services,
    IHttpContextAccessor httpContextAccessor,
    ILogger<UserManager<TUser>> logger)
    : UserManager<TUser>(store, optionsAccessor, passwordHasher, userValidators,
        passwordValidators, keyNormalizer, errors, services, logger)
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    protected override CancellationToken CancellationToken
    {
        get { return httpContextAccessor.HttpContext?.RequestAborted ?? CancellationToken.None; }
    }
}