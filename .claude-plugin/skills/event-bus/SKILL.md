---
name: event-bus
description: The EksenOS way to publish integration events and handle them with IEventHandler<TEvent> — an outbox-backed IEventBus with InMemory and RabbitMQ transports, an EF Core outbox/inbox/dead-letter store, a built-in dashboard, and dead-letter alerts (Slack). Use when one part of the system must react to something that happened in another (an order was placed, a payment was captured) without a direct in-process call.
---

# Event Bus (Eksen.EventBus)

An **integration event** is an immutable fact that something happened — `OrderPlaced`, `PaymentCaptured`, `ShipmentDispatched`. You **publish** it through `IEventBus` and zero or more `IEventHandler<TEvent>` react, possibly in another app or process. The bus is **outbox-first**: a published event is written to an outbox in the same transaction as your domain change, then a background processor delivers it over a transport (InMemory for a single process, RabbitMQ across services). Failed handlers are retried with backoff and, if they keep failing, parked in a **dead-letter** store you can inspect via the dashboard or be alerted about.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Payment`, `Shipment`).

## Defining an event

Derive from `IntegrationEvent` (which implements `IIntegrationEvent`). The base supplies `EventId`, `CreationTime`, `CorrelationId`, and `SourceApp`; you add the payload as `init`-only properties so the fact is immutable:

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

`AddEventBus` registers `IEventBus`, the JSON serializer, the retry pipeline, and two hosted services — `OutboxProcessorBackgroundService` (drains the outbox to the transport) and `EventBusListenerBackgroundService` (consumes from the transport and dispatches to handlers). `AppName` stamps every published event's `SourceApp` and identifies this app to transports.

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

`UnitOfWorkEventBus` holds `AfterUnitOfWork` events until the `IUnitOfWorkScope` is completing, writes them to the outbox in a completing callback, then waits for the outbox rows to leave `Pending`/`Processing` in a post-commit action. Pair this with the **unit-of-work** skill so the transaction boundary actually exists; outside a unit of work the event publishes immediately. Tune the outbox with `OutboxOptions` (`IsEnabled`, `BatchSize`, `PollingInterval`).

## Persistence (EF Core)

`UseEntityFrameworkCore` registers an `EventBusDbContext` and EF Core implementations of the outbox, inbox, and dead-letter stores. Configure the provider through the standard `DbContextOptionsBuilder` (see the **entity-framework-core** skill for provider setup):

```csharp
bus.UseEntityFrameworkCore(db => db.UseSqlServer(connectionString));
```

`EventBusDbContext` exposes `OutboxMessages`, `InboxMessages`, and `DeadLetterMessages`; `OnModelCreating` applies the bundled entity configurations via `ApplyEventBusEntityConfigurations()`. Add it to your migrations so the three tables are created. Without an EF Core (or InMemory) store registered there is nowhere to persist the outbox, and an outbox-enabled bus has no backing store.

## Inbox & idempotency

When an `IInboxStore` is registered and `InboxOptions.IsEnabled` is true (the default), each `(EventId, HandlerType)` pair is recorded before the handler runs. If the same event is delivered again to the same handler, the processor sees it already exists and skips it — so handlers are effectively **exactly-once per handler**. On success the inbox row is marked processed; on failure it is marked failed. Make handlers idempotent anyway for the window between dispatch and the inbox write. `InboxOptions.IdempotencyWindow` (default 7 days) bounds how long records are retained.

## Transports

| Transport | Registration | Use it for |
|---|---|---|
| InMemory | `.UseInMemory()` | A single process, tests, local development. Outbox/inbox/dead-letter are in-memory singletons. |
| RabbitMQ | `.UseRabbitMq(rabbit => ...)` | Multiple apps/services exchanging events over a broker. |

RabbitMQ is configured through `RabbitMqEventBusOptions` (host, credentials, `ExchangeName`, `ExchangeType` — `"topic"` by default — `DeadLetterExchangeName`, `PrefetchCount`, recovery settings). Map events to queues and tune queues on the builder:

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

The wire name of an event is its CLR `FullName` (via `EventNameResolver`), so the **same event type must exist with the same namespace and name in both the publishing and consuming app** — share it through a contracts assembly.

## Retries & dead-lettering

A handler that throws is retried by the pipeline using `RetryOptions` (`MaxRetryAttempts` = 3, exponential `InitialDelay`/`MaxDelay` with `BackoffMultiplier`). When retries are exhausted and `DeadLetterOptions.IsEnabled` is true, the message is written to the `IDeadLetterStore` as a `DeadLetterMessage` (original id, event/handler type, payload, `LastError`, `TotalRetryCount`, correlation/source). The store supports paging (`GetMessagesAsync`), `GetByIdAsync`, `GetCountAsync`, and `RequeueAsync` to retry a parked message. Let handler failures surface as real exceptions — raise domain failures via the **error-handling** skill rather than swallowing them, so a genuinely broken event reaches the dead-letter store instead of being silently dropped.

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
- [ ] Persist the outbox/inbox/dead-letter with `.UseEntityFrameworkCore(...)` and add `EventBusDbContext` to migrations.
- [ ] For transactional publish, `.UseUnitOfWork()` and publish with `EventDispatchMode.AfterUnitOfWork` inside a unit of work.
- [ ] Mount `app.UseEksenEventBusDashboard()` with an auth mode set; add `.AddAlerts(...).UseSlack(...)` for dead-letter notifications.
- [ ] Let handler failures throw so retries and dead-lettering engage; inspect or `RequeueAsync` parked messages.
