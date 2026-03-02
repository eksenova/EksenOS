using Eksen.Identity.Tenants;
using Eksen.Identity.Users;

namespace Eksen.Identity;

public interface IHasCreator<out TCreator, TTenant>
    where TCreator : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public TCreator? Creator { get; }
}