using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Eksen.Permissions.AspNetCore;

internal sealed class PermissionAuthorizationPolicyProvider(
    IOptions<AuthorizationOptions> authorizationOptions,
    IOptions<PermissionOptions> permissionOptions
) : DefaultAuthorizationPolicyProvider(authorizationOptions)
{
    public override async Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        var policy = await base.GetPolicyAsync(policyName);
        if (policy != null)
        {
            return policy;
        }

        var permissions = permissionOptions.Value.Permissions;
        var permission = permissions
            .FirstOrDefault(x => string
                .Equals(
                    x.Name.Value,
                    policyName,
                    StringComparison.OrdinalIgnoreCase));

        if (permission == null)
        {
            return null;
        }

        var policyBuilder = new AuthorizationPolicyBuilder();
        policyBuilder.Requirements.Add(new PermissionAuthorizationRequirement(permission));

        return policyBuilder.Build();
    }
}