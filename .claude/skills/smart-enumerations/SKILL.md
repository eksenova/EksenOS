---
name: smart-enumerations
description: The EksenOS way to model a closed set of named values with Eksen.SmartEnums — declare an Enumeration<TSelf>, give it extra data, persist it by code with EF Core, serialize it as a string over JSON/OpenAPI, and handle unknown-code parsing. Use when you would otherwise reach for a C# enum, a status/type "code" column, or a lookup table of fixed values.
---

# Smart Enumerations (Eksen.SmartEnums)

A **smart enumeration** is a sealed set of named instances — like a C# `enum`, but a real reference type that can carry behaviour and extra data, is persisted and serialized by a stable string **code**, and never silently accepts an out-of-range integer. Prefer it over a plain `enum` whenever a value is part of your domain (a status, a type, a category) or crosses a persistence/HTTP boundary.

All examples use the marketplace's e-commerce running example (`Order`, `Product`, `Shipment`, `Payment`).

## Declaring an enumeration

Derive from `Eksen.SmartEnums.Enumeration<TSelf>`. The base constructor takes the `code` and defaults it to the field name via `[CallerMemberName]`, so the simplest enumeration is just a list of fields:

```csharp
using Eksen.SmartEnums;

public sealed record ShipmentCarrier : Enumeration<ShipmentCarrier>
{
    public static readonly ShipmentCarrier Ups = new();
    public static readonly ShipmentCarrier Fedex = new();
    public static readonly ShipmentCarrier Dhl = new();
    public static readonly ShipmentCarrier Local = new();

    private ShipmentCarrier([CallerMemberName] string? code = null) : base(code) { }
}
```

`Ups.Code == "Ups"`. The `code` is the contract — it is what gets persisted and serialized, so **keep codes stable** even if you rename a field.

### Carrying extra data

Add properties and pass them through the constructor. Pass `code` explicitly when it must differ from the field name:

```csharp
public sealed record OrderStatus : Enumeration<OrderStatus>
{
    public static readonly OrderStatus Pending   = new(displayName: "Awaiting payment");
    public static readonly OrderStatus Paid       = new(displayName: "Paid");
    public static readonly OrderStatus Packed     = new(displayName: "Packed");
    public static readonly OrderStatus Shipped    = new(displayName: "Shipped");
    public static readonly OrderStatus Delivered  = new(displayName: "Delivered", isTerminal: true);
    public static readonly OrderStatus Cancelled  = new(displayName: "Cancelled", isTerminal: true);
    public static readonly OrderStatus Refunded   = new(displayName: "Refunded", isTerminal: true);

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

## The base API

`Enumeration<TSelf>` gives every enumeration:

| Member | Behaviour |
|---|---|
| `Code` | The stable string identity. |
| `static Parse(string code)` | Case-insensitive and whitespace-tolerant. Throws `CommonErrors.ObjectNotFound` (an `EksenException`, `ErrorType.NotFound`) for an unknown code, and `ArgumentException` for a null/blank code. |
| `static GetValues()` | All declared instances. |
| `static MaxLength` | Longest declared code length — use it for column sizing. |
| `CompareTo` / ordering | Ordinal by `Code`. |
| `ToString()` | Returns `Code`. |

```csharp
OrderStatus.Parse("shipped")   // => OrderStatus.Shipped (case-insensitive)
OrderStatus.Parse("Returned")  // throws EksenException (NotFound)
OrderStatus.GetValues()        // all seven statuses
```

`EnsureItemsInitialized` reflects over the type's public static fields **of the enumeration type**, so you can safely add public constants (`public const decimal DefaultTaxRate = 0.2m;`) without breaking materialisation. Duplicate **codes** are rejected at first use.

### When you need a numeric (or external) key

The base has **no integer `Id`** and **no `GetById`** — code is the identity. When an external system speaks in numeric codes, model that as an ordinary property and add a **type-local** lookup; do not expect it on the base:

```csharp
public sealed record PaymentStatus : Enumeration<PaymentStatus>
{
    public static readonly PaymentStatus Authorised = new(gatewayCode: 10);
    public static readonly PaymentStatus Captured   = new(gatewayCode: 20);
    public static readonly PaymentStatus Failed     = new(gatewayCode: 30);
    public static readonly PaymentStatus Refunded   = new(gatewayCode: 40);

