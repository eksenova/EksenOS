using System.Security.Claims;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Permissions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Eksen.Identity.Authentication;

public class EksenUserSignInManager<TUser, TTenant>(
    UserManager<TUser> userManager,
    IHttpContextAccessor contextAccessor,
    IUserClaimsPrincipalFactory<TUser> claimsFactory,
    IPermissionCache permissionCache,
    IOptions<IdentityOptions> optionsAccessor,
    ILogger<SignInManager<TUser>> logger,
    IAuthenticationSchemeProvider schemes,
    IUserConfirmation<TUser> confirmation)
    : SignInManager<TUser>(userManager,
        contextAccessor,
        claimsFactory,
        optionsAccessor,
        logger,
        schemes,
        confirmation)
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public override async Task SignInWithClaimsAsync(
        TUser user,
        AuthenticationProperties? authenticationProperties,
        IEnumerable<Claim> additionalClaims)
    {
        await base.SignInWithClaimsAsync(user, authenticationProperties, additionalClaims);
        await permissionCache.InvalidateForUserAsync(user.Id, Context.RequestAborted);
    }
}