using System.Diagnostics;
using System.Diagnostics.Metrics;
using Eksen.Telemetry;
using Eksen.TestBase;
using Eksen.UnitOfWork;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace Eksen.EntityFrameworkCore.Tests;

public sealed class DbCommandMetricsInterceptorTests : IAsyncLifetime
{
    private static readonly ActivitySource TestSource = new("Eksen.EntityFrameworkCore.Tests");

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private ServiceProvider _services = null!;

    public async ValueTask InitializeAsync()
    {
        await _connection.OpenAsync();
        var services = new ServiceCollection();
        services.AddEksen(eksen => eksen.AddUnitOfWork().AddEntityFrameworkCore());
        services.AddDbContext<TestDbContext>((provider, options) =>
        {
            var manager = provider.GetRequiredService<IUnitOfWorkManager>();
            var tracker = provider.GetRequiredService<IDbContextTracker>();
            options.UseSqlite(_connection)
                .AddInterceptors(
                    new DbContextTrackerCommandInterceptor(manager, tracker),
                    new DbContextTrackerSaveChangesInterceptor(manager, tracker))
                .UseEksenCommandMetrics(provider);
        });
        services.AddScoped<TestEntityRepository>();
        _services = services.BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true });
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<TestDbContext>().Database.EnsureCreatedAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Records_Command_Duration_With_Sqlite_Tags()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EntityFrameworkCore, _services.GetRequiredService<IMeterFactory>());
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        context.TestEntities.Add(new TestEntity(new TestEntityName("metrics")));
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        (await context.TestEntities.CountAsync(TestContext.Current.CancellationToken)).ShouldBe(1);

        var measurements = collector.MeasurementsOf("eksen.db.command.duration");
        measurements.ShouldNotBeEmpty();
        measurements.ShouldAllBe(measurement => (string?)measurement.Tag("db.system.name") == "sqlite");
        measurements.ShouldContain(measurement => (string?)measurement.Tag("db.operation.name") == "INSERT");
        measurements.ShouldContain(measurement => (string?)measurement.Tag("db.operation.name") == "SELECT");
        measurements.ShouldAllBe(measurement => measurement.Value >= 0);
        measurements.ShouldAllBe(measurement => measurement.Tag("error.type") == null);
        measurements.ShouldAllBe(measurement => measurement.Tag("server.address") == null);
        measurements.ShouldContain(measurement => (string?)measurement.Tag("db.namespace") == "main");
    }

    [Fact]
    public async Task Records_The_Error_Type_Of_A_Failed_Command()
    {
        using var collector = new TelemetryCollector(EksenTelemetry.EntityFrameworkCore, _services.GetRequiredService<IMeterFactory>());
        await using var scope = _services.CreateAsyncScope();
        var context = scope.ServiceProvider.GetRequiredService<TestDbContext>();

        await Should.ThrowAsync<SqliteException>(() =>
            context.Database.ExecuteSqlRawAsync("UPDATE missing_table SET x = 1", TestContext.Current.CancellationToken));

        var failed = collector.MeasurementsOf("eksen.db.command.duration")
            .Single(measurement => measurement.Tag("error.type") != null);
        failed.Tag("error.type").ShouldBe(typeof(SqliteException).FullName);
        failed.Tag("db.operation.name").ShouldBe("UPDATE");
    }

    [Fact]
    public async Task Unit_Of_Work_Adds_Transaction_Events_To_The_Current_Span()
    {
        using var collector = new TelemetryCollector("Eksen.EntityFrameworkCore.Tests");
        await using var scope = _services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var manager = provider.GetRequiredService<IUnitOfWorkManager>();

        using var request = TestSource.StartActivity("request")!;
        var unitOfWork = manager.BeginScope();
        await provider.GetRequiredService<TestEntityRepository>().InsertAsync(
            new TestEntity(new TestEntityName("events")), autoSave: true, cancellationToken: TestContext.Current.CancellationToken);
        await unitOfWork.CommitAsync(TestContext.Current.CancellationToken);

        var names = request.Events.Select(activityEvent => activityEvent.Name).ToList();
        names.ShouldContain("tx.begin");
        names.ShouldContain("tx.commit");
        names.IndexOf("tx.begin").ShouldBeLessThan(names.IndexOf("tx.commit"));
        request.Events.First(activityEvent => activityEvent.Name == "tx.begin").Tags
            .ShouldContain(tag => tag.Key == "eksen.db.context" && Equals(tag.Value, nameof(TestDbContext)));
    }

    [Fact]
    public async Task Rollback_Adds_A_Transaction_Rollback_Event()
    {
        using var collector = new TelemetryCollector("Eksen.EntityFrameworkCore.Tests");
        await using var scope = _services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var manager = provider.GetRequiredService<IUnitOfWorkManager>();

        using var request = TestSource.StartActivity("request")!;
        var unitOfWork = manager.BeginScope();
        await provider.GetRequiredService<TestEntityRepository>().InsertAsync(
            new TestEntity(new TestEntityName("rolled-back")), autoSave: true, cancellationToken: TestContext.Current.CancellationToken);
        await unitOfWork.RollbackAsync(TestContext.Current.CancellationToken);

        request.Events.ShouldContain(activityEvent => activityEvent.Name == "tx.rollback");
    }

    [Theory]
    [InlineData("SELECT * FROM t", "SELECT")]
    [InlineData("  insert into t values (1)", "INSERT")]
    [InlineData("-- comment\nUPDATE t SET x = 1", "UPDATE")]
    [InlineData("/* tag */ DELETE FROM t", "DELETE")]
    [InlineData("(SELECT 1) UNION (SELECT 2)", "SELECT")]
    [InlineData("WITH x AS (SELECT 1) SELECT * FROM x", "WITH")]
    [InlineData("CREATE TABLE t (id int)", "CREATE")]
    [InlineData("", "other")]
    [InlineData(null, "other")]
    [InlineData("-- only a comment", "other")]
    public void Classifies_The_First_Keyword(string? commandText, string expected)
    {
        DbCommandMetricsInterceptor.ClassifyOperation(commandText).ShouldBe(expected);
    }

    [Theory]
    [InlineData("tcp://db.internal:5432", "db.internal")]
    [InlineData("tcp://10.1.0.5:5432", "10.1.0.5")]
    [InlineData("tcp:sql.example.com,1433", "sql.example.com")]
    [InlineData(@"sql01\SQLEXPRESS", "sql01")]
    [InlineData("localhost", "localhost")]
    [InlineData("tcp://[::1]:5432", "::1")]
    [InlineData(":memory:", null)]
    [InlineData("/var/lib/app/data.db", null)]
    [InlineData(@"C:\data\app.db", null)]
    [InlineData("app.sqlite", null)]
    [InlineData("unix:/var/run/postgresql", null)]
    [InlineData("", null)]
    [InlineData(null, null)]
    public void Extracts_The_Server_Address(string? dataSource, string? expected)
    {
        DbCommandMetricsInterceptor.ExtractServerAddress(dataSource).ShouldBe(expected);
    }

    [Theory]
    [InlineData("Npgsql.EntityFrameworkCore.PostgreSQL", "postgresql")]
    [InlineData("Microsoft.EntityFrameworkCore.SqlServer", "mssql")]
    [InlineData("Microsoft.EntityFrameworkCore.Sqlite", "sqlite")]
    [InlineData("Npgsql.NpgsqlConnection", "postgresql")]
    [InlineData("Microsoft.Data.SqlClient.SqlConnection", "mssql")]
    [InlineData("Some.Other.Provider", "other_sql")]
    [InlineData(null, "other_sql")]
    public void Maps_The_Provider_To_The_System_Name(string? provider, string expected)
    {
        DbCommandMetricsInterceptor.MapSystemName(provider).ShouldBe(expected);
    }

    [Fact]
    public async Task Configured_System_Name_Overrides_The_Provider()
    {
        var services = new ServiceCollection();
        services.AddMetrics();
        services.Configure<EksenDbCommandMetricsOptions>(options => options.DbSystemName = "custom");
        services.AddEksen(eksen => eksen.AddUnitOfWork().AddEntityFrameworkCore());
        await using var provider = services.BuildServiceProvider();
        using var collector = new TelemetryCollector(EksenTelemetry.EntityFrameworkCore, provider.GetRequiredService<IMeterFactory>());
        var options = new DbContextOptionsBuilder<TestDbContext>()
            .UseSqlite(_connection)
            .UseEksenCommandMetrics(provider)
            .Options;
        await using var context = new TestDbContext(options);

        await context.TestEntities.CountAsync(TestContext.Current.CancellationToken);

        collector.MeasurementsOf("eksen.db.command.duration").ShouldAllBe(measurement => (string?)measurement.Tag("db.system.name") == "custom");
        collector.MeasurementsOf("eksen.db.command.duration").ShouldNotBeEmpty();
    }
}
