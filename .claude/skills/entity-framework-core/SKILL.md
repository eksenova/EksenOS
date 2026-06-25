---
name: entity-framework-core
description: The EksenOS EF Core layer — an EksenDbContext base, EfCoreRepository/EfCoreIdRepository implementations of the Eksen.Repositories abstractions, the unit-of-work provider, automatic creation/modification/soft-delete stamping, global query filters, and SqlServer + Sqlite provider wiring through AddEksen. Use when you need to persist aggregates with EF Core, back an IRepository<> with a real database, or register a DbContext in an EksenOS app.
---

# Entity Framework Core (Eksen.EntityFrameworkCore)

The EF Core family is the **persistence implementation** for EksenOS. It supplies an `EksenDbContext` base, concrete `EfCoreRepository` / `EfCoreIdRepository` classes that implement the `IRepository<>` / `IReadOnlyRepository<>` abstractions from the repositories skill, an EF Core `IUnitOfWorkProvider` for the unit-of-work skill, and SaveChanges interceptors that stamp audit times and turn deletes into soft-deletes. Two satellite packages — `Eksen.EntityFrameworkCore.SqlServer` and `Eksen.EntityFrameworkCore.Sqlite` — register a typed `DbContext` against a provider. You write the domain (entities, value objects, smart enums) once; this layer maps and stores it.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Product`, `Shipment`, `Payment`).

## Registration

Everything starts at the `IEksenBuilder` root. `AddEntityFrameworkCore` registers the unit-of-work provider and DbContext tracker; the nested builder (`IEksenEntityFrameworkCoreBuilder`) is where you bind a `DbContext` to a provider with `UseSqlServerDbContext` or `UseSqliteDbContext`. Register your repositories as ordinary scoped services:

```csharp
services.AddEksen(eksen => eksen
    .AddEntityFrameworkCore(efCore => efCore
        .UseSqlServerDbContext<ShopDbContext>(connectionString)));

services.AddScoped<IOrderRepository, OrderRepository>();
services.AddScoped<ICustomerRepository, CustomerRepository>();
```

Both provider methods are generic over your context and take optional configuration callbacks:

```csharp
efCore.UseSqlServerDbContext<ShopDbContext>(
    connectionString,
    dbContextOptionsAction: db => db.EnableSensitiveDataLogging(),
    sqlServerOptionsAction: sql => sql.CommandTimeout(30));

// SQLite, same shape
efCore.UseSqliteDbContext<ShopDbContext>(connectionString);
```

Each registration wires the SaveChanges interceptors (auto-properties + unit-of-work tracking) onto the context's `DbContextOptions` and registers `TDbContext` as scoped — you do not call `AddDbContext` yourself.

## The DbContext

Derive your context from `EksenDbContext` (which is a `DbContext`) and expose `DbSet<>`s as usual. In `OnModelCreating`, apply your `IEntityTypeConfiguration`s and the EksenOS global query filters:

```csharp
public class ShopDbContext(
    DbContextOptions<ShopDbContext> options
) : EksenDbContext(options)
{
    public DbSet<Order> Orders
    {
        get { return Set<Order>(); }
    }

    public DbSet<Customer> Customers
    {
        get { return Set<Customer>(); }
    }

    public DbSet<Product> Products
    {
        get { return Set<Product>(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfiguration(new OrderTypeConfiguration());
        modelBuilder.ApplyConfiguration(new CustomerTypeConfiguration());

        modelBuilder.ApplyEksenSoftDeleteQueryFilter();
    }
}
```

`ApplyEksenSoftDeleteQueryFilter()` adds a `IsDeleted == false` filter to every `ISoftDelete` entity (`ApplyEksenQueryFilters()` is the umbrella call that includes it). For your own cross-cutting filter, `ApplyQueryFilter<TBase>(...)` composes a predicate onto every entity assignable to `TBase` and **AND**s with any existing filter:

```csharp
// soft-delete + a tenant scope, composed together
modelBuilder.ApplyEksenSoftDeleteQueryFilter();
modelBuilder.ApplyQueryFilter<IMustHaveTenant>(e => e.TenantId == currentTenantId);
```

## Mapping entities

Map strongly-typed IDs, value objects, and smart enums with EF Core `HasConversion`. A ULID `OrderId` persists as its string form (size with `OrderId.Length`); `OrderStatus` persists by `Code` (see the smart-enumerations skill); `Money`/`OrderNumber` convert through their value (see the value-objects and ulid skills):

```csharp
public class OrderTypeConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .HasConversion(v => v.Value.ToString(), v => OrderId.Parse(v))
            .HasMaxLength(OrderId.Length)
            .ValueGeneratedNever()
            .IsRequired();

        builder.Property(x => x.OrderNumber)
            .HasConversion(v => v.Value, v => new OrderNumber(v))
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion(v => v.Code, v => OrderStatus.Parse(v))
            .HasMaxLength(OrderStatus.MaxLength)
            .IsRequired();

        builder.OwnsMany(x => x.Items);   // OrderItem lines
    }
}
```

## Repositories

Back an `IRepository<>` with a concrete EF Core repository. For aggregates with a strongly-typed ID, derive from `EfCoreIdRepository<TDbContext, TEntity, TId, TIdValue>` — the third/fourth type args are the ID type and its underlying value (`System.Ulid` for ULID ids) — and pair it with an interface that extends `IIdRepository<>`:

```csharp
public interface IOrderRepository : IIdRepository<Order, OrderId, System.Ulid>;

