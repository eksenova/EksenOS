---
name: test-base
description: The EksenOS way to write unit, service, integration, and end-to-end tests with Eksen.TestBase — xUnit base classes that build a DI container from the IEksenBuilder, spin up a real database via Testcontainers, share a bounded SQL Server container pool across an assembly, and host the full ASP.NET Core app behind an HttpClient. Use when you need a test that resolves real Eksen services, runs against a real EF Core schema, or exercises HTTP endpoints end to end.
---

# Test Base (Eksen.TestBase)

The test-base family gives you a ladder of xUnit base classes, each adding one layer of real infrastructure: a plain unit test, a test with a DI container built from `AddEksen`, the same container wired to a real database, and finally the whole ASP.NET Core app behind an `HttpClient`. You inherit the rung you need and override a couple of hooks — the base class owns the `IAsyncLifetime` set-up/tear-down, schema creation, and disposal. The packages target xUnit v3 (`IAsyncLifetime`, `AssemblyFixture`) and use Testcontainers for the database.

All examples use the marketplace's e-commerce running example.

## The four base classes

| Base class | Package | Gives you | Inherit when |
|---|---|---|---|
| `EksenUnitTestBase` | Eksen.TestBase | nothing (an empty marker base) | pure unit tests with no container |
| `EksenServiceTestBase` | Eksen.TestBase | a built `ServiceProvider` from `AddEksen` | you need to resolve real Eksen services |
| `EksenDatabaseTestBase<TDbContext>` | Eksen.TestBase | the above + a created EF Core schema | you need a real database |
| `EksenWebTestBase<TProgram, TDbContext>` | Eksen.TestBase.AspNetCore | a hosted app + `HttpClient` | you exercise HTTP endpoints end to end |

## Unit tests

`EksenUnitTestBase` is an empty base — derive from it to mark a class as a plain unit test (value objects, smart enumerations, domain logic) that needs no DI container:

```csharp
using Eksen.TestBase;

public class OrderNumberTests : EksenUnitTestBase
{
    [Fact]
    public void Parse_Rejects_Blank()
    {
        Should.Throw<EksenException>(() => OrderNumber.Parse("  "));
    }
}
```

## Service tests

`EksenServiceTestBase` implements `IAsyncLifetime`. Its `InitializeAsync` builds a `ServiceCollection`, calls `services.AddEksen(...)` — always wiring `AddUnitOfWork()` for you — runs your `ConfigureEksen`/`ConfigureServices` hooks, then exposes the built `ServiceProvider`. Override `ConfigureEksen` to register the Eksen modules under test:

```csharp
using Eksen.TestBase;
using Microsoft.Extensions.DependencyInjection;

public class OrderPricingTests : EksenServiceTestBase
{
    protected override void ConfigureEksen(IEksenBuilder builder)
    {
        base.ConfigureEksen(builder);
        builder.AddDistributedTransactions();   // whatever module the test exercises
    }

    [Fact]
    public void Resolves_The_Service_Under_Test()
    {
        var pricing = ServiceProvider.GetRequiredService<IOrderPricingService>();
        pricing.ShouldNotBeNull();
    }
}
```

The hooks, in call order:

| Hook | Signature | Purpose |
|---|---|---|
| `ConfigureEksen` | `void (IEksenBuilder builder)` | add Eksen modules (`AddUnitOfWork` is already wired) |
| `ConfigureServices` | `Task (ServiceCollection services)` | register plain services, replace registrations |
| `OnServiceProviderBuiltAsync` | `Task ()` | run set-up after `ServiceProvider` exists |
| `DisposeAsync` | `ValueTask ()` | tear-down |

`ServiceProvider` is the seam for the whole test: resolve repositories, the `IUnitOfWork`, and your services from it. Configure `AddEksen` with the same builder calls your production composition root uses — see the **core** skill for the `IEksenBuilder` root and **unit-of-work** for the transaction boundary the base class enables.

## Database tests

`EksenDatabaseTestBase<TDbContext>` adds a real database to the service test. You supply two things: a connection string and the EF Core provider configuration. The base then builds the provider via `AddEntityFrameworkCore` and calls `EnsureCreatedAsync` on your context so every test starts against a live schema:

```csharp
protected abstract Task<string> GetConnectionStringAsync();

protected abstract void ConfigureDbContext(
    IEksenEntityFrameworkCoreBuilder efCoreBuilder,
    string connectionString);
```

A SQLite-in-memory variant for the e-commerce `ShopDbContext` (one shared-cache database per test, kept alive by an open connection):

