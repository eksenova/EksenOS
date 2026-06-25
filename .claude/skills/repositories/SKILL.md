---
name: repositories
description: The EksenOS persistence-ignorant data access contract — inject IReadOnlyRepository<TEntity>/IRepository<TEntity> (and their Id-typed variants) and query with strongly-typed filter, include, sorting, pagination, and query-option parameters. Use when an application service needs to load, page, count, insert, update, or remove an aggregate without depending on EF Core or any concrete store.
---

# Repositories (Eksen.Repositories)

A **repository** is the abstraction your domain and application services talk to instead of a `DbContext`. `Eksen.Repositories` defines the contracts only — `IReadOnlyRepository<TEntity>` for queries and `IRepository<TEntity>` for writes, plus the strongly-typed parameter records (`BaseFilterParameters<TEntity>`, `BaseIncludeOptions<TEntity>`, `BaseSortingParameters<TEntity>`, `BasePaginationParameters`, `BaseQueryOptions`) that shape a query. The concrete implementation and DI registration live in the entity-framework-core skill; depend on these interfaces and your code stays persistence-ignorant and unit-testable.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Product`, `Shipment`, `Payment`).

## The two contracts

`TEntity` must be a `class` implementing `Eksen.ValueObjects.Entities.IEntity` (see the entities skill). The read contract carries no write methods; the write contract extends it:

```csharp
using Eksen.Repositories;

public sealed class OrderService(
    IReadOnlyRepository<Product> productsRepository,   // queries only
    IRepository<Order> ordersRepository                // queries + writes
)
{
    // ...
}
```

For aggregates keyed by a ULID identifier (see the ulid skill), use the **Id**-typed contracts, which add `FindAsync(id, ...)` / `GetAsync(id, ...)` overloads:

```csharp
// read-only, keyed by OrderId (a ULID-based IEntityId)
IReadOnlyIdRepository<Order, OrderId, Ulid> ordersRepository;

// read + write, keyed by id
IIdRepository<Order, OrderId, Ulid> ordersRepository;
```

Every contract has overloads that let you swap in custom parameter types; the bare `IRepository<Order>` defaults all five to the `Default…` records (`DefaultFilterParameters<Order>`, `DefaultIncludeOptions<Order>`, `DefaultQueryOptions`, `DefaultPaginationParameters`, `DefaultSortingParameters<Order>`).

## Reading

`IReadOnlyRepository<TEntity>` exposes four query methods. Every parameter after the predicate is optional, and every method takes a trailing `CancellationToken`:

```csharp
// Single entity — returns null when nothing matches
Order? draft = await ordersRepository.FindAsync(o => o.Status == OrderStatus.Pending, cancellationToken: ct);

// Single entity — throws CommonErrors.ObjectNotFound (NotFound) when nothing matches
Order placed = await ordersRepository.GetAsync(o => o.OrderNumber == orderNumber, cancellationToken: ct);

// Many entities
ICollection<Order> recent = await ordersRepository.GetListAsync(
    filterParameters: o => o.Status == OrderStatus.Shipped,
    cancellationToken: ct);

// Count
long pending = await ordersRepository.CountAsync(o => o.Status == OrderStatus.Pending, cancellationToken: ct);
```

The predicate is an `Expression<Func<TEntity, bool>>` — `BaseFilterParameters<TEntity>` defines an **implicit conversion** from a lambda, so `o => o.Status == OrderStatus.Shipped` is accepted wherever a `TFilterParameters` is expected.

On the Id-typed contracts, prefer the id overloads for a primary-key lookup:

```csharp
Order order = await ordersRepository.GetAsync(orderId, cancellationToken: ct);   // throws if missing
Order? maybe = await ordersRepository.FindAsync(orderId, cancellationToken: ct);  // null if missing
```

### Filter parameters

`BaseFilterParameters<TEntity>` holds a `Predicate` and a virtual `ToFilterExpression()`. The implicit conversion covers the common case; construct it explicitly when you build a predicate conditionally:

```csharp
var filter = new DefaultFilterParameters<Order> { Predicate = o => o.CustomerId == customerId };
ICollection<Order> theirs = await ordersRepository.GetListAsync(filter, cancellationToken: ct);
```

### Include options

`BaseIncludeOptions<TEntity>` carries a collection of `Expression<Func<TEntity, object>>` navigation selectors and an `IgnoreAutoIncludes` flag. It converts implicitly from a list or array of selectors:

```csharp
Order order = await ordersRepository.GetAsync(
    orderId,
    includeOptions: new DefaultIncludeOptions<Order>
    {
        Includes = [o => o.Items, o => o.Shipment, o => o.Payment],
    },
    cancellationToken: ct);
