---
name: distributed-locks
description: The EksenOS way to take a mutual-exclusion lock that holds across every process and instance with Eksen.DistributedLocks — resolve IDistributedLockProvider, AcquireAsync/TryAcquireAsync a named IDistributedLockHandle, run the critical section, and release by disposing, backed by PostgreSQL advisory locks or SQL Server sp_getapplock. Use when two replicas must not run the same job, fulfil the same order, or capture the same payment at once.
---

# Distributed Locks (Eksen.DistributedLocks)

A **distributed lock** is a mutual-exclusion primitive that spans every process and machine sharing one database — the cross-instance equivalent of `lock(obj)`. While one worker holds the lock named `"order-fulfilment:ORD-1001"`, every other worker that asks for the same name either waits, gives up, or fails, so a critical section runs on at most one node at a time. Reach for it when scaling out means the same unit of work could otherwise run twice: a scheduled job firing on three replicas, two requests packing the same `Order`, or two handlers capturing the same `Payment`.

`Eksen.DistributedLocks` is the provider-agnostic abstraction; `Eksen.DistributedLocks.PostgreSql` and `Eksen.DistributedLocks.SqlServer` are the two backends. The lock lives for the lifetime of a database session held inside the handle, so it is released the instant the handle is disposed (or the connection drops) — there are no orphaned rows to clean up.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Shipment`, `Payment`).

## Registration

Register through the `IEksenBuilder` root, then pick exactly one backend. `AddDistributedLocks` takes an optional configure action; `UsePostgreSql` / `UseSqlServer` register the `IDistributedLockProvider` as a singleton:

```csharp
services.AddEksen(eksen => eksen
    .AddDistributedLocks(locks => locks
        .Configure(options => options.DefaultTimeout = TimeSpan.FromSeconds(30))
        .UsePostgreSql()));   // or .UseSqlServer()
```

`Configure` sets `EksenDistributedLockOptions.DefaultTimeout` — the timeout applied when a caller passes none. Leave it `null` to wait indefinitely by default.

Each backend binds its connection string from configuration. The default section paths are `Eksen:DistributedLocks:PostgreSql` and `Eksen:DistributedLocks:SqlServer`; pass a different path to `UsePostgreSql(configSectionPath)` / `UseSqlServer(configSectionPath)` if you keep it elsewhere. The `ConnectionString` is `[Required]` and validated on start:

```jsonc
// appsettings.json
{
  "Eksen": {
    "DistributedLocks": {
      "PostgreSql": {
        "ConnectionString": "Host=localhost;Database=orders;Username=app;Password=..."
      }
    }
  }
}
```

Both backends use the same database the rest of EksenOS already talks to — there is no extra infrastructure. PostgreSQL maps the lock name onto a session-level **advisory lock** (`pg_advisory_lock`); SQL Server maps it onto `sp_getapplock` with a session-scoped exclusive lock.

## Acquiring a lock

Inject `IDistributedLockProvider` and call one of two methods. Both return an `IDistributedLockHandle`, which is `IAsyncDisposable` — **always `await using`** so the lock is released when the block exits:

```csharp
public sealed class OrderFulfilmentService(IDistributedLockProvider locks)
{
    public async Task FulfilAsync(OrderNumber orderNumber, CancellationToken ct)
    {
        await using var handle = await locks.AcquireAsync(
            name: $"order-fulfilment:{orderNumber.Value}",
            timeout: TimeSpan.FromSeconds(10),
            cancellationToken: ct);

        // Exclusive across all instances until the handle is disposed.
        // Pack the order, create the Shipment, capture the Payment...
    }
}
```

`AcquireAsync` is the **wait-or-throw** path: it blocks until the lock is granted, and if it cannot be granted within `timeout` it throws `DistributedLockException`. A `null` timeout (and no `DefaultTimeout`) waits indefinitely.

`TryAcquireAsync` is the **best-effort** path: it never throws on contention. With no timeout it makes a single non-blocking attempt; with a timeout it polls until the deadline. Either way, on failure it returns a handle whose `IsAcquired` is `false` — check it before entering the critical section:

```csharp
await using var handle = await locks.TryAcquireAsync("nightly-reconciliation", cancellationToken: ct);

if (!handle.IsAcquired)
{
    // Another replica already owns the job this tick — skip quietly.
    return;
}