```csharp
using Eksen.TestBase;
using Microsoft.Data.Sqlite;

public abstract class ShopDbSqliteTestBase : EksenDatabaseTestBase<ShopDbContext>
{
    private SqliteConnection? _keepAlive;

    protected override async Task<string> GetConnectionStringAsync()
    {
        var connectionString =
            "DataSource=file:shop-tests-" + Guid.NewGuid().ToString("N") + "?mode=memory&cache=shared";
        _keepAlive = new SqliteConnection(connectionString);
        await _keepAlive.OpenAsync();
        return connectionString;
    }

    protected override void ConfigureDbContext(
        IEksenEntityFrameworkCoreBuilder efCoreBuilder,
        string connectionString)
    {
        efCoreBuilder.UseSqliteDbContext<ShopDbContext>(connectionString);
    }

    public override async ValueTask DisposeAsync()
    {
        if (_keepAlive is not null)
        {
            await _keepAlive.DisposeAsync();
        }
        await base.DisposeAsync();
    }
}
```

A concrete test resolves the repository from `ServiceProvider` and round-trips an aggregate through the real schema:

```csharp
public class OrderRepositoryTests : ShopDbSqliteTestBase
{
    [Fact]
    public async Task Persists_And_Reloads_An_Order()
    {
        var ordersRepository = ServiceProvider.GetRequiredService<IOrderRepository>();

        var order = new Order(OrderNumber.Parse("ORD-1001"), CustomerId.NewId());
        await ordersRepository.InsertAsync(order);

        var reloaded = await ordersRepository.GetAsync(order.Id);
        reloaded.Status.ShouldBe(OrderStatus.Pending);
    }
}
```

`TDbContext` is your application context (derive it from `EksenDbContext`) and `IOrderRepository` your repository abstraction — see the **entity-framework-core** and **repositories** skills. For the Sqlite/SqlServer provider extension methods (`UseSqliteDbContext`, `UseSqlServerDbContext`) see **entity-framework-core**.

## Real SQL Server with a shared pool

`Eksen.TestBase.SqlServer` runs database tests against actual SQL Server containers without one container per test. `EksenSqlServerTestBase<TDbContext>` draws an idle worker from an assembly-shared `SqlServerWorkerPool`, hands you its freshly cleaned `ConnectionString`, configures the provider with `UseSqlServerDbContext`, and returns the worker to the pool on disposal.

Register the pool once for the whole test assembly with xUnit's `AssemblyFixture`:

```csharp
using Eksen.TestBase.SqlServer;
using Xunit;

[assembly: AssemblyFixture(typeof(SqlServerWorkerPool))]
```

The pool is then injected into each derived base via its constructor:

```csharp
using Eksen.TestBase.SqlServer;

public abstract class ShopDbSqlServerTestBase(
    SqlServerWorkerPool pool
) : EksenSqlServerTestBase<ShopDbContext>(pool);

public class OrderRepositorySqlServerTests(
    SqlServerWorkerPool pool
) : ShopDbSqlServerTestBase(pool)
{
    [Fact]
    public async Task Captures_A_Payment_Against_An_Order()
    {
        var ordersRepository = ServiceProvider.GetRequiredService<IOrderRepository>();
        // ConnectionString is available if you need raw ADO.NET assertions
        ConnectionString.ShouldNotBeNullOrEmpty();
        // ... exercise the aggregate against real SQL Server ...
    }
}
```

Between leases each worker is wiped (all foreign keys and user tables dropped) and the base re-runs `EnsureCreatedAsync`, so every test sees an empty `ShopDbContext` schema.

### Tuning the pool

`SqlServerTestEnvironment` reads two environment variables; the pool size is clamped to `[1, MaxWorkersHardCap]` (hard cap 10):

| Variable (`SqlServerTestEnvironment` const) | Controls | Default |
|---|---|---|
| `EKSEN_SQL_MAX_WORKERS` (`MaxWorkersVariable`) | concurrent containers | `MaxWorkersHardCap` = 10 |
| `EKSEN_SQL_CPUSET` (`CpuSetVariable`) | logical CPU set pinned per container | `DefaultCpuSet` = `"0-3"` |

The CPU set is pinned because SQL Server 2025 asserts on an odd logical-CPU count — keep it an even set.

### Sharing the pool with an outside host

When a host you stand up yourself (for example a `WebApplicationFactory`) needs the same bounded pool instead of its own containers, lease a connection with `SqlServerWorkerPool.LeaseConnectionAsync()`, point the host's `DbContext` at the lease's `ConnectionString`, and dispose the `SqlServerConnectionLease` in tear-down to return the worker:

