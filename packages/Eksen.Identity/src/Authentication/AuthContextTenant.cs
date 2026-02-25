using System.Security.Claims;
using Eksen.Entities.Tenants;
using Eksen.Identity.Abstractions;
using Eksen.Identity.Claims;

namespace Eksen.Identity.Authentication;

internal sealed record AuthContextTenant(EksenTenantId TenantId, TenantName TenantName) : IAuthContextTenant
{
    public static AuthContextTenant? FromClaimsPrincipal(
        ClaimsPrincipal principal)
    {
        return FromClaimsPrincipal(principal,
            EksenClaims.TenantId,
            EksenClaims.TenantName);
    }

    public static AuthContextTenant? FromClaimsPrincipal(
        ClaimsPrincipal principal,
        string tenantIdClaimType,
        string tenantNameClaimType)
    {
        var tenantId = principal.FindFirstValue(tenantIdClaimType);
        if (tenantId == null || !System.Ulid.TryParse(tenantId, out var tenantUlid))
        {
            return null;
        }

        var tenantName = principal.FindFirstValue(tenantNameClaimType);
        if (string.IsNullOrWhiteSpace(tenantName))
        {
            return null;
        }

        return new AuthContextTenant(
            new EksenTenantId(tenantUlid),
            TenantName.Parse(tenantName));
    }
}