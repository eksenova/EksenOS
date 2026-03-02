using System.Security.Claims;
using Eksen.Identity.Claims;
using Eksen.Identity.Tenants;

namespace Eksen.Identity.AspNetCore.Authentication;

internal sealed record ClaimsBasedAuthContextTenant(EksenTenantId TenantId, TenantName TenantName) : IAuthContextTenant
{
    public static ClaimsBasedAuthContextTenant? FromClaimsPrincipal(
        ClaimsPrincipal principal)
    {
        return FromClaimsPrincipal(principal,
            EksenClaims.TenantId,
            EksenClaims.TenantName);
    }

    public static ClaimsBasedAuthContextTenant? FromClaimsPrincipal(
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

        return new ClaimsBasedAuthContextTenant(
            new EksenTenantId(tenantUlid),
            TenantName.Parse(tenantName));
    }
}