```

Set `IgnoreAutoIncludes = true` to suppress navigations the model auto-includes when you only need the root aggregate.

### Sorting

`BaseSortingParameters<TEntity>` holds a dynamic-LINQ `Sorting` string (`"Property direction"`), and converts implicitly from a string:

```csharp
ICollection<Order> newestFirst = await ordersRepository.GetListAsync(
    sortingParameters: "OrderNumber desc",
    cancellationToken: ct);
```

With no sorting supplied, the EF Core implementation falls back to `CreationTime DESC` for entities that implement `IHasCreationTime` (see the auditing and entities skills).

### Pagination

`BasePaginationParameters` is a `SkipCount`/`MaxResultCount` pair (both nullable). Combine it with sorting and a count for a stable page:

```csharp
var page = new DefaultPaginationParameters { SkipCount = 0, MaxResultCount = 25 };

long total = await ordersRepository.CountAsync(o => o.CustomerId == customerId, cancellationToken: ct);
ICollection<Order> firstPage = await ordersRepository.GetListAsync(
    filterParameters: o => o.CustomerId == customerId,
    paginationParameters: page,
    sortingParameters: "CreationTime desc",
    cancellationToken: ct);
```

For a validated, non-negative page index, the `SortingIndex` value object (`SortingIndex.Create(int)` / `SortingIndex.Parse(string)`) rejects negatives with `RepositoriesErrors.NegativeSortingIndex` and unparseable input with `RepositoriesErrors.InvalidSortingIndex`.

### Query options

`BaseQueryOptions` toggles cross-cutting query behaviour:

```csharp
var snapshot = new DefaultQueryOptions
{
    AsNoTracking = true,        // read-only projection, no change tracking
    IgnoreQueryFilters = true,  // bypass global filters, e.g. soft-delete
};

ICollection<Order> everyOrder = await ordersRepository.GetListAsync(queryOptions: snapshot, cancellationToken: ct);
```

`AsNoTracking` is the right default for read-only list/report queries. `IgnoreQueryFilters` reaches past global filters such as the `ISoftDelete` filter (see the entities and auditing skills) — use it deliberately.

## Writing

`IRepository<TEntity>` adds the mutating methods. Each takes an `autoSave` flag (default `false`) and a `CancellationToken`:

```csharp
await ordersRepository.InsertAsync(order, cancellationToken: ct);
await ordersRepository.InsertManyAsync(newOrders, cancellationToken: ct);

await ordersRepository.UpdateAsync(order, cancellationToken: ct);
await ordersRepository.UpdateManyAsync(repricedOrders, cancellationToken: ct);

await ordersRepository.RemoveAsync(order, cancellationToken: ct);
await ordersRepository.RemoveManyAsync(cancelledOrders, cancellationToken: ct);