```csharp
await using var lease = await pool.LeaseConnectionAsync();
// configure the host's ShopDbContext with lease.ConnectionString
```

## End-to-end web tests

`EksenWebTestBase<TProgram, TDbContext>` hosts the whole application through a `WebApplicationFactory<TProgram>` and exposes an `HttpClient` named `Client`. `TProgram` is the app entry point (`Program`); `TDbContext` is the context the app uses. The base pre-creates the database, removes the app's own `DbContext`/unit-of-work registrations, and re-registers them against your test connection string — so the real pipeline (controllers, EF Core, unit of work) runs against a real schema:

```csharp
using Eksen.TestBase.AspNetCore;
using Eksen.TestBase.SqlServer;

public class OrderEndpointsTests(
    SqlServerWorkerPool pool
) : EksenWebTestBase<Program, ShopDbContext>
{
    private SqlServerConnectionLease? _lease;

    protected override async Task<string> GetConnectionStringAsync()
    {
        _lease = await pool.LeaseConnectionAsync();
        return _lease.ConnectionString;
    }

    protected override void ConfigureDbContext(
        IEksenEntityFrameworkCoreBuilder efCoreBuilder,
        string connectionString)
    {
        efCoreBuilder.UseSqlServerDbContext<ShopDbContext>(connectionString);
    }

    [Fact]
    public async Task Get_Orders_Returns_Ok()
    {
        var response = await Client.GetAsync("/api/orders");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        if (_lease is not null)
        {
            await _lease.DisposeAsync();
        }
    }
}
```

Override hooks for the host:

| Hook | Signature | Purpose |
|---|---|---|
| `ConfigureWebHost` | `void (IWebHostBuilder builder)` | tweak the host (config, replace services) |
| `ConfigureDbContextOptions` | `void (DbContextOptionsBuilder<TDbContext>, string)` | provider for the pre-create step before the factory builds |
| `EnsureDatabaseCreatedAsync` | `Task (string connectionString)` | override the schema pre-creation |

`Factory` is available (protected) if you need to build extra clients or resolve `Factory.Services`.

## Fakes

`AddFakeRandomStringGenerator()` (an `IServiceCollection` extension) replaces the registered `IRandomStringGenerator` with `FakeRandomStringGenerator`, which returns a deterministic run of `'a'` of the requested length. Use it from `ConfigureServices` to make any code that generates random strings (tokens, codes) predictable:

```csharp
protected override async Task ConfigureServices(ServiceCollection services)
{
    await base.ConfigureServices(services);
    services.AddFakeRandomStringGenerator();
}
```

## Errors

Tests assert on real EksenOS errors. A failed value-object parse or a not-found aggregate surfaces as an `EksenException`; assert the exception, not a generic one:

```csharp
[Fact]
public async Task Get_Missing_Order_Throws_NotFound()
{
    var ordersRepository = ServiceProvider.GetRequiredService<IOrderRepository>();
    await Should.ThrowAsync<EksenException>(() => ordersRepository.GetAsync(OrderId.NewId()));
}
```

In a web test, the same domain error is mapped to its HTTP status by the exception handler — see the **error-handling** skill — so assert on `response.StatusCode` instead.

## Checklist

- [ ] Pick the lowest rung that fits: `EksenUnitTestBase` → `EksenServiceTestBase` → `EksenDatabaseTestBase<TDbContext>` → `EksenWebTestBase<TProgram, TDbContext>`.
- [ ] In service tests, register modules in `ConfigureEksen` (call `base` first); `AddUnitOfWork` is already wired.
- [ ] In database tests, implement `GetConnectionStringAsync` and `ConfigureDbContext`; let the base `EnsureCreated` the schema.
- [ ] For real SQL Server, add `[assembly: AssemblyFixture(typeof(SqlServerWorkerPool))]` and take `SqlServerWorkerPool` in the constructor.
- [ ] Tune the pool with `EKSEN_SQL_MAX_WORKERS` / `EKSEN_SQL_CPUSET`; keep the CPU set even.
- [ ] Share the pool with a self-hosted app via `LeaseConnectionAsync()` and dispose the `SqlServerConnectionLease`.
- [ ] In web tests, drive the app through `Client`; override `ConfigureWebHost` only for host tweaks.
- [ ] Use `AddFakeRandomStringGenerator()` to make random-string generation deterministic.
- [ ] Resolve everything from `ServiceProvider`; assert `EksenException` in code and `StatusCode` over HTTP.
