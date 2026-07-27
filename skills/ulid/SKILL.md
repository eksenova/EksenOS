---
name: ulid
description: The EksenOS way to give every aggregate a strongly-typed, ULID-based identifier with Eksen.Ulid — declare an OrderId/CustomerId record over UlidEntityId<TSelf>, generate sortable ids, bind them from routes and JSON in ASP.NET Core, and render them as `format: ulid` strings in OpenAPI. Use when you would otherwise type a bare `Guid`/`string` key or pass raw identifiers between layers.
---

# ULID Identifiers (Eksen.Ulid)

A **ULID** is a 26-character, lexicographically sortable, time-ordered identifier — a friendlier alternative to `Guid` for primary keys. `Eksen.Ulid` wraps one in a **strongly-typed id**: `OrderId` is not interchangeable with `CustomerId`, the compiler stops you passing a product key where an order key belongs, and the value still serializes and binds as a plain 26-char string at the edge. Prefer a typed id over a bare `System.Ulid`/`Guid`/`string` for every aggregate key.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Product`, `Shipment`, `Payment`).

## Declaring an identifier

Derive a `sealed record` from `Eksen.Ulid.UlidEntityId<TSelf>` with a single positional `System.Ulid Value`. That one line is the whole declaration — the base supplies generation, parsing, comparison and conversions:

```csharp
using Eksen.Ulid;

public sealed record OrderId(System.Ulid Value) : UlidEntityId<OrderId>(Value);
public sealed record CustomerId(System.Ulid Value) : UlidEntityId<CustomerId>(Value);
public sealed record ProductId(System.Ulid Value) : UlidEntityId<ProductId>(Value);
public sealed record ShipmentId(System.Ulid Value) : UlidEntityId<ShipmentId>(Value);
public sealed record PaymentId(System.Ulid Value) : UlidEntityId<PaymentId>(Value);
```

Use them as aggregate keys instead of raw scalars:

```csharp
public sealed class Order
{
    public OrderId Id { get; private init; } = OrderId.NewId();

    public CustomerId CustomerId { get; private init; }

    public OrderNumber Number { get; private init; }

    public OrderStatus Status { get; private set; } = OrderStatus.Pending;
}
```

`UlidEntityId<TSelf>` is a `BaseEntityId<TSelf, System.Ulid, …>`, so it composes with the `Eksen.Entities` aggregate/audit abstractions and is itself a value object — equality is by `Value`, never by reference (see the **entities** and **value-objects** skills).

## The base API

Every typed id gets, for free:

| Member | Behaviour |
|---|---|
| `static NewId()` | Generates a fresh, time-ordered id (`System.Ulid.NewUlid()`). |
| `static Create(System.Ulid value)` | Wraps an existing `System.Ulid`. |
| `static Empty` | The all-zero id (`System.Ulid.Empty`). |
| `static Parse(string s, …)` | Parses a 26-char ULID string; throws on a malformed value. |
| `static TryParse(string? s, IFormatProvider?, out TSelf? result)` | Non-throwing parse. |
| `Value` | The underlying `System.Ulid`. |
| `ToString()` / `ToParseableString()` | The canonical 26-char string. |
| `(System.Ulid)id`, `(string)id` | Explicit conversions to the raw value / string. |
| `CompareTo` / `Equals` | Ordinal by the underlying ULID; equal `Value`s are equal ids. |
| `const Length` | `26` — the fixed ULID string length; use it for column sizing. |

```csharp
var id = OrderId.NewId();                       // 01KD0ZX9P61N7AHAV35R79XGQM
var same = OrderId.Parse(id.ToString());        // round-trips
same.ShouldBe(id);

OrderId.TryParse("not-a-ulid", null, out _);    // => false
```

Because ids are time-ordered, `OrderId` values sort in creation order — handy for keyset pagination and index locality (pair with the query/sort parameters in the **repositories** skill).

## Registration

Register through the `IEksenBuilder` root with `AddUlid`. This scans the id types as value objects and installs the `System.Ulid` type converter; the optional builder adds the ASP.NET Core and OpenAPI integrations:

```csharp
services.AddEksen(eksen => eksen
    .AddUlid(ulid => ulid
        .AddAspNetCoreSupport()   // route constraint + JSON converter for System.Ulid
        .AddOpenApiSupport()));    // schema + operation transformers
