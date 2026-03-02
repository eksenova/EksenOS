using Eksen.Identity.Roles;
using Eksen.Identity.Tenants;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Eksen.Permissions.EntityFrameworkCore;

public sealed class
    EksenRolePermissionEntityTypeConfiguration<TRole, TTenant> : IEntityTypeConfiguration<EksenRolePermission<TRole, TTenant>>
    where TRole : class, IEksenRole<TTenant>
    where TTenant : class, IEksenTenant
{
    public void Configure(EntityTypeBuilder<EksenRolePermission<TRole, TTenant>> builder)
    {
        builder.ToTable(name: "RolePermissions");
        builder.ConfigureEksenRolePermission();
    }
}