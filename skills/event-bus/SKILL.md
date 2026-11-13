---
name: event-bus
description: The EksenOS way to publish integration events and handle them with IEventHandler<TEvent> — an outbox-backed IEventBus with InMemory and RabbitMQ transports, an EF Core outbox/inbox/dead-letter store, a Redis inbox for hosts without a database, delayed broker retries, dead-letter queues with replay, a built-in dashboard, and dead-letter alerts (Slack). Use when one part of the system must react to something that happened in another (an order was placed, a payment was captured) without a direct in-process call.
---

# Event Bus (Eksen.EventBus)

An **integration event** is an immutable fact that something happened — `OrderPlaced`, `PaymentCaptured`, `ShipmentDispatched`. You **publish** it through `IEventBus` and zero or more `IEventHandler<TEvent>` react, possibly in another app or process. The bus is **outbox-first**: a published event is written to an outbox in the same transaction as your domain change, then a background processor delivers it over a transport (InMemory for a single process, RabbitMQ across services). Failed handlers are retried in process, then redelivered through the broker with growing delays, and, if they keep failing, parked in a **dead-letter** store or queue you can inspect, replay, or be alerted about.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Payment`, `Shipment`).

## Defining an event

Derive from `IntegrationEvent` (which implements `IEvent`). The base supplies `EventId`, `CreationTime`, `CorrelationId`, and `SourceApp`; you add the payload as `init`-only properties so the fact is immutable:

```csharp
using Eksen.EventBus;

public sealed class OrderPlacedIntegrationEvent : IntegrationEvent
{
    public required OrderNumber OrderNumber { get; init; }

    public required CustomerId CustomerId { get; init; }

    public required MoneyAmount TotalAmount { get; init; }

    public required Currency Currency { get; init; }
}

public sealed class PaymentCapturedIntegrationEvent : IntegrationEvent
{
    public required OrderNumber OrderNumber { get; init; }

    public required MoneyAmount Amount { get; init; }
}
```

The payload still crosses a JSON boundary, but you model it with the same value objects, smart enumerations, and ULID ids as the rest of the domain — the `JsonEventSerializer` registers their converters, so each one travels as its **bare underlying primitive**: an `OrderNumber` is `"ORD-1001"`, a `MoneyAmount` is `49.90`, a smart enum is its code, a ULID `CustomerId` is its string. The wire contract is therefore still a flat JSON shape, and the typed payload round-trips without you flattening it yourself (see the **ulid**, **smart-enumerations**, and **value-objects** skills). A composite value object backed by a tuple — `Money` is `Currency` + `MoneyAmount` — is decomposed into its parts on the event (a `MoneyAmount TotalAmount` and a `Currency`) so every field stays a single scalar. Only genuinely meaning-free values stay primitive, and never put a rich aggregate on the wire.

## Handling an event

Implement `IEventHandler<TEvent>`. Handlers are resolved per-event from a fresh scope, so inject scoped services (repositories, unit of work) freely:

```csharp
using Eksen.EventBus;

public sealed class CreateShipmentOnOrderPlaced(
    IRepository<Shipment, ShipmentId> shipmentsRepository
) : IEventHandler<OrderPlacedIntegrationEvent>
{
    public async Task HandleAsync(
        OrderPlacedIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        var shipment = Shipment.For(@event.OrderNumber, ShipmentCarrier.Ups);
        await shipmentsRepository.InsertAsync(shipment, cancellationToken);
    }
}
```

Multiple handlers may subscribe to the same event; each runs in its own scope and is tracked independently for idempotency and retry.

## Local events

An **integration event** always crosses a serialization boundary, so its payload must be JSON-shaped. When the reaction has to stay in the same process and needs the *real* object — a `Stream` you intend to forward, an open handle, a live aggregate instance, or a slot the listener writes back into — publish a **local event** instead.

Derive from `LocalEvent` (which implements `ILocalEvent : IEvent`) and handle it with `ILocalEventHandler<TEvent>`:

```csharp
using Eksen.EventBus;

public sealed class ShipmentLabelRequestedLocalEvent : LocalEvent
{
    public required Shipment Shipment { get; init; }

    public Stream? Label { get; set; }
}

