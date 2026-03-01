using Eksen.Entities.Roles;
using Eksen.Entities.Tenants;
using Eksen.Entities.Users;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore;

public static class ModelBuilderExtensions
{
    public static void ApplyEksenPermissionsConfigurations<TUser, TRole, TTenant>(this ModelBuilder builder)
        where TUser : class, IEksenUser<TTenant>
        where TRole : class, IEksenRole<TTenant>
        where TTenant : class, IEksenTenant
    {
        builder.ApplyConfiguration(new EksenUserPermissionEntityTypeConfiguration<TUser, TTenant>());
        builder.ApplyConfiguration(new EksenRolePermissionEntityTypeConfiguration<TRole, TTenant>());
        builder.ApplyConfiguration(new EksenUserRoleEntityTypeConfiguration<TUser, TRole, TTenant>());
        builder.ApplyConfiguration(new EksenPermissionDefinitionEntityTypeConfiguration());
    }
}