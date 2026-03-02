using Eksen.Identity.Tenants;

namespace Eksen.Identity;

public interface IHasTenant<out TTenant> where TTenant : class, IEksenTenant
{
    public TTenant Tenant { get; }
}

public interface IMayHaveTenant<out TTenant> where TTenant : class, IEksenTenant
{
    public TTenant? Tenant { get; }
}