public sealed class RenderShipmentLabel(ILabelRenderer renderer)
    : ILocalEventHandler<ShipmentLabelRequestedLocalEvent>
{
    public async Task HandleAsync(
        ShipmentLabelRequestedLocalEvent @event,
        CancellationToken cancellationToken = default)
    {
        @event.Label ??= await renderer.OpenAsync(@event.Shipment, cancellationToken);
    }
}
```

`ILocalEventHandler<TEvent>` extends `IEventHandler<TEvent>`, so a handler reads the same as any other. What changes is delivery:

- **No serialization.** The handler receives the very instance that was published, so reference-typed members survive and a handler may write back through a settable property.
- **Same scope.** Local handlers run in the publisher's scope, not a fresh one, so the ambient unit of work, current user, and tenant are the caller's.
- **Never leaves the process.** Local events bypass the outbox, the transport, the inbox, and dead-lettering entirely — none of those apply to an in-memory call.
- **Multiple listeners.** Every registered `ILocalEventHandler<TEvent>` is invoked in registration order, awaited one after another. With no listener registered, publishing is a no-op.

Because there is no retry or dead-letter safety net, a throwing local handler faults the publisher's call. Keep them fast and side-effect-light, and use an integration event when the work must survive a crash.

`EventDispatchMode.AfterUnitOfWork` still applies: the instance is held and the handlers run as a post-commit action rather than being written to the outbox.

Registration is the same `Subscribe`/`SubscribeFromAssembly` call as any other handler — the builder detects `ILocalEvent` and registers the handler as `ILocalEventHandler<TEvent>` so every listener resolves, keeping it out of the transport's handler registry.

## Registration

Register through the `IEksenBuilder` root with `AddEventBus`, then pick exactly one transport. Subscribe handlers explicitly with `Subscribe<TEvent, THandler>()` or scan an assembly with `SubscribeFromAssembly`:

```csharp
services.AddEksen(eksen => eksen
    .AddEventBus(bus => bus
        .Configure(options => options.AppName = "orders")
        .Subscribe<OrderPlacedIntegrationEvent, CreateShipmentOnOrderPlaced>()
        .SubscribeFromAssembly(typeof(OrderPlacedIntegrationEvent).Assembly)
        .UseInMemory()));
```

`AddEventBus` registers `IEventBus`, the JSON serializer, the retry pipeline, the singleton `IEventProcessor` (it opens its own scope per handler attempt, so a singleton transport can hold it), and three hosted services — `OutboxProcessorBackgroundService` (drains the outbox to the transport), `EventBusListenerBackgroundService` (consumes from the transport and dispatches to handlers) and `EventBusCleanupBackgroundService` (purges processed outbox and inbox rows). The outbox processor and the cleanup only touch stores that are registered, so a host without a database starts them as no-ops. `AppName` stamps every published event's `SourceApp`, names this app's consumer queues (`{AppName}.{HandlerType}`) and is what `PublishOptions.TargetApp` matches.

### Publishing

Inject `IEventBus` and call `PublishAsync`. With the outbox enabled (the default) this writes an `OutboxMessage` and returns; the background processor delivers it:

```csharp
public sealed class PlaceOrderHandler(
    IEventBus eventBus,
    IRepository<Order, OrderId> ordersRepository
)
{
    public async Task PlaceAsync(Order order, CancellationToken cancellationToken)
    {
        await ordersRepository.InsertAsync(order, cancellationToken);

        await eventBus.PublishAsync(
            new OrderPlacedIntegrationEvent
            {
                OrderNumber = order.OrderNumber,
                CustomerId = order.CustomerId,
                TotalAmount = order.Total.Amount,
                Currency = order.Total.Currency
            },
            cancellationToken);
    }
}
```

`PublishOptions` lets you target a specific app, override the correlation id, attach headers, delay delivery, or choose a dispatch mode:

```csharp
await eventBus.PublishAsync(
    new PaymentCapturedIntegrationEvent
    {
        OrderNumber = OrderNumber.Create("ORD-1001"),
        Amount = MoneyAmount.Create(49.90m)
    },
    new PublishOptions
    {
        TargetApp = "shipping",
        CorrelationId = order.OrderNumber.Value,
        Headers = new Dictionary<string, string> { ["x-tenant"] = "eu" },
        DispatchMode = EventDispatchMode.AfterUnitOfWork
    },
    cancellationToken);
