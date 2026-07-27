---
name: unit-of-work
description: The EksenOS way to wrap a business operation in a single commit/rollback boundary with Eksen.UnitOfWork — an IUnitOfWorkManager that opens scoped, optionally transactional IUnitOfWorkScopes across every registered provider, lifecycle callbacks and post-commit actions, the [UnitOfWork] attribute, and ASP.NET Core per-request units of work via UseUnitOfWork(). Use when several repository writes must succeed or fail together, or when you want every mutating HTTP request committed atomically.
---

# Unit of Work (Eksen.UnitOfWork)

A **unit of work** is a single atomic boundary around a business operation: every write that happens inside it commits together or rolls back together. `IUnitOfWorkManager` opens an `IUnitOfWorkScope`, fans it out to each registered `IUnitOfWorkProvider` (EF Core, an outbox, …), and gives you one `CommitAsync`/`RollbackAsync` to end them all. Reach for it whenever placing an order touches more than one aggregate — write the `Order`, decrement the `Product` stock, record the `Payment` — and a partial result would corrupt the domain.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Product`, `Payment`, `Shipment`).

## Registration

Register through the `IEksenBuilder` root. `AddUnitOfWork` adds a **scoped** `IUnitOfWorkManager`; the optional configure callback is the hook providers plug into:

```csharp
services.AddEksen(eksen => eksen
    .AddUnitOfWork());
```

`Eksen.UnitOfWork` ships the manager and the scope contract only — it owns no database. The actual transaction comes from an `IUnitOfWorkProvider` registered alongside it; the EF Core provider is the usual one (see the entity-framework-core skill). With no provider registered, a scope is a real boundary with an empty `ProviderScopes` set — its callbacks still fire, but nothing is persisted.

The configure callback exposes the builder for providers to extend:

```csharp
public interface IEksenUnitOfWorkBuilder
{
    IEksenBuilder EksenBuilder { get; }
    // .Services -> IServiceCollection (extension)
}
```

## ASP.NET Core: a unit of work per request

`Eksen.UnitOfWork.AspNetCore` wraps every **mutating** request in a scope. Add the middleware after routing so it can read endpoint metadata:

```csharp
app.UseRouting();
app.UseUnitOfWork();   // UnitOfWorkMiddleware
```

The middleware:

- **Skips** `GET`, `HEAD`, `OPTIONS` (case-insensitive) and requests with no resolved endpoint — those flow straight through with no scope.
- For `POST`/`PUT`/`PATCH`/`DELETE`, calls `BeginScope`, runs the pipeline, then `CommitAsync` on success.
- On any exception from the pipeline, calls `RollbackAsync` and **re-throws** — the error still reaches your exception handler (see the error-handling skill).
- Always `DisposeAsync`es the scope in a `finally`.

So a controller action just does its work; the request boundary commits it:

```csharp
[HttpPost]
public async Task<OrderId> PlaceOrder(PlaceOrderRequest request, CancellationToken ct)
{
    var order = Order.Place(request.CustomerId, request.Items);
    await _ordersRepository.InsertAsync(order, ct);

    foreach (var item in order.Items)
    {
        await _productsRepository.DecrementStockAsync(item.Sku, item.Quantity, ct);
    }

    // No explicit commit: the per-request unit of work commits if this returns,
    // and rolls back the Order insert + every stock change if anything throws.
    return order.Id;
}
```

### Tuning a single endpoint with `[UnitOfWork]`

Put `[UnitOfWork]` on an action method to override the per-request defaults. It has exactly two knobs:

```csharp
// Raise the isolation level for a balance-sensitive capture
[HttpPost("{id}/capture")]
[UnitOfWork(IsolationLevel = IsolationLevel.Serializable)]
public Task CapturePayment(OrderId id, CancellationToken ct)
{
    return _payments.CaptureAsync(id, ct);
}

// Opt a mutating endpoint out of the ambient unit of work entirely
[HttpPost("reindex")]
[UnitOfWork(IsEnabled = false)]
public Task Reindex(CancellationToken ct)
{
    return _search.ReindexAsync(ct);
}
```

| Property | Default | Effect |
|---|---|---|
| `IsEnabled` | `true` | `false` makes the middleware skip the scope for this endpoint. |
| `IsolationLevel` | `null` | `System.Data.IsolationLevel?` passed through to `BeginScope`. |

The attribute targets methods only. When absent, the middleware uses a default `[UnitOfWork]` (enabled, provider-default isolation).

## Managing a scope by hand

Outside an HTTP request — a background worker, a seeder (see the data-seeding skill), a console tool — open the scope yourself through `IUnitOfWorkManager`:

```csharp
public class OrderFulfilmentJob(
    IUnitOfWorkManager uow,
    IRepository<Order, OrderId> ordersRepository
)
{
    public async Task FulfilAsync(OrderId orderId, CancellationToken ct)
    {
        await using var scope = uow.BeginScope();   // transactional by default
        try
        {
            var order = await ordersRepository.GetAsync(orderId, ct);
            order.MarkPacked();
            await ordersRepository.UpdateAsync(order, ct);

            await scope.CommitAsync(ct);
        }
        catch
        {
            await scope.RollbackAsync(ct);
            throw;
        }
    }
}
```

`BeginScope` signature:

```csharp
IUnitOfWorkScope BeginScope(
    bool isTransactional = true,
    IsolationLevel? isolationLevel = null,
    CancellationToken cancellationToken = default);
