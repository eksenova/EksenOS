---
name: data-seeding
description: The EksenOS way to populate a database with baseline data using Eksen.DataSeeding — write an IDataSeedContributor per concern, order them with [SeedAfter], register them with AddDataSeeding, and run them all inside one transactional unit of work via IDataSeeder. Use when you need idempotent reference/demo data (default products, a demo customer, an example order) created at startup or in tests.
---

# Data Seeding (Eksen.DataSeeding)

**Data seeding** is the act of putting baseline rows into a store so the application is usable on a fresh database — reference data (a catalogue of default `Product`s), bootstrap records (a system `Customer`), or demo data for a sandbox. In EksenOS you express each unit of seed logic as an `IDataSeedContributor`, declare ordering between contributors with `[SeedAfter]`, and let the `IDataSeeder` run them all inside a single transactional unit of work. The framework gives you ordering, dependency-injection, and one atomic transaction; **idempotency is your contributor's job** — it runs on every call, so it must check before it inserts.

All examples use the marketplace's e-commerce running example (`Customer`, `Product`, `Order`).

## Writing a contributor

Implement `IDataSeedContributor` — one method, `SeedAsync`. The contributor is created through `ActivatorUtilities`, so constructor dependencies (repositories, the current-time provider, anything in DI) are injected for you:

```csharp
using Eksen.DataSeeding;
using Eksen.Repositories;

public sealed class ProductDataSeedContributor(
    IRepository<Product> productsRepository
) : IDataSeedContributor
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        // Idempotency: bail out if the catalogue is already seeded.
        if (await productsRepository.CountAsync(cancellationToken: cancellationToken) > 0)
        {
            return;
        }

        await productsRepository.InsertManyAsync(
        [
            new Product(Sku.Parse("SKU-COFFEE-1KG"), "Single-origin coffee, 1kg", Money.Of(18.50m, "EUR")),
            new Product(Sku.Parse("SKU-MUG-CER"),   "Ceramic mug",              Money.Of(9.00m,  "EUR")),
        ], cancellationToken: cancellationToken);
    }
}
```