```

`CorrelationId` from options wins over the event's own value; `SourceApp` falls back to the configured `AppName` when the event doesn't set it.

## Transactional outbox & the unit of work

The outbox guarantees an event is published **only if** its surrounding transaction commits. By default `DefaultEventBus` saves the outbox message in its own scope. To bind publishing to the ambient unit of work — so events flush to the outbox as part of the same transaction and the publisher waits for them to be processed — opt in with `UseUnitOfWork()` and publish with `EventDispatchMode.AfterUnitOfWork`:

```csharp
services.AddEksen(eksen => eksen
    .AddEventBus(bus => bus
        .Configure(o => o.AppName = "orders")
        .SubscribeFromAssembly(typeof(OrderPlacedIntegrationEvent).Assembly)
        .UseUnitOfWork()
        .UseEntityFrameworkCore(db => db.UseSqlServer(connectionString))
        .UseRabbitMq()));
```

`UnitOfWorkEventBus` holds `AfterUnitOfWork` events until the `IUnitOfWorkScope` is completing, writes them to the outbox in a completing callback, then waits for the outbox rows to leave `Pending`/`Processing` in a post-commit action. Pair this with the **unit-of-work** skill so the transaction boundary actually exists; outside a unit of work the event publishes immediately. Tune the outbox with `OutboxOptions`.

### Publishing the outbox

Every host running the outbox processor polls the same table, so rows are **claimed with a lease** before they are published: `IOutboxStore.ClaimAsync(batchSize, leaseDuration)` marks the due rows `Processing` under a lease token and expiry. On PostgreSQL the claim is a single `UPDATE … WHERE Id IN (SELECT … FOR UPDATE SKIP LOCKED)`, so concurrent hosts take disjoint batches; on other providers it is a conditional update that re-checks each row is still due, so a row is leased to one host at a time. A row whose host died mid-batch becomes claimable again when its lease expires. The broker message id is the event's `IEvent.EventId` (stored on the row as `EventId`), so consumers deduplicate on the real event id, not on the outbox row.

A failed publish releases the lease, increments `RetryCount` and makes the row due again at `NextAttemptTime` = now + exponential backoff with jitter; after `MaxPublishAttempts` the row is `Failed`.

| `OutboxOptions` | Default | Meaning |
|---|---|---|
| `IsEnabled` | `true` | Write integration events to the outbox. Without a registered `IOutboxStore` the bus publishes straight to the transport and the processor does not run. |
| `BatchSize` | `100` | Rows claimed per poll. |
| `PollingInterval` | 5 s | Delay between polls. |
| `LeaseDuration` | 1 min | How long a claim owns its rows. |
| `MaxPublishAttempts` | `10` | Publish attempts before the row is marked `Failed`. |
| `RetryInitialDelay` / `RetryBackoffMultiplier` / `RetryMaxDelay` | 5 s / 2 / 10 min | Backoff of failed publishes (+/- 20 % jitter). |
| `ProcessedRetention` | 7 days | Published rows older than this are purged. |

`CleanupOptions` (`IsEnabled` = true, `Interval` = 1 h, `BatchSize` = 1000) drives the purge of processed outbox rows older than `ProcessedRetention` and processed inbox rows older than `InboxOptions.IdempotencyWindow`.



## Persistence (EF Core)

`UseEntityFrameworkCore` registers an `EventBusDbContext` and EF Core implementations of the outbox, inbox, and dead-letter stores. Configure the provider through the standard `DbContextOptionsBuilder` (see the **entity-framework-core** skill for provider setup):

```csharp
bus.UseEntityFrameworkCore(db => db.UseSqlServer(connectionString));
```

`EventBusDbContext` exposes `OutboxMessages`, `InboxMessages`, and `DeadLetterMessages`; `OnModelCreating` applies the bundled entity configurations via `ApplyEventBusEntityConfigurations()`. Add it to your migrations so the three tables are created. The outbox table carries `EventId`, `NextAttemptTime`, `LeaseToken` and `LeaseExpiresAt` (indexed with `Status`), and both tables have a `(Status, ProcessedTime)` index for the purge; a database created before these columns needs a migration (backfill `EventId` from `Id` for existing rows). Without an EF Core (or InMemory) store registered there is nowhere to persist the outbox, and an outbox-enabled bus has no backing store.

## Inbox & idempotency

When an `IInboxStore` is registered and `InboxOptions.IsEnabled` is true (the default), every handler attempt first **claims** the `(EventId, HandlerType)` pair with `IInboxStore.TryClaimAsync`:

- `Claimed` — run the handler, then `CompleteAsync` the claim.
- `AlreadyProcessed` — the delivery is a duplicate; it is acknowledged without running the handler.
- `InProgress` — another consumer holds the claim; the attempt fails with `InboxClaimConflictException` and is retried later (never acknowledged, so a crashed claimer cannot lose the event).

With a unit of work (`IUnitOfWorkManager` registered) the claim, the handler's changes and the completion commit together, and a failure rolls all of them back. A store that does not enlist in the unit of work (`IInboxStore.EnlistsInUnitOfWork` = false: Redis, in-memory) is completed only after the commit, and on failure its claim is released with `ReleaseAsync` so a redelivery can claim it again. Without any unit of work the sequence is simply claim → handler → complete, or release on failure — the processor needs no database.

`InboxOptions.IdempotencyWindow` (default 7 days) is how long a processed record is remembered: the cleanup service purges older processed EF rows, and the Redis inbox expires its processed marker after exactly this long.

### Redis inbox (hosts without a database)

`Eksen.EventBus.Redis` adds `UseRedisInbox`, an `IInboxStore` in Redis. It replaces any inbox registered before it:

```csharp
bus.UseRedisInbox(redis =>
{
    redis.Configuration = "redis:6379";   // or leave null to use a registered IConnectionMultiplexer
    redis.KeyPrefix = "eksen:inbox";
    redis.ProcessingTimeout = TimeSpan.FromMinutes(5);
});
```

A claim is `SET {KeyPrefix}:{AppName}:h:{HandlerType}:{EventId} … NX PX {ProcessingTimeout}`; completing rewrites the key as processed with `IdempotencyWindow` as its expiry; releasing deletes it only if it is still the caller's claim. A consumer that dies mid-handler loses its claim after `ProcessingTimeout`, so keep it above the longest handler run including retries. Only a small JSON record is stored, not the payload.

| `RedisInboxOptions` | Default | Meaning |
|---|---|---|
| `Configuration` | `null` | StackExchange.Redis configuration string; null uses the registered `IConnectionMultiplexer`. |
| `KeyPrefix` | `eksen:inbox` | Prefix of every key. |
| `ProcessingTimeout` | 5 min | Lifetime of an unfinished claim. |
| `Database` | `-1` | Redis database number. |

### A consumer-only host

A host that only consumes needs no database, outbox, EF inbox or unit of work:

```csharp
services.AddEksen(eksen => eksen
    .AddEventBus(bus => bus
        .Configure(options => options.AppName = "shipping-notifier")
        .Subscribe<OrderPlacedIntegrationEvent, NotifyCustomerOnOrderPlaced>()
        .UseRabbitMq(rabbit => rabbit.Configure(o => o.HostName = "rabbitmq"))
        .UseRedisInbox(redis => redis.Configuration = "redis:6379")));
