using Microsoft.AspNetCore.Authorization;

namespace Eksen.Permissions.AspNetCore;

public sealed class PermissionAuthorizationRequirement(DefinedPermission permission) : IAuthorizationRequirement
{
    public DefinedPermission Permission { get; } = permission;
}