```

Pass `isTransactional: false` for a non-transactional batch where you want changes flushed but not wrapped in a DB transaction. `uow.Current` returns the innermost active scope (or `null`); `BeginScope` nests, so an inner scope must be disposed before the outer one — disposing out of order throws `InvalidOperationException`.

Both `CommitAsync` and `RollbackAsync` dispose the scope when they finish, and `DisposeAsync` is idempotent, so the `await using` + explicit-commit pattern above is safe.

## The scope contract

`IUnitOfWorkScope` is what you hold between begin and commit:

| Member | Behaviour |
|---|---|
| `ScopeId` | A `Guid` identifying this scope. |
| `CommitAsync(ct)` | Fires *completing* callbacks, commits every provider scope, fires *completed* callbacks, runs post-commit actions, then disposes. |
| `RollbackAsync(ct)` | Rolls back every provider scope, fires *rollback* callbacks, then disposes. |
| `SaveChangesAsync(ct)` | Flushes pending changes through each provider **without** committing the transaction — useful when a later step needs a generated key. |
| `AddCompletingCallback(cb)` | Run **before** providers commit (still inside the transaction). |
| `AddCompletedCallback(cb)` | Run **after** providers commit, before post-commit actions. |
| `AddRollbackCallback(cb)` | Run when the scope rolls back. |
| `AddPostCommitAction(cb)` | Run **after** a successful commit — for effects that must not happen unless the data landed. |
| `ProviderScopes` | The `IUnitOfWorkProviderScope`s that joined this scope. |

Every callback is a `Func<IServiceProvider, CancellationToken, Task>`, so resolve what you need from the passed `IServiceProvider`:

```csharp
await using var scope = uow.BeginScope();

var order = Order.Place(request.CustomerId, request.Items);
await ordersRepository.InsertAsync(order, ct);

// Send the confirmation email only once the order is durably committed.
scope.AddPostCommitAction(async (sp, token) =>
{
    var emails = sp.GetRequiredService<IEmailSender>();
    await emails.SendOrderConfirmationAsync(order.Id, token);
});

await scope.CommitAsync(ct);
```

Use `AddPostCommitAction` (or `AddCompletedCallback`) for side effects that must observe a committed `Order` — sending mail (emailing skill), publishing an integration event (event-bus skill). Use `AddRollbackCallback` to undo non-transactional work, e.g. release a reserved `Sku` if the `Payment` fails.

## Providers

A provider is the bridge from this abstraction to a real resource. `IUnitOfWorkProvider.BeginScope(parent, isTransactional, isolationLevel, ct)` returns an `IUnitOfWorkProviderScope` that the composite scope drives. The manager resolves **all** registered `IUnitOfWorkProvider`s and joins each into the scope, so a single `CommitAsync` can span more than one resource. You rarely implement this yourself — register the EF Core provider (entity-framework-core skill) and let repository writes enlist automatically. For a true cross-resource saga with compensation, see the distributed-transactions skill.

## Errors

The unit of work does not invent its own exception type — it lets the operation's exceptions propagate. In the ASP.NET Core flow, a domain error raised inside the request (an `EksenException` from your aggregates) triggers `RollbackAsync` and is re-thrown unchanged, so the `EksenExceptionHandler` still maps it to the right HTTP status (see the error-handling skill). Raise domain failures (insufficient stock, an already-`Cancelled` order) as `EksenException`/`CommonErrors` and trust the boundary to undo the partial write.

## Testing

`IUnitOfWorkManager` and `IUnitOfWorkScope` are plain interfaces — mock them to assert your handler commits, or use a real manager with a fake provider to assert the fan-out:

```csharp
[Fact]
public async Task PlaceOrder_Commits_The_Scope()
{
    var scope = new Mock<IUnitOfWorkScope>();
    var uow = new Mock<IUnitOfWorkManager>();
    uow.Setup(m => m.BeginScope(
            It.IsAny<bool>(), It.IsAny<IsolationLevel?>(), It.IsAny<CancellationToken>()))
       .Returns(scope.Object);

    await new PlaceOrderHandler(uow.Object, _ordersRepository).HandleAsync(_request, default);

    scope.Verify(s => s.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    scope.Verify(s => s.RollbackAsync(It.IsAny<CancellationToken>()), Times.Never);
}

[Fact]
public void BeginScope_Joins_Every_Provider()
{
    var services = new ServiceCollection();
    services.AddSingleton<IEnumerable<IUnitOfWorkProvider>>(new[] { _provider.Object });
    var manager = new UnitOfWorkManager(services.BuildServiceProvider());

    var scope = manager.BeginScope();

    scope.ProviderScopes.Count.ShouldBe(1);
}
```

For end-to-end coverage against a real database and per-request commit, use the integration fixtures from the test-base skill.

## Checklist

- [ ] Register with `AddEksen(eksen => eksen.AddUnitOfWork())`, and register a provider (EF Core) so commits actually persist.
- [ ] Add `UseUnitOfWork()` after `UseRouting()` so mutating requests commit per request and roll back on error.
- [ ] Don't hand-commit inside an action covered by the middleware — let the request boundary do it.
- [ ] Use `[UnitOfWork(IsolationLevel = …)]` to raise isolation, `[UnitOfWork(IsEnabled = false)]` to opt an endpoint out.
- [ ] Outside HTTP, `await using var scope = uow.BeginScope();` then `CommitAsync`/`RollbackAsync`; dispose nested scopes inner-first.
- [ ] Put must-be-committed side effects (email, events) in `AddPostCommitAction`/`AddCompletedCallback`, undo work in `AddRollbackCallback`.
- [ ] Call `SaveChangesAsync` only to flush mid-scope; it does not end the transaction.
- [ ] Raise domain failures as `EksenException` and let the boundary roll back — don't swallow them.
