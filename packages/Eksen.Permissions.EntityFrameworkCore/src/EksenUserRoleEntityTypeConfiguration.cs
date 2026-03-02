using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Permissions.EntityFrameworkCore;

public sealed class
    EksenUserRoleEntityTypeConfiguration<TUser, TRole, TTenant> : IEntityTypeConfiguration<EksenUserRole<TUser, TRole, TTenant>>
    where TUser : class, IEksenUser<TTenant>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public void Configure(EntityTypeBuilder<EksenUserRole<TUser, TRole, TTenant>> builder)
    {
        builder.ToTable(name: "UserRoles");
        builder.ConfigureEksenUserRole();
    }
}