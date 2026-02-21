using Eksen.Entities.Tenants;
using Eksen.Entities.Users;

namespace Eksen.Entities;

public interface IHasCreator<out TCreator, TTenant>
    where TCreator : IEksenUser<TTenant>
    where TTenant : IEksenTenant
{
    public TCreator? Creator { get; }
}