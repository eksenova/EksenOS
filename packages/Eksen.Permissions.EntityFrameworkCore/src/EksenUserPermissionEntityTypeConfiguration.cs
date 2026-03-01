using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Permissions.EntityFrameworkCore;

public sealed class
    EksenUserPermissionEntityTypeConfiguration<TUser, TTenant> : IEntityTypeConfiguration<EksenUserPermission<TUser, TTenant>>
    where TUser : class, IEksenUser<TTenant>
    where TTenant : class, IEksenTenant
{
    public void Configure(EntityTypeBuilder<EksenUserPermission<TUser, TTenant>> builder)
    {
        builder.ToTable(name: "UserPermissions");
        builder.ConfigureEksenUserPermission();
    }
}