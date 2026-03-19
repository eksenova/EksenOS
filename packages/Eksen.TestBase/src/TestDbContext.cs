using Eksen.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Eksen.TestBase;

public class TestDbContext(DbContextOptions<TestDbContext> options) : EksenDbContext(options)
{
    public DbSet<TestEntity> TestEntities
    {
        get { return Set<TestEntity>(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new TestEntityTypeConfiguration());
        modelBuilder.ApplyEksenSoftDeleteQueryFilter();
    }
}