public class OrderRepository(
    ShopDbContext dbContext
) : EfCoreIdRepository<ShopDbContext, Order, OrderId, System.Ulid>(dbContext),
    IOrderRepository;
```

For an aggregate without an ID-typed key, or a read-only projection, use `EfCoreRepository<TDbContext, TEntity>` / `EfCoreReadOnlyRepository<TDbContext, TEntity>`. Richer overloads let you plug in custom filter / include / query / pagination / sorting parameter types (those parameter primitives belong to the repositories skill):

```csharp
public class ProductRepository(
    ShopDbContext dbContext
) : EfCoreRepository<ShopDbContext, Product>(dbContext), IProductRepository;
```

The base classes already implement the abstraction surface — `GetAsync`, `FindAsync`, `GetListAsync`, `CountAsync`, `InsertAsync` / `InsertManyAsync`, `UpdateAsync` / `UpdateManyAsync`, `RemoveAsync` / `RemoveManyAsync`. `GetAsync` for a missing row raises `CommonErrors.ObjectNotFound` (an `EksenException`, `ErrorType.NotFound` → 404 at the edge via the error-handling skill); `FindAsync` swallows that one error and returns `null`.

```csharp
var order = await ordersRepository.GetAsync(orderId);          // throws NotFound if absent
var maybe = await ordersRepository.FindAsync(orderId);         // null if absent
var open  = await ordersRepository.GetListAsync();             // default sort: CreationTime DESC
```

Override the protected `ApplyDefaultFilters`, `ApplyDefaultIncludes`, or `ApplyDefaultSorting` hooks to bake in per-repository defaults (e.g. always include `Items`, or only return non-cancelled orders).

## Saving and the unit of work

Write methods take `autoSave`. Pass `autoSave: true` to call `SaveChangesAsync` immediately; pass `false` (the default) to defer the flush to an enclosing unit of work:

```csharp
await ordersRepository.InsertAsync(order, autoSave: true);
```

To make several writes atomic, open a scope from `IUnitOfWorkManager` (unit-of-work skill). The EF Core provider tracks every `EksenDbContext` touched inside the scope; committing flushes them all, and when the scope is transactional the writes are wrapped in a real database transaction that commits atomically. The scope commits when it is disposed (unless you already committed it) — to abort, call `RollbackAsync` on the scope before it leaves the block:

```csharp
await using (unitOfWorkManager.BeginScope(isTransactional: true))
{
    await ordersRepository.InsertAsync(order);     // autoSave: false
    await paymentsRepository.InsertAsync(payment);
}   // flushed and committed on dispose; call RollbackAsync to abort
```

## Auto properties and soft delete

The `AutoPropertiesSaveChangesInterceptor` is wired automatically by the provider registration. On `SaveChanges` it:

- stamps `CreationTime = DateTime.UtcNow` on **added** `IHasCreationTime` entities (when unset);
- stamps `LastModificationTime = DateTime.UtcNow` on **modified** `IHasModificationTime` entities (when unset);
- converts a **delete** of an `ISoftDelete` entity into a modify that sets `IsDeleted = true` instead of issuing a `DELETE`.

So `RemoveAsync` on a soft-deletable `Order` marks it deleted, and the global query filter then hides it from subsequent reads. To read soft-deleted rows, disable filters through query options (`IgnoreQueryFilters`) — see the repositories skill. The entity/time interfaces (`IHasCreationTime`, `IHasModificationTime`, `ISoftDelete`) come from the entities skill; for current-user audit stamping and audit logs, layer the auditing skill on top.

## Providers: SqlServer and Sqlite

The two satellite packages differ only in the provider call:

```csharp
// production / SQL Server
efCore.UseSqlServerDbContext<ShopDbContext>(connectionString);

