using Eksen.EntityFrameworkCore;
using Eksen.Identity.EntityFrameworkCore.Roles;
using Eksen.Identity.EntityFrameworkCore.Tenants;
using Eksen.Identity.EntityFrameworkCore.Users;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Identity.EntityFrameworkCore.Tests.Fakes;

public class IdentityTestDbContext(DbContextOptions<IdentityTestDbContext> options) : EksenDbContext(options)
{
    public DbSet<TestTenant> Tenants => Set<TestTenant>();
    public DbSet<TestRole> Roles => Set<TestRole>();
    public DbSet<TestUser> Users => Set<TestUser>();

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
    }
}