```

`AddUlid` alone (no callback) is enough for pure domain/persistence use; add the two integration calls only in the API host.

## ASP.NET Core: routing, binding & JSON

`AddAspNetCoreSupport()` registers:

- the **`ulid` route constraint** — `{id:ulid}` rejects any segment that is not a valid 26-char ULID before the action runs;
- a **JSON converter** for `System.Ulid` on both MVC and minimal-API serializer options, so raw ULIDs read/write as bare strings.

```csharp
[HttpGet("orders/{id:ulid}")]
public async Task<OrderDto> GetOrder(OrderId id, CancellationToken ct)
{
    var order = await _ordersRepository.GetAsync(id, ct);   // typed id flows straight through
    return order.ToDto();
}
```

Typed ids (`OrderId`, `CustomerId`, …) bind from route and query values via the value-object string type converter that `UlidEntityId<TSelf>` wires up, and serialize as their 26-char string through the value-object JSON support registered by the value-objects integration — see the **value-objects** skill for how that binding/serialization layer works.

```jsonc
// Order payload — ids are bare ULID strings, not objects
{ "id": "01KD0ZX9P61N7AHAV35R79XGQM", "customerId": "01KD0ZXB2M0R8TC2Q9F4E7WJ5N", "status": "Shipped" }
```

## OpenAPI

`AddOpenApiSupport()` adds a schema transformer and an operation transformer so every `System.Ulid` (and any member marked `[Ulid]`) — plus every `{…:ulid}` route parameter — is documented as a fixed-length string:

```yaml
id:
  type: string
  format: ulid
  minLength: 26
  maxLength: 26
  example: "01KD0ZX9P61N7AHAV35R79XGQM"
```

This builds on the broader transformer set in the **open-api** skill; both register through `AddOpenApi`, so they coexist.

## Validating raw ULID input

When a DTO carries a ULID as a raw `System.Ulid` or `string` (rather than a typed id), annotate it with `[Ulid]` to get model validation. It treats `null` as valid (combine with `[Required]` when the value is mandatory) and rejects anything that is not a parseable ULID:

```csharp
public sealed class CapturePaymentRequest
{
    [Required, Ulid]
    public string OrderId { get; init; } = default!;
}
```

A failed `[Ulid]` check surfaces as a standard model-validation `400`. Prefer typed ids on the wire where you can — `[Ulid]` is for the cases where a raw value is unavoidable.

## Persistence (EF Core)

Map a typed id with a value converter to its 26-char string, sized from `Length`. The same shape works for keys and foreign keys (see the **entity-framework-core** skill for the `DbContext` base and provider setup):

```csharp
builder.Property(x => x.Id)
    .HasConversion(id => id.ToString(), value => OrderId.Parse(value))
    .HasMaxLength(OrderId.Length)
    .IsRequired();

builder.Property(x => x.CustomerId)
    .HasConversion(id => id.ToString(), value => CustomerId.Parse(value))
    .HasMaxLength(CustomerId.Length)
    .IsRequired();
```

ULIDs sort in time order, so a clustered key on `Id` stays append-friendly — unlike random `Guid`s — without an extra sequence column.

## Testing

Pin the contract that crosses boundaries: generation is unique, string round-trips, and equality is by value.

```csharp
[Fact]
public void NewId_Is_Unique()
{
    OrderId.NewId().ShouldNotBe(OrderId.NewId());
}

[Fact]
public void String_Round_Trips()
{
    var id = OrderId.NewId();
    OrderId.Parse(id.ToString()).ShouldBe(id);
}

[Fact]
public void Equal_Values_Are_Equal_Ids()
{
    var ulid = System.Ulid.NewUlid();
    OrderId.Create(ulid).ShouldBe(OrderId.Create(ulid));
}

[Fact]
public void TryParse_Rejects_Garbage()
{
    OrderId.TryParse("invalid", null, out _).ShouldBeFalse();
}
```

Assert that distinct typed ids stay distinct types in your signatures — that compile-time separation is the main reason to use them (fixtures live in the **test-base** skill).

## Checklist

- [ ] Declare each key as `public sealed record XId(System.Ulid Value) : UlidEntityId<XId>(Value);` — one per aggregate.
- [ ] Generate with `XId.NewId()`; wrap existing values with `Create`; parse external strings with `Parse`/`TryParse`.
- [ ] Register via `AddEksen(...).AddUlid(...)`, adding `.AddAspNetCoreSupport()` and `.AddOpenApiSupport()` in the API host.
- [ ] Use the `:ulid` route constraint on raw-ULID segments; let typed ids bind via the value-object converter.
- [ ] Persist by string with `.HasConversion(id => id.ToString(), v => XId.Parse(v)).HasMaxLength(XId.Length)`.
- [ ] Annotate unavoidable raw ULID fields with `[Ulid]` (add `[Required]` when mandatory).
- [ ] Never swap a typed id for a bare `Guid`/`string` in domain signatures — the type safety is the point.
