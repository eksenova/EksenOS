---
name: distributed-transactions
description: The EksenOS way to coordinate a multi-step operation across several resources with Eksen.DistributedTransactions — begin an IDistributedTransaction, add execute/compensate steps, and commit so that any failure rolls back the already-executed steps in reverse (saga-style compensation). Also swaps in a distributed IUnitOfWorkManager that commits every provider scope as one compensating transaction. Use when a single business action touches more than one database, service, or gateway and you need all-or-nothing semantics without a 2-phase-commit coordinator.
---

# Distributed Transactions (Eksen.DistributedTransactions)

A **distributed transaction** is a saga: an ordered list of **steps**, each with an `execute` action and a paired `compensate` action. Steps run in order; if any step throws, the steps that already ran are **compensated in reverse order**, undoing their effects. There is no two-phase-commit coordinator and no shared XA transaction — compensation is how you reach all-or-nothing across resources that cannot share a single ACID boundary (a payment gateway, an inventory service, a shipping API, a second database). Reach for it when one business action spans multiple resources; for a single relational store, a plain `IUnitOfWork` is enough — see the unit-of-work skill.

All examples use the marketplace's e-commerce running example (`Order`, `Product`, `Shipment`, `Payment`).

## Registration

Add the package through the `IEksenBuilder` root. This registers `IDistributedTransactionManager` (scoped):

```csharp
services.AddEksen(eksen => eksen
    .AddDistributedTransactions());
```

The configuration callback is optional and hands you an `IEksenDistributedTransactionsBuilder` (with `Services` and `EksenBuilder`) for further wiring:

```csharp
services.AddEksen(eksen => eksen
    .AddDistributedTransactions(tx =>
    {
        // tx.Services is the same IServiceCollection — register your typed steps here
        tx.Services.AddScoped<CapturePaymentStep>();
        tx.Services.AddScoped<ReserveInventoryStep>();
        tx.Services.AddScoped<CreateShipmentStep>();
    }));
```

## Beginning a transaction

Inject `IDistributedTransactionManager` and call `Begin`. The optional `name` is used in exception messages and step diagnostics; when omitted it is auto-generated:

```csharp
public sealed class PlaceOrderHandler(IDistributedTransactionManager txManager)
{
    public async Task HandleAsync(OrderId orderId, CancellationToken ct)
    {
        await using var tx = txManager.Begin($"PlaceOrder-{orderId}");
        // add steps, then commit...
    }
}
```

`IDistributedTransaction` is `IAsyncDisposable`. `await using` matters: if you dispose a transaction that is still `Pending` or `Executing` (e.g. an exception escapes before `CommitAsync` returns), `DisposeAsync` performs a best-effort `RollbackAsync` so partially-executed work is compensated.

## Adding steps

`AddStep` returns the transaction, so calls chain. The most direct form is an inline `execute`/`compensate` pair — each receives the `IServiceProvider` (resolve scoped services from it) and a `CancellationToken`:

```csharp
await using var tx = txManager.Begin($"PlaceOrder-{order.Id}");

tx.AddStep(
    name: "CapturePayment",
    execute: async (sp, ct) =>
    {
        var payments = sp.GetRequiredService<IPaymentGateway>();
        await payments.CaptureAsync(order.PaymentId, order.Total, ct);
    },
    compensate: async (sp, ct) =>
    {
        var payments = sp.GetRequiredService<IPaymentGateway>();
        await payments.RefundAsync(order.PaymentId, order.Total, ct);
    });

tx.AddStep(
    name: "ReserveInventory",
    execute: async (sp, ct) =>
    {
        var inventory = sp.GetRequiredService<IInventoryService>();
        foreach (var item in order.Items)
        {
            await inventory.ReserveAsync(item.Sku, item.Quantity, ct);
        }
    },
    compensate: async (sp, ct) =>
    {
        var inventory = sp.GetRequiredService<IInventoryService>();
        foreach (var item in order.Items)
        {
            await inventory.ReleaseAsync(item.Sku, item.Quantity, ct);
        }
    });

tx.AddStep(
    name: "CreateShipment",
    execute: (sp, ct) => sp.GetRequiredService<IShippingService>()
        .BookAsync(order.Id, ShipmentCarrier.Ups, ct),
    compensate: (sp, ct) => sp.GetRequiredService<IShippingService>()
        .CancelAsync(order.Id, ct));

await tx.CommitAsync(ct);
```

