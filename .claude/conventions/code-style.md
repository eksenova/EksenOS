# EksenOS Code Style

Mechanical C# conventions that every EksenOS skill example **and** every EksenOS-based
project follows. This is the companion to [`running-example.md`](./running-example.md) (the
domain every example uses) and the **code-conventions** skill (the architectural rules —
value objects required, smart enums over `enum`, ULID identities, vertical slices). When you
write or edit a code sample in any skill, it must obey every rule below.

All examples use the marketplace's e-commerce running example (`Order`, `OrderItem`,
`Customer`, `Product`, `Shipment`, `Payment`).

## 1. Value objects, never bare primitives

A field, property, parameter, DTO member, or event payload that carries domain meaning uses a
**value object** (or a smart enumeration, or a strongly-typed id) — never a raw `string`,
`decimal`, `int`, or `Guid`. A primitive is only acceptable when the value genuinely has no
domain meaning (a `bool` flag, a free-form note, a count with no unit).

```csharp
// Wrong — primitives leak domain meaning and invariants
public sealed class PaymentCapturedIntegrationEvent : IntegrationEvent
{
    public required string OrderNumber { get; init; }

    public required decimal Amount { get; init; }
}

// Right — value objects carry the invariants and serialize as their bare primitive
public sealed class PaymentCapturedIntegrationEvent : IntegrationEvent
{
    public required OrderNumber OrderNumber { get; init; }

    public required MoneyAmount Amount { get; init; }
}
```

This holds on **every** boundary, including integration events: the event bus serializes value
objects, smart enumerations, and ULID ids to their underlying primitive on the wire (an
`OrderNumber` becomes `"ORD-1001"`, a `MoneyAmount` becomes `49.90`, a smart enum becomes its
code), so the typed payload round-trips without flattening it yourself. Use the shipped value
objects (`Money`, `MoneyAmount`, `EmailAddress`, `Quantity`, `AddressLine`, …) and compose new
ones — see the **value-objects**, **smart-enumerations**, and **ulid** skills.

## 2. Name repository dependencies `<entities>Repository`

A repository field or parameter is named after the aggregate it serves, pluralised, with a
`Repository` suffix — `ordersRepository`, `shipmentsRepository`, `paymentsRepository`. Never a
bare `orders` or `repository`.

```csharp
// Wrong
public sealed class FulfilOrderHandler(IRepository<Shipment, ShipmentId> shipments)

// Right
public sealed class FulfilOrderHandler(IRepository<Shipment, ShipmentId> shipmentsRepository)
```

## 3. Format primary constructors with parameters on their own lines

When a type has a primary constructor **and** a base type or interface list, put each
parameter on its own line and the closing `)` on its own line, followed by `: Base`. This keeps
the inheritance clause readable instead of wrapping it awkwardly after a long parameter list.

```csharp
// Wrong — the interface clause wraps after the parameters
public sealed class CreateShipmentOnOrderPlaced(IRepository<Shipment, ShipmentId> shipmentsRepository)
    : IEventHandler<OrderPlacedIntegrationEvent>

// Right
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

Apply the same multi-line form whenever the constructor has two or more parameters. A single
parameter with no base/interface list may stay on one line.

## 4. Declare `const` and `static` fields first

Within a type, constants and static fields come before instance members. The canonical order is:
`const` → `static` fields (including `static readonly` instances) → instance fields →
constructors → properties → methods.

```csharp
public sealed record OrderNumber : ValueObject<OrderNumber, string>,
    IValueObjectParser<OrderNumber, string>
{
    public const int MaxLength = 16;

    private OrderNumber(string value) : base(value) { }

    // ... properties, then methods
}
```

## 5. Blank lines: between properties and methods, never between fields

Leave exactly one blank line between properties, and between methods. Do **not** put blank
lines between consecutive fields (including the `static readonly` instances of a smart
enumeration).

```csharp
public sealed record OrderStatus : Enumeration<OrderStatus>
{
    // fields: no blank lines between them
    public static readonly OrderStatus Pending = new(displayName: "Awaiting payment");
    public static readonly OrderStatus Paid = new(displayName: "Paid");
    public static readonly OrderStatus Shipped = new(displayName: "Shipped");
    public static readonly OrderStatus Delivered = new(displayName: "Delivered", isTerminal: true);

    // properties: one blank line between each
    public string DisplayName { get; }

    public bool IsTerminal { get; }

    private OrderStatus(string displayName, bool isTerminal = false, [CallerMemberName] string? code = null)
        : base(code)
    {
        DisplayName = displayName;
        IsTerminal = isTerminal;
    }
}
```

## 6. Always use braces; no inline or brace-less control statements

Every `if`, `else`, `for`, `foreach`, `while`, and `using` statement uses a braced block on its
own lines — even a one-line body. No `if (x) return;`, no brace-less bodies. (Ternary
*expressions* and the conditional `?.`/`??` operators are fine — this rule is about statements.)

```csharp
// Wrong
if (!handle.IsAcquired) return;

// Right
if (!handle.IsAcquired)
{
    return;
}
```

## 7. No expression-bodied methods or properties

Never use the `=>` shortcut for a method or property body. Use a block body with an explicit
`return`. Auto-properties (`{ get; }`, `{ get; init; }`, `{ get; set; }`) are not
expression-bodied and are correct. Lambdas and LINQ expressions (`x => x.Total`) and
specification expressions are fine — the ban is only on member bodies.

```csharp
// Wrong
public override Expression<Func<Order, bool>> ToExpression()
    => order => order.Status == OrderStatus.Pending;

public string DisplayName => _displayName;

// Right
public override Expression<Func<Order, bool>> ToExpression()
{
    return order => order.Status == OrderStatus.Pending;
}

public string DisplayName
{
    get { return _displayName; }
}
```

## Checklist

- [ ] Domain values are value objects / smart enums / ULID ids, never bare primitives — on every boundary, events included.
- [ ] Repository dependencies are named `<entities>Repository`.
- [ ] Primary constructors with a base/interface list (or 2+ params) put each parameter on its own line, `)` then `: Base` on the next.
- [ ] `const`/`static` fields are declared before instance members.
- [ ] One blank line between properties and between methods; no blank lines between fields.
- [ ] Every control statement uses a braced block — no inline or brace-less bodies.
- [ ] No `=>` expression-bodied methods or properties; auto-properties and lambdas are fine.