```

The outbox processor sees no `IOutboxStore` and stops, the cleanup skips the missing stores, and a publish from this host goes straight to the transport. Failed deliveries use the broker's delayed redelivery and dead-letter queues (below).

## Transports

| Transport | Registration | Use it for |
|---|---|---|
| InMemory | `.UseInMemory()` | A single process, tests, local development. Outbox/inbox/dead-letter are in-memory singletons. |
| RabbitMQ | `.UseRabbitMq(rabbit => ...)` | Multiple apps/services exchanging events over a broker. |

RabbitMQ is configured through `RabbitMqEventBusOptions` (host, credentials, `ExchangeName`, `ExchangeType` — `"topic"` by default — `DeadLetterExchangeName`, `PrefetchCount`, recovery settings, and the reliability switches below). Map events to queues and tune queues on the builder:

```csharp
bus.UseRabbitMq(rabbit => rabbit
    .Configure(o =>
    {
        o.HostName = "rabbitmq";
        o.UserName = "orders";
        o.Password = secret;
        o.ExchangeName = "eksen.events";
    })
    .MapEventToQueue<OrderPlacedIntegrationEvent>(
        queueName: "shipping.order-placed",
        routingKey: "order.placed")
    .MapEventToQueue<PaymentCapturedIntegrationEvent>(
        queueName: "shipping.payment-captured",
        routingKey: "payment.captured")
    .ConfigureQueue("shipping.order-placed", q =>
    {
        q.Durable = true;
        q.MessageTtl = 60_000;
    }));
