using Eksen.Identity.Tenants;

namespace Eksen.Identity;

public interface IAuthContextTenant
{
    public EksenTenantId TenantId { get; }

    public TenantName TenantName { get; }
}