The overload without a `name` auto-numbers the step (`Step-1`, `Step-2`, …):

```csharp
tx.AddStep(
    (sp, ct) => /* execute */,
    (sp, ct) => /* compensate */);
```

`AddStep` throws `InvalidOperationException` once the transaction has left the `Pending` state — you cannot add steps after `CommitAsync`/`RollbackAsync` has started.

### Reusable typed steps

For a step you want to unit-test, reuse, or inject dependencies into, implement `IDistributedTransactionStep`:

```csharp
public sealed class CapturePaymentStep(
    IPaymentGateway gateway,
    OrderContext order
) : IDistributedTransactionStep
{
    public string Name
    {
        get { return "CapturePayment"; }
    }

    public Task ExecuteAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        return gateway.CaptureAsync(order.PaymentId, order.Total, cancellationToken);
    }

    public Task CompensateAsync(IServiceProvider serviceProvider, CancellationToken cancellationToken = default)
    {
        return gateway.RefundAsync(order.PaymentId, order.Total, cancellationToken);
    }
}
```

Add it by instance or by type. The generic overload resolves the step from the container with `GetRequiredService<TStep>()`, so the step type must be registered (see Registration above):

```csharp
tx.AddStep(new CapturePaymentStep(gateway, order));   // by instance
tx.AddStep<ReserveInventoryStep>();                    // resolved from DI
```

## Commit, compensation, and state

`CommitAsync` runs each step's `ExecuteAsync` in order. On the first failure it stops, sets `State` to `Compensating`, runs `CompensateAsync` on the **already-executed** steps in **reverse** order, and rethrows as a `DistributedTransactionException`.

`State` (`DistributedTransactionState`) moves through:

| State | Meaning |
|---|---|
| `Pending` | Created; steps can still be added. |
| `Executing` | `CommitAsync` is running steps. |
| `Committed` | All steps executed successfully — terminal. |
| `Compensating` | A step failed (or `RollbackAsync` was called); undoing executed steps. |
| `Compensated` | Every compensation succeeded — the transaction was cleanly undone. |
| `Failed` | One or more compensations themselves threw — manual intervention needed. |

```csharp
await using var tx = txManager.Begin("PlaceOrder");
// ...add steps...
try
{
    await tx.CommitAsync(ct);          // tx.State == Committed
}
catch (DistributedTransactionException ex)
{
    // Steps were rolled back. ex.State distinction is on the transaction:
    //   tx.State == Compensated  -> cleanly undone, safe to retry
    //   tx.State == Failed       -> some compensation failed; inspect ex.CompensationExceptions
}
```

`RollbackAsync` compensates executed steps on demand. It is a no-op when the transaction is already `Compensated`/`Failed`, and throws `InvalidOperationException` if called after a successful `Committed`.

## Errors

A failed commit (or a rollback whose compensation throws) raises `DistributedTransactionException`:

- `Message` names the transaction and the step that failed, plus how many steps were compensated.
- `InnerException` is the original exception from the failing `execute` step.
- `CompensationExceptions` is an `IReadOnlyList<Exception>` of every error thrown **during** compensation — empty when compensation was clean (`Compensated`), non-empty when the transaction ended `Failed`.

```csharp
catch (DistributedTransactionException ex)
{
    logger.LogError(ex.InnerException, "Step failed: {Message}", ex.Message);
    foreach (var compError in ex.CompensationExceptions)
    {
        logger.LogCritical(compError, "Compensation failed — order left in partial state");
    }
}
```