// predicate overload — remove everything matching a filter
await ordersRepository.RemoveAsync(o => o.Status == OrderStatus.Cancelled, cancellationToken: ct);
```

### autoSave and the unit of work

When `autoSave` is `false` (the default), the repository stages the change but does **not** flush it — the surrounding unit of work commits everything atomically at the boundary (see the unit-of-work skill). Keep `autoSave: false` inside an application-service method so a single request is one transaction:

```csharp
public async Task PlaceAsync(Order order, CancellationToken ct)
{
    await ordersRepository.InsertAsync(order, cancellationToken: ct);   // autoSave: false
    // ... more work, all committed together by the UnitOfWork
}
```

Pass `autoSave: true` only when you genuinely need the write flushed immediately (for example to read back a database-generated value before further work in the same scope).

## Errors

`GetAsync` raises `CommonErrors.ObjectNotFound` (an `EksenException` with `ErrorType.NotFound`) when no row matches — `FindAsync` is just `GetAsync` with that one error swallowed back to `null`. With the error-handling skill's `EksenExceptionHandler` registered, a `NotFound` surfaces as **HTTP 404** at the API edge, so an application service can call `GetAsync` and let a missing aggregate become a 404 without writing any mapping code:

```csharp
// missing order -> CommonErrors.ObjectNotFound -> 404, no try/catch needed
Order order = await ordersRepository.GetAsync(orderId, cancellationToken: ct);
```

This package's own errors are validation errors on the `SortingIndex` value object: `RepositoriesErrors.NegativeSortingIndex` and `RepositoriesErrors.InvalidSortingIndex` (both `ErrorType.Validation` → 400). Raise and assert domain errors via the error-handling skill.

## Custom parameter types

When a query has a recurring, named shape, subclass the parameter record and override `ToFilterExpression()` instead of threading raw predicates everywhere. The generic repository contracts accept it as `TFilterParameters`:

```csharp
public sealed record OpenOrdersOf(
    CustomerId Customer
) : BaseFilterParameters<Order>
{
    public override Expression<Func<Order, bool>> ToFilterExpression()
    {
        return o => o.CustomerId == Customer && o.Status != OrderStatus.Delivered;
    }
}

// IReadOnlyRepository<Order, OpenOrdersOf> ordersRepository;
ICollection<Order> open = await ordersRepository.GetListAsync(new OpenOrdersOf(customerId), cancellationToken: ct);
```

Use the same technique with `BaseIncludeOptions<Order>`, `BaseSortingParameters<Order>`, and `BaseQueryOptions` to lock down include graphs and query behaviour per use case.

## Testing

Because services depend on the interfaces, test them against a fake or an in-memory EF Core repository (see the test-base and entity-framework-core skills). Assert behaviour through the contract — that `GetAsync` throws on a miss, that `FindAsync` returns `null`, and that filters/pagination select the right rows:

```csharp
[Fact]
public async Task GetAsync_Throws_When_Order_Missing()
{
    await Should.ThrowAsync<EksenException>(
        () => ordersRepository.GetAsync(unknownOrderId, cancellationToken: TestContext.Current.CancellationToken));
}

[Fact]
public async Task GetListAsync_Pages_Shipped_Orders()
{
    var page = await ordersRepository.GetListAsync(
        filterParameters: o => o.Status == OrderStatus.Shipped,
        paginationParameters: new DefaultPaginationParameters { SkipCount = 0, MaxResultCount = 10 },
        sortingParameters: "OrderNumber desc",
        cancellationToken: TestContext.Current.CancellationToken);

    page.Count.ShouldBeLessThanOrEqualTo(10);
}
```

## Checklist

- [ ] Depend on `IReadOnlyRepository<TEntity>` for queries and `IRepository<TEntity>` for writes; pick the `…IdRepository` variants when you key by a ULID id.
- [ ] Pass predicates as lambdas — `BaseFilterParameters<TEntity>` converts implicitly; promote recurring queries to a custom record overriding `ToFilterExpression()`.
- [ ] Use `GetAsync` when absence is an error (→ `CommonErrors.ObjectNotFound` → 404) and `FindAsync` when `null` is a valid outcome.
- [ ] Shape lists with `BaseIncludeOptions`, `BaseSortingParameters` (dynamic-LINQ string), and `BasePaginationParameters`; pair `GetListAsync` with `CountAsync` for total counts.
- [ ] Set `AsNoTracking = true` for read-only queries; reach for `IgnoreQueryFilters` only when you mean to bypass soft-delete/global filters.
- [ ] Leave `autoSave: false` so the unit of work commits the request atomically; flush eagerly only when you must read back immediately.
- [ ] Register the concrete repository implementations via the entity-framework-core skill — this package ships contracts only.
