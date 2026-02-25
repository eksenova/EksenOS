using Eksen.Entities.Tenants;
using Eksen.Entities.Users;

namespace Eksen.Entities;

public interface IHasCreator<out TCreator, TTenant>
    where TCreator : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public TCreator? Creator { get; }
}