await ReconcilePaymentsAsync(ct);
```

### Choosing the lock name

The `name` is the identity of the lock — two callers contend only when their names match exactly. Build it from the resource you are guarding so unrelated work proceeds in parallel:

```csharp
$"order-fulfilment:{orderNumber.Value}"   // serialises work on ONE order
$"customer-credit:{customerId}"           // serialises per customer
"nightly-reconciliation"                  // a single global singleton job
```

Different names never block each other. If you omit `name` (pass `null`), the provider generates a random one — useful only when you want a handle that contends with nothing.

## The handle

`IDistributedLockHandle` is deliberately small:

| Member | Behaviour |
|---|---|
| `Name` | The lock name you asked for (or the generated one). |
| `IsAcquired` | `true` while held; `false` for a `TryAcquireAsync` that lost, and after disposal. |
| `DisposeAsync()` | Releases the lock and closes the underlying connection. Idempotent — disposing twice is safe. |

A lost `TryAcquireAsync` returns a `NotAcquiredDistributedLockHandle` — a real handle (so `await using` is uniform) that holds nothing, reports `IsAcquired == false`, and disposes to a no-op. Never run the guarded work without confirming `IsAcquired`.

Because the lock is bound to the session inside the handle, **scope the handle as tightly as the critical section**. Hold it across the protected work and dispose promptly; do not stash it in a field or pass it across requests.

## Locks vs. transactions

A distributed lock guards *who may run*, not *what is durable*. It is not a substitute for a transaction. The usual shape is: take the lock, then open a unit of work for the actual reads and writes:

```csharp
await using var handle = await locks.AcquireAsync(
    $"order-fulfilment:{orderNumber.Value}", cancellationToken: ct);

await using var scope = uow.BeginScope();
var order = await ordersRepository.GetAsync(orderId, ct);
order.MarkPacked();
await ordersRepository.UpdateAsync(order, ct);
await scope.CommitAsync(ct);
```

Persist the `Order`/`Shipment`/`Payment` changes through the **repositories** and **unit-of-work** skills; the lock only ensures a single instance reaches this code at a time. For multi-resource workflows with compensation, reach for the **distributed-transactions** skill instead — a lock alone does not roll anything back.

## Errors

`DistributedLockException` is the only failure type, and it surfaces from `AcquireAsync` when the timeout elapses (or the backend reports an `sp_getapplock` failure). It is a plain `Exception`, **not** an `EksenException`, so the `error-handling` skill's `EksenExceptionHandler` will not map it to a tidy HTTP status — treat a failed acquire as a domain decision (retry the job later, return 409, skip the tick) at the call site rather than letting it bubble to the edge. `TryAcquireAsync` never throws on contention; reserve `try/catch` for the wait-or-throw path.

```csharp
try
{
    await using var handle = await locks.AcquireAsync(name, TimeSpan.FromSeconds(5), ct);
    await DoWorkAsync(ct);
}
catch (DistributedLockException)
{
    // Someone else holds it — re-queue and move on.
}
```

## Testing

Resolve the real `IDistributedLockProvider` against a real database — these locks are database behaviour, so an in-memory fake proves nothing. The PostgreSQL backend pairs with a container fixture; the SQL Server backend builds on the `test-base` skill's `EksenSqlServerTestBase` / `SqlServerWorkerPool`. Pin the two contracts that callers depend on: a contended `AcquireAsync` throws, and a contended `TryAcquireAsync` reports `IsAcquired == false`.

```csharp
[Fact]
public async Task Second_Acquire_Throws_While_Held()
{
    await using var first = await provider.AcquireAsync("order-fulfilment:ORD-1001");

    await Should.ThrowAsync<DistributedLockException>(
        () => provider.AcquireAsync("order-fulfilment:ORD-1001", TimeSpan.FromMilliseconds(200)));
}

[Fact]
public async Task TryAcquire_Returns_NotAcquired_While_Held()
{
    await using var first = await provider.AcquireAsync("nightly-reconciliation");

    await using var second = await provider.TryAcquireAsync("nightly-reconciliation");

    second.IsAcquired.ShouldBeFalse();
}

[Fact]
public async Task Lock_Is_Reusable_After_Release()
{
    var first = await provider.AcquireAsync("order-fulfilment:ORD-1001");
    await first.DisposeAsync();

    await using var second = await provider.AcquireAsync("order-fulfilment:ORD-1001");
    second.IsAcquired.ShouldBeTrue();
}
```

Also assert that disposing releases (a fresh acquire of the same name succeeds afterwards) and that `DisposeAsync` is idempotent.

## Checklist

- [ ] Register via `AddEksen(...).AddDistributedLocks(locks => locks.Configure(...).UsePostgreSql())` (or `.UseSqlServer()`) — exactly one backend.
- [ ] Put the backend `ConnectionString` under `Eksen:DistributedLocks:PostgreSql` / `:SqlServer` (or pass a custom section path).
- [ ] Inject `IDistributedLockProvider`; `await using` every handle so the lock is released on exit.
- [ ] Name the lock after the resource it guards (`order-fulfilment:{orderNumber}`) so unrelated work never contends.
- [ ] Use `AcquireAsync` (wait-or-throw) or `TryAcquireAsync` (best-effort) — and check `IsAcquired` before the critical section on the latter.
- [ ] Set `DefaultTimeout`, or pass a `timeout`, rather than waiting indefinitely in request paths.
- [ ] Catch `DistributedLockException` at the call site — it is a plain exception, not an `EksenException`, so the edge handler won't map it.
- [ ] Take the lock *around* a unit of work (see the unit-of-work and repositories skills); use distributed-transactions when you need compensation.
- [ ] Test against a real database (PostgreSQL container / `EksenSqlServerTestBase`); pin contended-throw, contended-not-acquired, and release-then-reacquire.
