namespace Eksen.Permissions;

[AttributeUsage(AttributeTargets.Field | AttributeTargets.Class, Inherited = false)]
public sealed class DisableTenantSeeding() : Attribute
{
    public bool IsDisabled { get; } = true;

    public DisableTenantSeeding(bool isDisabled) : this()
    {
        IsDisabled = isDisabled;
    }
}
