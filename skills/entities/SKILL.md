---
name: entities
description: The EksenOS base abstractions for audit-time and soft-delete domain entities with Eksen.Entities — implement IHasCreationTime, IHasModificationTime, and ISoftDelete so the EF Core layer stamps timestamps and turns deletes into soft deletes automatically. Use when an aggregate needs "created at / last modified at" tracking or must be hidden rather than physically removed.
---

# Entities (Eksen.Entities)

`Eksen.Entities` is a tiny, dependency-light **contracts** package: three marker interfaces that tag a domain entity as wanting creation-time tracking, modification-time tracking, or soft delete. Implement them on your aggregates and the EksenOS EF Core layer fills the values in for you — you never write the stamping code by hand. The interfaces expose **read-only getters**: domain code reads `CreationTime`/`IsDeleted`; infrastructure writes them.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Product`, `Shipment`, `Payment`).

## The contracts

| Interface | Member | Meaning |
|---|---|---|
| `IHasCreationTime` | `DateTime CreationTime { get; }` | When the row was first inserted. |
| `IHasModificationTime` | `DateTime? LastModificationTime { get; }` | When the row was last updated; `null` until the first update. |
| `ISoftDelete` | `bool IsDeleted { get; }` | Logically deleted; the row stays in the table. |

They live in the `Eksen.Entities` namespace and carry no behaviour — they are pure tags the infrastructure reflects over.

## Implementing the contracts

Compose whichever interfaces an aggregate needs. Keep the setters **private** (or `init`) — the contract is getter-only, and the EF Core interceptor assigns the backing property by name when it persists:

```csharp
using Eksen.Entities;

public class Order : IHasCreationTime, IHasModificationTime, ISoftDelete
{
    public OrderId Id { get; private init; }

    public OrderNumber Number { get; private set; }

    public OrderStatus Status { get; private set; }

    public DateTime CreationTime { get; private set; }

    public DateTime? LastModificationTime { get; private set; }

    public bool IsDeleted { get; private init; }

    private Order()
    {
        Id = OrderId.Empty;
        Number = null!;
        Status = OrderStatus.Pending;
    }

    public Order(OrderNumber number) : this()
    {
        Id = OrderId.NewId();
        Number = number;
    }
}
```

The identity (`OrderId`) comes from the ulid skill; `OrderNumber`/`OrderStatus` from the value-objects and smart-enumerations skills. Mix and match the three tags — a `Product` might be only `ISoftDelete`, while a `Payment` carries the two time interfaces but is never soft-deleted.

## How the values get stamped

The package itself does nothing at runtime — the stamping is done by `AutoPropertiesSaveChangesInterceptor` from the entity-framework-core skill, which inspects `ChangeTracker` entries on every `SaveChanges`:

- **Added** + `IHasCreationTime` whose `CreationTime` is still `default` → sets `CreationTime = DateTime.UtcNow`.
- **Modified** + `IHasModificationTime` whose `LastModificationTime` is still `null` → sets `LastModificationTime = DateTime.UtcNow`.
- **Deleted** + `ISoftDelete` → flips the entry back to **Modified**, sets `IsDeleted = true`, and cascades referenced target entries to **Modified** so they are saved too.

Note the **only-when-unset** rule: an already-populated `CreationTime` or a non-null `LastModificationTime` is left untouched, so you can seed explicit timestamps (e.g. when importing historical orders) and they survive. For richer stamping — who created/modified the row, injectable clock, audited-property opt-out — pair this with the auditing skill, which builds on the same interfaces.

## Soft delete and query filters

Because a soft delete only sets `IsDeleted = true`, the row stays in the table. Apply the global query filter so every `ISoftDelete` entity is hidden by default — this is the `!e.IsDeleted` predicate from `ApplyEksenQueryFilters` in the entity-framework-core skill:

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyEksenQueryFilters(); // adds !IsDeleted to every ISoftDelete entity
}
```

After that, a removed `Order` disappears from normal queries. To read soft-deleted rows (admin views, audits), opt out per query:

```csharp
var cancelled = await dbContext.Orders
    .IgnoreQueryFilters()
    .Where(o => o.IsDeleted)
    .ToListAsync();
```

## Registration

`Eksen.Entities` ships **only contracts** — there is no `AddEntities(...)` call on the `IEksenBuilder` root, and nothing to register in DI. The behaviour is switched on entirely in the EF Core layer: register the interceptor and apply the filters (both covered by the entity-framework-core skill).

```csharp
new DbContextOptionsBuilder<ShopDbContext>()
    .UseSqlServer(connectionString)
    .AddInterceptors(new AutoPropertiesSaveChangesInterceptor());
```

Referencing the package self-registers an `AppModules.Entities` marker (`"Eksen.Entities"`) into `Eksen.Core`'s `AppModuleRegistry` the first time the assembly is touched — useful for module-presence diagnostics, not something you call.

## Testing

Tag a test entity, save through a context that has the interceptor wired, and assert the values were filled in:

```csharp
[Fact]
public async Task Save_Stamps_CreationTime_On_Insert()
{
    var order = new Order(new OrderNumber("ORD-1001"));
    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync();

    order.CreationTime.ShouldNotBe(default);
}

[Fact]
public async Task Remove_Soft_Deletes_Instead_Of_Hard_Deleting()
{
    var order = new Order(new OrderNumber("ORD-1002"));
    dbContext.Orders.Add(order);
    await dbContext.SaveChangesAsync();

    dbContext.Orders.Remove(order);
    await dbContext.SaveChangesAsync();

    dbContext.ChangeTracker.Clear();
    var loaded = await dbContext.Orders.IgnoreQueryFilters().FirstAsync(o => o.Id == order.Id);
    loaded.IsDeleted.ShouldBeTrue();
}
```

Assert the contract behaviour — creation/modification stamps appear, an explicit timestamp is **not** overwritten, and a `Remove` leaves the row present with `IsDeleted == true` (reachable only via `IgnoreQueryFilters()`). The test-base skill gives you the in-memory/SQL context fixtures these tests run on.

## Checklist

- [ ] Implement `IHasCreationTime` / `IHasModificationTime` / `ISoftDelete` on aggregates that need created-at, last-modified-at, or logical-delete tracking.
- [ ] Keep the `CreationTime` / `LastModificationTime` / `IsDeleted` setters `private` or `init`; never set them in domain code.
- [ ] Don't hand-write timestamp/soft-delete logic — register `AutoPropertiesSaveChangesInterceptor` (entity-framework-core skill) and let it stamp.
- [ ] Call `modelBuilder.ApplyEksenQueryFilters()` so `ISoftDelete` rows are hidden by default; use `IgnoreQueryFilters()` to read them back.
- [ ] Seed explicit timestamps when needed — the interceptor only fills unset (`default`/`null`) values.
- [ ] Reach for the auditing skill when you also need who-did-it stamping and an injectable clock.
