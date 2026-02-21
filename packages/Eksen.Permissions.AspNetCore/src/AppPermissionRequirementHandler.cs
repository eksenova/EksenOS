using Microsoft.AspNetCore.Authorization;

namespace Eksen.Permissions.AspNetCore;

internal sealed class AppPermissionRequirementHandler(IPermissionChecker permissionChecker)
    : AuthorizationHandler<PermissionAuthorizationRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        PermissionAuthorizationRequirement requirement)
    {
        var permission = requirement.Permission;

        if (await permissionChecker.HasPermissionAsync(permission.Name))
        {
            context.Succeed(requirement);
        }
    }
}