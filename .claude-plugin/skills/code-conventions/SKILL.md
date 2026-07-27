---
name: code-conventions
description: The architectural conventions every EksenOS-based project follows — model domain values as value objects and smart enumerations (never primitives), identify aggregates with ULID strongly-typed ids, persist through IRepository<> inside a unit of work, raise failures as EksenExceptions mapped at the edge, and wire every module off the AddEksen / IEksenBuilder root. Use when starting a new module or feature, reviewing code for EksenOS-fitness, or deciding which building block a piece of domain logic belongs in.
---

# Code Conventions (EksenOS)

This skill is the **rulebook** for building on EksenOS: the design decisions that make code
idiomatic across every package. It pairs with two companions — the mechanical C# formatting
rules in [`conventions/code-style.md`](../../conventions/code-style.md) (value-object-over-primitive,
repository naming, constructor layout, member ordering, braces, no expression bodies), and the
locked domain in [`conventions/running-example.md`](../../conventions/running-example.md). Read
all three before writing a feature.

All examples use the marketplace's e-commerce running example (`Order`, `OrderItem`,
`Customer`, `Product`, `Shipment`, `Payment`).

## The non-negotiables

| Rule | Why | Skill |
|---|---|---|
| A domain value is a **value object**, never a bare primitive. | Invariants live in one place; the value is always valid. | **value-objects** |
| A fixed set of named values is a **smart enumeration**, never a C# `enum`. | Carries data/behaviour, persists by stable code, rejects unknowns. | **smart-enumerations** |
| An aggregate's identity is a **ULID strongly-typed id**, never a `string`/`Guid`. | Type-safe ids that can't be mixed up; sortable; bound and serialized everywhere. | **ulid** |
| Data access goes through **`IRepository<>`** inside a **unit of work**. | One transaction boundary; no leaking `DbContext`. | **repositories**, **unit-of-work** |
| Failures are **`EksenException`s** raised from `ErrorDescriptor`s. | Mapped to the right HTTP status at the edge; carry structured data. | **error-handling** |
| Every module is wired off **`AddEksen` / `IEksenBuilder`**. | One consistent composition root; modules opt in explicitly. | **core** |

## Modelling the domain

Start from the values, not the primitives. A `Customer`'s email is an `EmailAddress`; an
`Order`'s human key is an `OrderNumber`; a line total is `Money`; a quantity is `Quantity`. A
status is a smart enumeration (`OrderStatus`), and the aggregate's identity is a ULID id
(`OrderId`). An aggregate therefore reads as a composition of typed values:

```csharp
public sealed class Order : AggregateRoot<OrderId, System.Ulid>
{
    private readonly List<OrderItem> _items = [];

    public OrderNumber OrderNumber { get; private set; }

    public CustomerId CustomerId { get; private set; }

    public OrderStatus Status { get; private set; }

    public Money Total { get; private set; }

    public IReadOnlyList<OrderItem> Items
    {
        get { return _items.AsReadOnly(); }
    }

    private Order(OrderId id, OrderNumber orderNumber, CustomerId customerId) : base(id)
    {
        OrderNumber = orderNumber;
        CustomerId = customerId;
        Status = OrderStatus.Pending;
        Total = Money.Zero;
    }

    public static Order Place(OrderNumber orderNumber, CustomerId customerId)
    {
        return new Order(OrderId.NewId(), orderNumber, customerId);
    }

    public void MarkPaid()
    {
        if (Status != OrderStatus.Pending)
        {
            throw OrderingErrors.OrderNotPayable.Raise(Status.Code);
        }

        Status = OrderStatus.Paid;
    }
}
```

See the **entities** skill for the aggregate/entity base types and the **value-objects** skill
for declaring `OrderNumber`/`Money`.

## Orchestrating a use case

Application logic (an app service or an event handler) depends on repositories named
`<entities>Repository`, mutates aggregates through their methods, and commits through the unit
of work. Inputs and outputs are typed — value objects in, value objects out:

```csharp
public sealed class CapturePaymentHandler(
    IRepository<Order, OrderId> ordersRepository,
    IRepository<Payment, PaymentId> paymentsRepository,
    IUnitOfWork unitOfWork
) : IEventHandler<PaymentCapturedIntegrationEvent>
{
    public async Task HandleAsync(
        PaymentCapturedIntegrationEvent @event,
        CancellationToken cancellationToken = default)
    {
        await using var scope = unitOfWork.BeginScope();

        var order = await ordersRepository.GetAsync(@event.OrderId, cancellationToken);
        order.MarkPaid();
        await ordersRepository.UpdateAsync(order, cancellationToken);

        var payment = Payment.Capture(order.Id, @event.Amount);
        await paymentsRepository.InsertAsync(payment, cancellationToken);

        await scope.CommitAsync(cancellationToken);
    }
}
```

The repository and unit-of-work usage is in the **repositories** and **unit-of-work** skills;
integration events (and how their value-object payloads travel on the wire) are in the
**event-bus** skill.

## Errors and the edge

Inside the domain, an invariant breach raises an `EksenException` through an `ErrorDescriptor`
with the right `ErrorType` (`Validation`, `NotFound`, `Conflict`, …). At the HTTP boundary the
`EksenExceptionHandler` maps that to a status code and a `{ errorMessage, errorData }` body — so
domain code never returns ad-hoc error strings or throws bare `Exception`s. See the
**error-handling** skill; user-facing message text comes from the **localization** skill.

## Wiring it together

Every capability is registered off the `IEksenBuilder` root, so a project's composition root
reads as a list of the modules it uses:

```csharp
services.AddEksen(eksen => eksen
    .AddValueObjects(valueObjects => valueObjects
        .Configure(options => options.AddAssembly(typeof(OrderNumber).Assembly))
        .AddAspNetCoreSupport())
    .AddSmartEnums(smartEnums => smartEnums
        .Configure(options => options.AddAssembly(typeof(OrderStatus).Assembly))
        .AddAspNetCoreSupport())
    .AddUlid(ulid => ulid.AddAspNetCoreSupport())
    .AddEntityFrameworkCore<OrderingDbContext>(db => db.UseSqlServer(connectionString))
    .AddEventBus(bus => bus
        .Configure(options => options.AppName = "orders")
        .SubscribeFromAssembly(typeof(OrderPlacedIntegrationEvent).Assembly)
        .UseInMemory()));
```

See the **core** skill for authoring your own `AddX(this IEksenBuilder ...)` module extension,
and the repository `CLAUDE.md` for how these modules stack up into a vertical slice.

## Checklist

- [ ] Every domain value is a value object, smart enumeration, or ULID id — no bare primitives (see code-style.md rule 1).
- [ ] Aggregates derive from the **entities** base types and expose behaviour, not setters.
- [ ] Application logic depends on `IRepository<>` named `<entities>Repository` and commits via a unit of work.
- [ ] Failures raise `EksenException`s from `ErrorDescriptor`s; the edge handler maps them — no bare `throw new Exception`.
- [ ] Every module is registered off `AddEksen(eksen => eksen.AddX(...))`.
- [ ] Code follows the mechanical rules in `conventions/code-style.md` and the domain in `conventions/running-example.md`.
