using Eksen.EntityFrameworkCore;
using Eksen.Identity.EntityFrameworkCore.Roles;
using Eksen.Identity.EntityFrameworkCore.Tenants;
using Eksen.Identity.EntityFrameworkCore.Users;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Permissions.EntityFrameworkCore.Tests.Fakes;

public class PermissionsTestDbContext(DbContextOptions<PermissionsTestDbContext> options) : EksenDbContext(options)
{
    public DbSet<TestTenant> Tenants => Set<TestTenant>();
    public DbSet<TestRole> Roles => Set<TestRole>();
    public DbSet<TestUser> Users => Set<TestUser>();
    public DbSet<PermissionDefinition> PermissionDefinitions => Set<PermissionDefinition>();
    public DbSet<EksenRolePermission<TestRole, TestTenant>> RolePermissions => Set<EksenRolePermission<TestRole, TestTenant>>();
    public DbSet<EksenUserPermission<TestUser, TestTenant>> UserPermissions => Set<EksenUserPermission<TestUser, TestTenant>>();
    public DbSet<EksenUserRole<TestUser, TestRole, TestTenant>> UserRoles => Set<EksenUserRole<TestUser, TestRole, TestTenant>>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TestTenant>(entity =>
        {
            entity.ConfigureEksenTenant();
        });

        modelBuilder.Entity<TestRole>(entity =>
        {
            entity.ConfigureEksenRole<TestRole, TestTenant>();
        });

        modelBuilder.Entity<TestUser>(entity =>
        {
            entity.ConfigureEksenUser<TestUser, TestTenant>();
        });

        modelBuilder.ApplyEksenPermissionsConfigurations<TestUser, TestRole, TestTenant>();
    }
}