```

### RabbitMQ topology and delivery

- Every handler of an app consumes its own durable queue `{AppName}.{HandlerType}`, bound to the event's routing key (its name, or the `MapEventToQueue` key) **and** to `{AppName}.{routingKey}`.
- A publish with `PublishOptions.TargetApp = "shipping"` uses routing key `shipping.{routingKey}`, which only the `shipping` app's queues bind — other apps never see it.
- Publishes wait for **publisher confirms** (`PublisherConfirms`, default true) and are **mandatory** (`Mandatory`, default true): a nacked or unroutable message throws `PublishException`, so the outbox retries it instead of losing it. Messages are persistent; the message id is the `EventId`.
- Each delivery runs only the handler of its queue, with the attempt number the broker carries.

| `RabbitMqEventBusOptions` | Default | Meaning |
|---|---|---|
| `PublisherConfirms` | `true` | Await broker confirms for every publish. |
| `Mandatory` | `true` | Return unroutable publishes as errors. |
| `DeadLetterExchangeName` | `eksen.events.dlx` | Topic exchange dead letters pass through; null disables broker dead-lettering. |
| `DeclareDeadLetterQueues` | `true` | Declare `{queue}.dlq` per handler queue, bound to the DLX with key `{queue}`. |
| `DelayExchangeName` | `{ExchangeName}.delay` | Headers exchange of the delay tiers. |

The wire name of an event is its CLR `FullName` (via `EventNameResolver`), so the **same event type must exist with the same namespace and name in both the publishing and consuming app** — share it through a contracts assembly.

## Retries & dead-lettering

A failing handler goes through three stages:

1. **In-process retries** — the Polly pipeline retries the handler `RetryOptions.MaxRetryAttempts` times (default 3) with exponential backoff `InitialDelay * BackoffMultiplier^n` (1 s, x2, capped at `MaxDelay` = 30 s) and jitter.
2. **Delayed broker redelivery** — if it still fails, the RabbitMQ transport acknowledges the delivery and republishes it to a delay tier: the headers exchange `{exchange}.delay` routes it by the `eksen-delay-tier` header to the durable queue `{exchange}.delay.{tier}` with a per-message `expiration` of the jittered backoff (`RedeliveryOptions`: `InitialDelay` 10 s, `BackoffMultiplier` 4, `MaxDelay` 15 min, `JitterFactor` 0.2). When it expires the tier queue dead-letters it through the default exchange with its routing key — the failing handler's queue name — so it returns **only to the queue that failed**, not to every subscriber. The message carries `x-eksen-attempt` (the delivery attempt), `x-eksen-first-failure-time` and `x-eksen-original-routing-key`. There are `DeadLetterOptions.MaxRetryAttemptsBeforeDeadLetter` (default 5) redeliveries, one tier each.
3. **Dead letter** — on the final attempt, or straight away for a **poison** message (a payload that does not deserialize, no handler for its type, no event id: `PoisonMessageException`, never retried):
   - with an `IDeadLetterStore` registered and `DeadLetterOptions.IsEnabled`, the message is saved there as a `DeadLetterMessage` (original event id, event and handler type, payload, `LastError`, `TotalRetryCount`, correlation/source) and the delivery is acknowledged;
   - otherwise the broker DLQ `{queue}.dlq` is the destination: the transport publishes the message to the DLX with routing key `{queue}` and the headers `x-eksen-dead-letter-reason` (`poison`, `max_attempts_exceeded`, `failed`, `redelivery_failed`), `x-eksen-exception-type`, `x-eksen-exception-message`, `x-eksen-attempts`, `x-eksen-original-routing-key`, `x-eksen-first-failure-time`, `x-eksen-failed-time`, `x-eksen-queue` and `x-eksen-handler`. Messages the broker itself rejects or expires from a handler queue follow the queue's `x-dead-letter-routing-key` to the same DLQ. (A handler queue declared before per-queue dead-letter routing keeps its old arguments — RabbitMQ cannot change them in place; the transport logs a warning and explicit dead-lettering still reaches the DLQ. Recreate the queue to complete the switch.)

`IEventRescheduler.RescheduleAsync(EventRedelivery redelivery, TimeSpan delay)` exposes the same mechanism: it redelivers an event to one handler queue of this app, immediately or through the tier whose range covers `delay`.

### Replaying dead letters

**Store.** `IDeadLetterStore` pages (`GetMessagesAsync(skip, take)`), reads (`GetByIdAsync`), counts the messages not yet requeued (`GetCountAsync`) and requeues: `RequeueAsync(id)` redelivers the dead letter **to the handler that failed it only** through `IEventRescheduler`, with a fresh retry budget, and marks it requeued (once; concurrent requeues redeliver once, and a failed redelivery leaves it unrequeued).

**Broker DLQ.** `IDeadLetterQueueReplayer` (registered by `UseRabbitMq`) covers hosts without a store:

```csharp
IReadOnlyList<DeadLetterQueueInfo> queues = await replayer.GetQueuesAsync(ct);      // per handler queue: DLQ name, message count
IReadOnlyList<DeadLetteredMessage> peek = await replayer.PeekAsync(queue, 20, ct); // read without removing, with the failure headers
int moved = await replayer.ReplayAsync(queue, 100, ct);                            // move N back to the handler queue, fresh retry budget
```

Let handler failures surface as real exceptions — raise domain failures via the **error-handling** skill rather than swallowing them, so a genuinely broken event reaches the dead-letter destination instead of being silently dropped.

## Dashboard

`Eksen.EventBus.Dashboard` mounts a web UI over the outbox/inbox/dead-letter stores. Register it on the bus builder, then mount the middleware in the pipeline:

```csharp
// registration
bus.AddDashboard(dash => dash
    .Configure(o =>
    {
        o.RoutePrefix = "eksen-eventbus";
        o.Title = "Orders Event Bus";
    })
    .UseBasicAuth("ops", secret));

