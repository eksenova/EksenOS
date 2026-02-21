using Eksen.Entities.Tenants;

namespace Eksen.Identity.Abstractions;

public interface IAuthContextTenant
{
    public EksenTenantId TenantId { get; }

    public TenantName TenantName { get; }
}