// tests / local / SQLite
efCore.UseSqliteDbContext<ShopDbContext>(connectionString);
```

Both accept the same `dbContextOptionsAction` plus a provider-specific options callback (`SqlServerDbContextOptionsBuilder` / `SqliteDbContextOptionsBuilder`). Pick the provider per environment; the DbContext, configurations, and repositories are identical across both.

## Testing

Integration tests run against a real database. SQL Server tests derive from `EksenSqlServerTestBase<TDbContext>` (Testcontainers worker pool); SQLite tests use a shared-cache in-memory database. Both configure the context through `ConfigureDbContext` and resolve repositories from `ServiceProvider` (test-base skill):

```csharp
public class OrderRepositoryTests(
    SqlServerWorkerPool pool
) : EksenSqlServerTestBase<ShopDbContext>(pool)
{
    [Fact]
    public async Task InsertAsync_Persists_Order()
    {
        var ordersRepository = ServiceProvider.GetRequiredService<IOrderRepository>();
        var order = new Order(new OrderNumber("ORD-1001"));

        await ordersRepository.InsertAsync(order, autoSave: true);

        var fetched = await ordersRepository.GetAsync(order.Id);
        fetched.OrderNumber.ShouldBe(new OrderNumber("ORD-1001"));
    }

    [Fact]
    public async Task RemoveAsync_Soft_Deletes()
    {
        var ordersRepository = ServiceProvider.GetRequiredService<IOrderRepository>();
        var order = new Order(new OrderNumber("ORD-1002"));
        await ordersRepository.InsertAsync(order, autoSave: true);

        await ordersRepository.RemoveAsync(order, autoSave: true);

        (await ordersRepository.FindAsync(order.Id)).ShouldBeNull();   // hidden by soft-delete filter
    }
}
```

Assert the contract that crosses the database boundary: round-trips through `HasConversion`, that `GetAsync` throws `NotFound` for a missing key while `FindAsync` returns `null`, that transactional scopes commit and roll back, and that soft-deleted rows disappear from default reads.

## Checklist

- [ ] Register with `AddEksen(e => e.AddEntityFrameworkCore(efCore => efCore.UseSqlServerDbContext<TContext>(...)))`; add repositories as scoped services.
- [ ] Derive the context from `EksenDbContext`; apply configurations and `ApplyEksenSoftDeleteQueryFilter()` in `OnModelCreating`.
- [ ] Map ULID ids, value objects, and smart enums with `HasConversion` (size ids by `Id.Length`, enums by `MaxLength`).
- [ ] Back each `IRepository<>`/`IIdRepository<>` with `EfCoreRepository` / `EfCoreIdRepository`; override the `ApplyDefault*` hooks for per-repo defaults.
- [ ] Use `autoSave: true` for single writes; wrap multi-step writes in `unitOfWorkManager.BeginScope(isTransactional: true)`.
- [ ] Let `RemoveAsync` soft-delete `ISoftDelete` entities; pass `IgnoreQueryFilters` query options to read deleted rows.
- [ ] Let missing-row `GetAsync` surface as `CommonErrors.ObjectNotFound` (→ 404 via the error-handling skill); use `FindAsync` when absence is normal.
- [ ] Pick `UseSqlServerDbContext` for production and `UseSqliteDbContext` for local/tests against the same context.
