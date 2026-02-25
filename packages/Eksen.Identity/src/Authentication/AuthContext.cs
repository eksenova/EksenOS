using Eksen.Identity.Abstractions;
using Eksen.Identity.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace Eksen.Identity.Authentication;

internal sealed class AuthContext(
    IHttpContextAccessor httpContextAccessor,
    IOptions<IdentityOptions> identityOptions
) : IAuthContext
{
    public bool IsAuthenticated
    {
        get { return User != null; }
    }

    public IAuthContextUser? User
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            return principal != null
                ? AuthContextUser.FromClaimsPrincipal(principal, identityOptions.Value)
                : null;
        }
    }

    public IAuthContextTenant? Tenant
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;

            return principal != null
                ? AuthContextTenant.FromClaimsPrincipal(principal)
                : null;
        }
    }

    public IAuthContextTenant? OriginalTenant
    {
        get
        {
            const string tenantIdClaimType = EksenClaims.OriginalTenantId;
            const string tenantNameClaimType = EksenClaims.OriginalTenantName;

            var principal = httpContextAccessor.HttpContext?.User;

            return principal != null
                ? AuthContextTenant.FromClaimsPrincipal(
                    principal,
                    tenantIdClaimType,
                    tenantNameClaimType)
                : null;
        }
    }

    public bool IsImpersonating
    {
        get
        {
            var principal = httpContextAccessor.HttpContext?.User;
            if (principal == null)
            {
                return false;
            }

            const string tenantIdClaimType = EksenClaims.OriginalTenantId;

            return principal.FindFirst(tenantIdClaimType) != null;
        }
    }

    public UserType UserType
    {
        get
        {
            if (!IsAuthenticated)
            {
                return UserType.Anonymous;
            }

            if (Tenant != null)
            {
                return UserType.Tenant;
            }

            return UserType.Host;
        }
    }

    public bool IsHost
    {
        get { return UserType is UserType.Host; }
    }

    public bool IsTenant
    {
        get { return UserType is UserType.Tenant; }
    }
}