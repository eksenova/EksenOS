namespace Eksen.Identity.Abstractions;

public interface IAuthContext
{
    public bool IsAuthenticated { get; }

    public IAuthContextUser? User { get; }

    public IAuthContextTenant? Tenant { get; }

    public IAuthContextTenant? OriginalTenant { get; }

    public bool IsImpersonating { get; }

    public bool IsTenant { get; }

    public bool IsHost { get; }

    public UserType UserType { get; }
}