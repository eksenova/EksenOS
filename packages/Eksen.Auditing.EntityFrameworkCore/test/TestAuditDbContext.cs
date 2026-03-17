using Eksen.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Eksen.Auditing.EntityFrameworkCore.Tests;

public class TestAuditDbContext(DbContextOptions<TestAuditDbContext> options)
    : EksenDbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyEksenAuditingConfiguration();
    }
}

public abstract class SqliteTestBase : IDisposable
{
    private readonly SqliteConnection _connection;

    protected TestAuditDbContext DbContext { get; }

    protected SqliteTestBase()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TestAuditDbContext>()
            .UseSqlite(_connection)
            .Options;

        DbContext = new TestAuditDbContext(options);
        DbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        DbContext.Dispose();
        _connection.Dispose();
        GC.SuppressFinalize(this);
    }
}
