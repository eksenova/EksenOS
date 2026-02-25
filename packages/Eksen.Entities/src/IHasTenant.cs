using Eksen.Entities.Tenants;

namespace Eksen.Entities;

public interface IHasTenant<out TTenant> where TTenant : class, IEksenTenant
{
    public TTenant Tenant { get; }
}

public interface IMayHaveTenant<out TTenant> where TTenant : class, IEksenTenant
{
    public TTenant? Tenant { get; }
}