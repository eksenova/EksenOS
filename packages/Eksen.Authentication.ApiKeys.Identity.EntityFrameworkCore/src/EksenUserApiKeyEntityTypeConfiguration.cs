using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Authentication.ApiKeys.Identity.EntityFrameworkCore;

public sealed class
    EksenUserApiKeyEntityTypeConfiguration<TUser, TTenant> : IEntityTypeConfiguration<EksenUserApiKey<TUser, TTenant>>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public void Configure(EntityTypeBuilder<EksenUserApiKey<TUser, TTenant>> builder)
    {
        builder.ToTable(name: "UserApiKeys");
        builder.ConfigureEksenUserApiKey();
    }
}
