using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Authentication.ApiKeys.Identity.EntityFrameworkCore;

public static class ModelBuilderExtensions
{
    public static void ApplyEksenApiKeyConfigurations<TUser, TTenant>(this ModelBuilder builder)
        where TUser : class, IEksenUser<TTenant>
        where TTenant : class, IEksenTenant
    {
        builder.ApplyConfiguration(new EksenUserApiKeyEntityTypeConfiguration<TUser, TTenant>());
    }
}