You do not pass `autoSave: true` — the seeder calls `SaveChangesAsync` after each contributor (see [Execution model](#execution-model)). Persist through the repository abstractions from the **repositories** skill; back them with EF Core via the **entity-framework-core** skill.

### Idempotency is the contract

`IDataSeeder.SeedAsync` re-runs every registered contributor on every call. There is no "already applied" bookkeeping table. A contributor that inserts unconditionally will duplicate rows on the second run. Always guard with an existence check — `CountAsync`, or `FindAsync` for a specific natural key:

```csharp
public sealed class SystemCustomerDataSeedContributor(
    IIdRepository<Customer, CustomerId, Ulid> customersRepository
) : IDataSeedContributor
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        var email = EmailAddress.Parse("system@shop.example");

        var existing = await customersRepository.FindAsync(
            new DefaultFilterParameters<Customer>(x => x.Email == email),
            cancellationToken: cancellationToken);

        if (existing is not null)
        {
            return;
        }

        await customersRepository.InsertAsync(
            new Customer(email, "System Account"),
            cancellationToken: cancellationToken);
    }
}
```

Build value objects (`Sku`, `Money`, `EmailAddress`) and strongly-typed ids (`CustomerId`) the same way production code does — see the **value-objects** and **ulid** skills.

## Ordering with [SeedAfter]

When one contributor depends on another's data, annotate it with `[SeedAfter(typeof(...))]`. The seeder runs the dependency first, even if it was registered later:

```csharp
[SeedAfter(typeof(ProductDataSeedContributor))]
public sealed class DemoOrderDataSeedContributor(
    IRepository<Order> ordersRepository,
    IRepository<Product> productsRepository
) : IDataSeedContributor
{
    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await ordersRepository.CountAsync(cancellationToken: cancellationToken) > 0)
        {
            return;
        }

        // Safe: ProductDataSeedContributor has already run and saved.
        var coffee = await productsRepository.GetAsync(
            new DefaultFilterParameters<Product>(x => x.Sku == Sku.Parse("SKU-COFFEE-1KG")),
            cancellationToken: cancellationToken);

        var customer = new Customer(EmailAddress.Parse("demo@shop.example"), "Demo Buyer");

        var order = new Order(OrderNumber.Parse("ORD-1001"), customer);
        order.AddItem(coffee.Id, Quantity.Of(2), coffee.Price);

        await ordersRepository.InsertAsync(order, cancellationToken: cancellationToken);
    }
}
```

Declare **one** predecessor per contributor — point `[SeedAfter]` at the contributor whose data this one reads. (Although the attribute is marked `AllowMultiple`, the runner reads it with `GetCustomAttribute<SeedAfterAttribute>()`, which throws if more than one is applied — so declare exactly one and model a multi-step bootstrap as a chain: A ← B ← C.)

`[SeedAfter]` only declares precedence — it does **not** auto-register the target. If you reference a contributor that was never registered, `SeedAsync` throws `InvalidOperationException` ("SeedAfter attribute is defined for … but … is not found"). Register every contributor you depend on. A contributor reached via multiple paths is still executed exactly once.

## Registration

Register through the `IEksenBuilder` root with `AddDataSeeding`, then add your contributor types to `EksenDataSeedingOptions`:

```csharp
services.AddEksen(eksen => eksen
    .AddDataSeeding(seeding => seeding
        .Configure(options =>
        {
            options.Add(typeof(ProductDataSeedContributor));
            options.Add(typeof(SystemCustomerDataSeedContributor));
            options.Add(typeof(DemoOrderDataSeedContributor));
        })));
```

`EksenDataSeedingOptions` offers three ways to enrol contributors:

| Call | Use |
|---|---|
| `options.Add(typeof(T))` | One contributor. |
| `options.AddRange([typeof(A), typeof(B)])` | Several at once. |
| `options.AddAssembly(typeof(ProductDataSeedContributor).Assembly)` | Discover every non-abstract `IDataSeedContributor` class in an assembly. |

`AddAssembly` skips interfaces and abstract classes automatically, and the underlying set is deduplicated — registering the same type twice is harmless. Ordering across discovered contributors is still governed solely by `[SeedAfter]`, so do not rely on declaration order for correctness.

`AddDataSeeding` registers `IDataSeeder` (the `DataSeeder` implementation) as a singleton.

## Running the seeder

Resolve `IDataSeeder` and call `SeedAsync` once during startup. Resolve it from a scope so the contributors' scoped dependencies (repositories, DbContext) are created correctly:

```csharp
var app = builder.Build();

await using (var scope = app.Services.CreateAsyncScope())
{
    var seeder = scope.ServiceProvider.GetRequiredService<IDataSeeder>();
    await seeder.SeedAsync();
}

await app.RunAsync();
```

## Execution model

A single `SeedAsync` call:

1. Opens **one transactional unit-of-work scope** (`isTransactional: true`) for the whole run — see the **unit-of-work** skill.
2. Resolves and orders contributors, honouring `[SeedAfter]` and de-duplicating.
3. For each contributor in turn: runs `SeedAsync`, disposes it if it implements `IDisposable`/`IAsyncDisposable`, then calls `SaveChangesAsync` on the scope.

Because everything shares one transaction, a throw from any contributor rolls the entire run back — you never end up with a half-seeded database. That is also why contributors don't need `autoSave: true`: the seeder flushes after each one. If a contributor allocates an unmanaged resource (an external client, a file handle), implement `IDisposable` or `IAsyncDisposable` on it and the seeder will clean it up after that contributor finishes.

## Errors

- A missing `[SeedAfter]` target raises `InvalidOperationException` at run time. Treat it as a wiring bug: register the referenced contributor.
- Domain validation inside a contributor (an invalid `Sku`, a malformed `EmailAddress`, a broken invariant) surfaces as whatever the domain raises — typically an `EksenException`. Let it propagate; the transaction rolls back. Raise your own domain failures through the **error-handling** skill rather than swallowing them, so a bad seed fails loudly instead of committing partial data.

## Testing

Drive `DataSeeder` directly. Assert ordering and idempotency — the two things the framework guarantees and your contributors must respect:

```csharp
[Fact]
public async Task Seeding_Twice_Does_Not_Duplicate_Products()
{
    await seeder.SeedAsync();
    await seeder.SeedAsync();

    (await productsRepository.CountAsync()).ShouldBe(2);
}

[Fact]
public async Task Demo_Order_Seeds_After_Its_Products()
{
    await seeder.SeedAsync();

    var order = await ordersRepository.GetAsync(
        new DefaultFilterParameters<Order>(x => x.OrderNumber == OrderNumber.Parse("ORD-1001")));
    order.Items.ShouldNotBeEmpty();
}
```

For full integration coverage against a real database and unit-of-work, build the fixture from the **test-base** skill.

## Checklist

- [ ] One `IDataSeedContributor` per concern; inject what you need through the constructor.
- [ ] Make every contributor **idempotent** — guard inserts with `CountAsync`/`FindAsync`; the seeder re-runs them on every call.
- [ ] Don't pass `autoSave: true`; the seeder calls `SaveChangesAsync` after each contributor inside one transaction.
- [ ] Express data dependencies with `[SeedAfter(typeof(...))]`, and register every contributor you depend on (an unregistered target throws `InvalidOperationException`).
- [ ] Register via `AddEksen(...).AddDataSeeding(s => s.Configure(o => o.Add/AddRange/AddAssembly(...)))`.
- [ ] Run `IDataSeeder.SeedAsync()` once at startup, resolved from a `CreateAsyncScope()`.
- [ ] Test ordering and idempotency (seed twice, assert no duplicates).
