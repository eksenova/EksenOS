namespace Eksen.Permissions.AspNetCore;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Property, AllowMultiple = true)]
public class BindPermissionAttribute(string permissionName) : Attribute
{
    public string PermissionName { get; } = permissionName;
}