// app pipeline
app.UseEksenEventBusDashboard();
```

Auth modes are `UseBasicAuth(username, password)`, `UseOpenIdConnect(...)`, and `UseCustomAuth(ctx => Task<bool>)`; the default is no auth, so always set one before exposing it. The dashboard is served from `RoutePrefix` (default `eksen-eventbus`).

## Dead-letter alerts (Slack)

`Eksen.EventBus.Alerts` turns dead-lettered messages into notifications through one or more `IDeadLetterAlertChannel`s; `Eksen.EventBus.Alerts.Slack` adds a Slack channel via an incoming webhook:

```csharp
bus.AddAlerts(alerts => alerts
    .Configure(o => o.EnabledChannels.Add("Slack"))
    .UseSlack(slack =>
    {
        slack.WebhookUrl = slackWebhookUrl;
        slack.Channel = "#orders-ops";
        slack.Username = "Eksen EventBus";
    }));
```

`AddAlerts` wires the `IDeadLetterNotifier`; only channels listed in `EnabledChannels` fire. A channel name comes from its `IDeadLetterAlertChannel.Name`. Each alert carries a `DeadLetterAlert` whose `Summary` names the failed event, retry count, handler, and last error. Notifier failures are logged, never thrown — a broken Slack webhook will not block event processing.

## Testing

For unit and integration tests, register `.UseInMemory()` so the whole pipeline (outbox, inbox, dead-letter, transport) runs in-process with no broker. Publish through `IEventBus`, then assert the handler's side effect (a `Shipment` was inserted, a `Payment` row was updated). The **test-base** skill provides the host and database fixtures; keep `OutboxOptions.PollingInterval` low in tests so delivery is prompt. Assert the contract — event type name, payload fields, and that an unhandled failure lands in the `IDeadLetterStore` — because the event name and JSON shape are a cross-app contract, so changing either is a breaking change.

## Checklist

- [ ] Derive events from `IntegrationEvent`; make the payload immutable, with value objects, smart enums, and ULID ids (each serializes as its bare underlying primitive, so the wire stays a flat JSON shape) — decompose tuple-backed objects like `Money` into `MoneyAmount` + `Currency`.
- [ ] Implement `IEventHandler<TEvent>`; inject scoped services and keep handlers idempotent.
- [ ] Register via `AddEksen(...).AddEventBus(...)`, subscribe with `Subscribe<TEvent, THandler>()` / `SubscribeFromAssembly(...)`, set `AppName`.
- [ ] Pick one transport: `.UseInMemory()` for one process/tests, `.UseRabbitMq(...)` across services (map events to queues; share the event type by `FullName`).
- [ ] Persist the outbox/inbox/dead-letter with `.UseEntityFrameworkCore(...)` and add `EventBusDbContext` to migrations (including the lease and `EventId` columns of the outbox).
- [ ] For transactional publish, `.UseUnitOfWork()` and publish with `EventDispatchMode.AfterUnitOfWork` inside a unit of work.
- [ ] Mount `app.UseEksenEventBusDashboard()` with an auth mode set; add `.AddAlerts(...).UseSlack(...)` for dead-letter notifications.
- [ ] Let handler failures throw so retries, delayed redeliveries and dead-lettering engage; inspect or `RequeueAsync` parked messages, or replay a broker DLQ with `IDeadLetterQueueReplayer`.
- [ ] On a host without a database, use `.UseRedisInbox(...)` for idempotency; the broker DLQ is its dead-letter destination.
