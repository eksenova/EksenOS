using System.Security.Claims;
using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Eksen.Identity.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Eksen.Identity.Authentication;

public class EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>(
    UserManager<TUser> userManager,
    RoleManager<TRole> roleManager,
    IOptions<IdentityOptions> identityOptions)
    : UserClaimsPrincipalFactory<TUser, TRole>(userManager, roleManager, identityOptions)
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    private readonly IOptions<IdentityOptions> _identityOptions = identityOptions;

    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(TUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);

        var emailClaimType = _identityOptions.Value.ClaimsIdentity.EmailClaimType;

        var email = identity.GetClaim(emailClaimType);
        if (!string.IsNullOrWhiteSpace(email))
        {
            identity.AddOrReplace(
                new Claim(emailClaimType, email, ClaimValueTypes.Email)
            );
        }

        if (user.Tenant != null)
        {
            identity.AddIfNotExists(new Claim(EksenClaims.TenantId, user.Tenant.Id.Value.ToString()));
            identity.AddIfNotExists(new Claim(EksenClaims.TenantName, user.Tenant.Name.Value));
        }

        return identity;
    }
}