`DistributedTransactionException` is a plain `Exception`, not an `EksenException`. If you want a failed saga to surface as a domain error at the API edge, catch it in your handler and rethrow an `EksenException` — see the error-handling skill.

## Distributed unit of work

The package can also replace the standard `IUnitOfWorkManager` so that **every** unit-of-work scope commits its provider scopes as one compensating transaction. Opt in from the unit-of-work builder with `UseDistributedTransactions`:

```csharp
services.AddEksen(eksen => eksen
    .AddDistributedTransactions()
    .AddUnitOfWork(uow => uow
        .UseDistributedTransactions(options =>
        {
            options.PostCommitTimeout = TimeSpan.FromMinutes(5); // default
        })));
```

With this in place, `BeginScope` opens a scope across all registered `IUnitOfWorkProvider`s (for example several EF Core `DbContext`s — see the entity-framework-core skill). On `CommitAsync`, each provider scope's commit becomes a step whose compensation is that provider's rollback; if a later provider fails to commit, the earlier provider commits are compensated. `DistributedUnitOfWorkOptions.PostCommitTimeout` (default 5 minutes) is the configured budget for the post-commit actions that run after all provider scopes have committed.

Everything you already use on a unit of work still applies: `SaveChangesAsync`, the `Completing`/`Completed`/`Rollback` callbacks, and `AddPostCommitAction`. Code that depends only on `IUnitOfWork`/`IUnitOfWorkManager` needs no changes — the distributed manager is a drop-in replacement.

## Testing

Steps are just `execute`/`compensate` delegates, so assert ordering and compensation directly. Use the `EksenServiceTestBase` from the test-base skill and register the package in `ConfigureEksen`:

```csharp
public class PlaceOrderSagaTests : EksenServiceTestBase
{
    protected override void ConfigureEksen(IEksenBuilder builder)
    {
        base.ConfigureEksen(builder);
        builder.AddDistributedTransactions();
    }

    [Fact]
    public async Task Failed_Step_Compensates_Earlier_Steps_In_Reverse()
    {
        var compensated = new List<string>();
        var manager = ServiceProvider.GetRequiredService<IDistributedTransactionManager>();
        await using var tx = manager.Begin("PlaceOrder");

        tx.AddStep("CapturePayment",
            (_, _) => Task.CompletedTask,
            (_, _) => { compensated.Add("CapturePayment"); return Task.CompletedTask; });

        tx.AddStep("ReserveInventory",
            (_, _) => throw new InvalidOperationException("out of stock"),
            (_, _) => Task.CompletedTask);

        var ex = await Should.ThrowAsync<DistributedTransactionException>(() => tx.CommitAsync());

        ex.InnerException.ShouldBeOfType<InvalidOperationException>();
        compensated.ShouldBe(["CapturePayment"]);
        tx.State.ShouldBe(DistributedTransactionState.Compensated);
    }
}
```

Assert the terminal `State` (`Committed` vs `Compensated` vs `Failed`), the compensation order, and `CompensationExceptions` for the failure path — those are the contract callers depend on.

## Checklist

- [ ] Register via `AddEksen(...).AddDistributedTransactions()`; register typed steps in the container yourself.
- [ ] Begin with `IDistributedTransactionManager.Begin(name)` and wrap it in `await using`.
- [ ] Give every step a matching `compensate` that truly undoes its `execute` — compensation is your only rollback.
- [ ] Resolve scoped services from the step's `IServiceProvider`, not a captured outer scope.
- [ ] Order steps so the cheapest-to-undo run last; add steps only while `Pending`.
- [ ] Catch `DistributedTransactionException`; branch on `tx.State` (`Compensated` = retryable, `Failed` = inspect `CompensationExceptions`).
- [ ] Translate saga failures into domain errors with the error-handling skill before they reach the API edge.
- [ ] For multi-DbContext atomicity, opt in with `AddUnitOfWork(uow => uow.UseDistributedTransactions(...))` — see the unit-of-work and entity-framework-core skills.