    public int GatewayCode { get; }

    private PaymentStatus(int gatewayCode, [CallerMemberName] string? code = null) : base(code)
    {
        GatewayCode = gatewayCode;
    }

    public static PaymentStatus? FromGatewayCode(int gatewayCode)
        => GetValues().FirstOrDefault(x => x.GatewayCode == gatewayCode);
}
```

## Registration

Register enumerations through the `IEksenBuilder` root. Scanning an assembly discovers every `Enumeration<TSelf>` in it:

```csharp
services.AddEksen(eksen => eksen
    .AddSmartEnums(smartEnums => smartEnums
        .Configure(options => options.AddAssembly(typeof(OrderStatus).Assembly))
        .AddAspNetCoreSupport()   // JSON converters + type-info resolver
        .AddOpenApiSupport()));    // OpenAPI schema + operation transformers
```

## JSON & OpenAPI

`AddAspNetCoreSupport()` makes an enumeration serialize as its bare `Code` string (not an object), and deserialize through `Parse`. An unknown code during deserialization becomes a `JsonException` → `400` model-validation error.

```jsonc
// Order payload
{ "orderNumber": "ORD-1001", "status": "Shipped" }
```

`AddOpenApiSupport()` renders the property as `type: string` with the allowed values enumerated, and configures collection query parameters as exploded form values:

```yaml
status:
  type: string
  enum: [Pending, Paid, Packed, Shipped, Delivered, Cancelled, Refunded]
```

## Persistence (EF Core)

Persist by `Code`; size the column from `MaxLength`. This inline conversion is the EksenOS convention — there is no dedicated helper:

```csharp
builder.Property(x => x.Status)
    .HasConversion(x => x.Code, x => OrderStatus.Parse(x))
    .HasMaxLength(OrderStatus.MaxLength)
    .IsRequired();

// nullable
builder.Property(x => x.Carrier)
    .HasConversion(
        x => x != null ? x.Code : null,
        x => x != null ? ShipmentCarrier.Parse(x) : null)
    .HasMaxLength(ShipmentCarrier.MaxLength);
```

## Errors

A failed `Parse` on a code coming from outside the system raises `CommonErrors.ObjectNotFound`. With `Eksen.ErrorHandling.AspNetCore` registered, the `EksenExceptionHandler` maps `ErrorType.NotFound` to **HTTP 404** and writes `{ errorMessage, errorData }`. Inside domain logic, treat an unknown code as a domain error, not a fallback.

## Testing

Pin the contract, not the internals:

```csharp
[Fact]
public void Parse_Is_Case_Insensitive()
    => OrderStatus.Parse("delivered").ShouldBe(OrderStatus.Delivered);

[Fact]
public void Parse_Throws_For_Unknown_Code()
    => Should.Throw<EksenException>(() => OrderStatus.Parse("Returned"));

[Fact]
public void GetValues_Returns_All_Declared()
    => Enumeration<OrderStatus>.GetValues().Count.ShouldBe(7);
```

Assert codes (and any extra data like `DisplayName`/`GatewayCode`), `Parse` round-trips, and that unknown codes throw — codes are persisted/serialized, so a code change is a breaking change.

## Checklist

- [ ] Derive from `Enumeration<TSelf>`; declare instances as `public static readonly` fields.
- [ ] Keep `Code` stable; pass `code:` explicitly only when it differs from the field name.
- [ ] Model extra data (display text, external numeric codes) as properties; add a type-local lookup if you need a non-code key.
- [ ] Register via `AddEksen(...).AddSmartEnums(...).AddAspNetCoreSupport().AddOpenApiSupport()`.
- [ ] Persist by `Code` with `.HasConversion(...).HasMaxLength(MaxLength)`.
- [ ] Let unknown-code `Parse` surface as a `NotFound` error (→ 404 at the edge).
