---
name: value-objects
description: The EksenOS way to model immutable, equality-by-value domain types with Eksen.ValueObjects — derive a ValueObject<TSelf, TUnderlyingValue>, validate in the constructor, expose Create/Parse, persist and serialize by the underlying primitive, and bind it from ASP.NET Core requests. Use when a string/decimal/tuple carries domain meaning and invariants (OrderNumber, Sku, Money, EmailAddress, PostalCode) rather than being a bare primitive.
---

# Value Objects (Eksen.ValueObjects)

A **value object** is an immutable domain type identified entirely by its contents, not by a reference or an id. Two value objects with the same value are equal. Unlike a bare `string` or `decimal`, a value object validates its own invariants at construction, so an `OrderNumber` is *always* a well-formed order number and an `EmailAddress` is *always* a syntactically valid, normalised address. Reach for one whenever a primitive carries domain meaning, has a validation rule, or crosses a persistence/HTTP boundary.

`Eksen.ValueObjects` also ships a library of ready-made value objects — `Money`, `MoneyAmount`, `Currency`, `EmailAddress`, `Quantity`, `AddressLine`, and more — that you can use directly.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Product`, `Shipment`, `Payment`).

## Declaring a value object

Derive from `Eksen.ValueObjects.ValueObject<TSelf, TUnderlyingValue>` and implement `IValueObjectParser<TSelf, TUnderlyingValue>`. As a `record`, it gets structural equality for free. You supply three things: a private constructor that validates, the `Create`/`Parse` factory pair, and `ToParseableString`:

```csharp
using Eksen.ValueObjects;

public sealed record OrderNumber : ValueObject<OrderNumber, string>,
    IValueObjectParser<OrderNumber, string>
{
    public const int MaxLength = 16;

    private OrderNumber(string value) : base(value) { }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw OrderingErrors.EmptyOrderNumber.Raise();
        }

        value = value.Trim().ToUpperInvariant();

        if (value.Length > MaxLength)
        {
            throw OrderingErrors.OrderNumberOverflow.Raise(value, MaxLength);
        }

        return value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static OrderNumber Create(string value)
    {
        return Parse(value);
    }

    public static OrderNumber Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new OrderNumber(value);
    }
}
```

Key points:

- The base constructor calls `Validate(value)` and stores the result in `Value`, so `Validate` is the **single normalisation + invariant gate** — trim, upper-case, range-check here and nothing downstream can construct an invalid instance.
- `Value` is strongly typed (`OrderNumber.Value` is a `string`). `ToString()` returns `ToParseableString()`.
- `Create` takes the already-typed underlying value; `Parse` takes a `string` (and an optional `IFormatProvider`). For a string-backed object the two collapse, as above. For non-string underlying types they differ — see `Money` below.

The same shape gives you `Sku` and `PostalCode`:

```csharp
public sealed record Sku : ValueObject<Sku, string>, IValueObjectParser<Sku, string>
{
    public const int MaxLength = 32;

    private Sku(string value) : base(value) { }

    protected override string Validate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw OrderingErrors.EmptySku.Raise();
        }

        value = value.Trim().ToUpperInvariant();

        return value.Length > MaxLength
            ? throw OrderingErrors.SkuOverflow.Raise(value, MaxLength)
            : value;
    }

    public override string ToParseableString(IFormatProvider? provider = null)
    {
        return Value;
    }

    public static Sku Create(string value)
    {
        return Parse(value);
    }

    public static Sku Parse(string value, IFormatProvider? formatProvider = null)
    {
        return new(value);
    }
}
```

### Underlying values beyond `string`

`TUnderlyingValue` can be any `notnull` type, including a tuple. The shipped `Money` is backed by `(Currency Currency, MoneyAmount Amount)`, which is why its `Create` takes the tuple while `Parse` reads a string like `"100.00 USD"`:

```csharp
using Eksen.ValueObjects.Finance;

var price   = Money.Create((Currency.Usd, MoneyAmount.Create(49.90m)));
var parsed  = Money.Parse("49.90 USD");   // Currency.Usd, Amount 49.90
price.ShouldBe(parsed);                    // value equality

price.Currency;       // Currency.Usd
price.Amount.Value;   // 49.90m
```

`Currency` here is a smart enumeration (`Enumeration<Currency>`) and `MoneyAmount` is itself a value object with arithmetic operators (`a + b`, `a * b`, and overloads against a bare `decimal`/`uint`), a `Zero`, `IsZero`, and `AssertPositive()`. Compose value objects out of other value objects and [smart enumerations](smart-enumerations) rather than flattening everything to primitives.

## What the base type gives you

`ValueObject<TSelf, TUnderlyingValue>` provides:

| Member | Behaviour |
|---|---|
| `Value` | The validated, strongly-typed underlying value. |
| `static Create(TUnderlyingValue)` | Build from an already-typed value (you implement it). |
| `static Parse(string, IFormatProvider?)` | Build from text (you implement it). Throws on invalid input. |
| `static TryParse(string?, out TSelf?)` | Non-throwing `Parse`; `false` (and `null`) on blank/invalid input. |
| `static TryCreate(TUnderlyingValue?, out TSelf?)` | Non-throwing `Create`. |
| `static GetUnderlyingValueType()` | The `TUnderlyingValue` `Type` — used by the JSON/binding plumbing. |
| `ToString()` | Returns `ToParseableString()`. |
| `Deconstruct(out TUnderlyingValue)` | `var (raw) = orderNumber;`. |
| record equality | Structural equality / `GetHashCode` over `Value`. |

```csharp
OrderNumber.Parse("ord-1001");                        // => "ORD-1001" (normalised)
OrderNumber.TryParse("  ", out var n);                // false, n == null
ValueObject<EmailAddress, string>.TryParse("nope", out var e);  // false
```

Use `Parse`/`Create` on a trusted path (you *expect* validity, and want the exception otherwise) and `TryParse`/`TryCreate` when probing untrusted input without exception flow.

## Registration

Register through the `IEksenBuilder` root (see the [core](core) skill). `AddValueObjects` automatically registers every value object shipped in `Eksen.ValueObjects` (`Money`, `EmailAddress`, `Quantity`, …); add your own by scanning their assembly. Registration wires up the `TypeConverter` and JSON converter for each discovered type:

```csharp
services.AddEksen(eksen => eksen
    .AddValueObjects(valueObjects => valueObjects
        .Configure(options => options.AddAssembly(typeof(OrderNumber).Assembly))
        .AddAspNetCoreSupport()));   // JSON converters + type-info resolver
```

`Configure(options => …)` exposes `EksenValueObjectOptions`:

- `AddAssembly(assembly)` — register every concrete value object in the assembly.
- `Add<TValueObject>()` / `Add(Type)` — register one explicitly.
- `AddRange(types)` — register a set.
- `ValueObjectTypes` — the registered types (handy in tests).

## JSON & ASP.NET Core binding

`AddAspNetCoreSupport()` (from `Eksen.ValueObjects.AspNetCore`) post-configures both the Minimal API `JsonOptions` and the MVC `JsonOptions` so every registered value object serializes as its **bare underlying value**, not as an object, and deserializes through `Create`. An `OrderNumber` becomes the JSON string `"ORD-1001"`; a `decimal`-backed object like `MoneyAmount` becomes the bare number `49.90`. (A tuple-backed object like `Money` serializes through its underlying tuple, not as a wrapper object.)

```jsonc
// Order payload — value objects are flat, not wrapped
{
  "orderNumber": "ORD-1001",
  "customerEmail": "buyer@example.com",
  "shippingPostalCode": "34000"
}
```

Because the underlying value drives serialization, the generated OpenAPI schema also describes the property as its underlying primitive (e.g. `orderNumber: { type: string }`) — there is no separate OpenAPI registration call in this package.

Registration also attaches a `TypeConverter` (`ValueObjectTypeConverter<,>`) to each value object via `TypeDescriptor`, so model binding from **route and query string** values works: a `[FromRoute] OrderNumber orderNumber` or `[FromQuery] Sku sku` binds straight from the request text through `Parse`. An unparseable value surfaces as a model-binding/`400`.

## Persistence (EF Core)

Persist by the underlying value with a `HasConversion`, sizing string columns from the value object's own `MaxLength` constant. See the [entity-framework-core](entity-framework-core) skill for `DbContext` wiring:

```csharp
builder.Property(x => x.OrderNumber)
    .HasConversion(x => x.Value, x => OrderNumber.Create(x))
    .HasMaxLength(OrderNumber.MaxLength)
    .IsRequired();

builder.Property(x => x.CustomerEmail)
    .HasConversion(x => x.Value, x => EmailAddress.Create(x))
    .HasMaxLength(EmailAddress.MaxLength);
```

For a value object backed by a tuple (like `Money`), map it as an **owned type** and convert each component, or store its `ToParseableString()` round-trip — pick whichever the query workload needs. A single-primitive value object (`OrderNumber`, `Sku`, `EmailAddress`, `PostalCode`) is always a plain column via the conversion above.

## Errors

Validation failures raise an `EksenException` through an `ErrorDescriptor.Raise(...)`, exactly like the shipped objects (`FinanceErrors`, `EmailingErrors`, `GeoLocationErrors`). Declare your own descriptors with the value-object error delegates so the error data carries the offending value:

```csharp
using Eksen.Core;
using Eksen.ErrorHandling;
using Eksen.ValueObjects.ErrorHandling;

public static class OrderingErrors
{
    public static readonly string Category = $"{AppModules.ValueObjects}.Ordering";
    public static readonly ErrorDescriptor EmptyOrderNumber =
        new(ErrorType.Validation, Category);
    public static readonly ErrorDescriptor<ValueLengthOverflowError> OrderNumberOverflow =
        new(ErrorType.Validation, Category,
            self => (value, maxLength) =>
                new ErrorInstance(self).WithValue(value).WithValue(maxLength));
    public static readonly ErrorDescriptor<ValueParseError> InvalidOrderNumber =
        new(ErrorType.Validation, Category,
            self => value => new ErrorInstance(self).WithValue(value));
}
```

The available delegates are `ValueParseError(string)`, `ValueValidationError<T>(T)`, `ValueOverflowError<T>(T value, T maxValue)`, and `ValueLengthOverflowError(string value, int maxLength)`. All value-object validation uses `ErrorType.Validation`, which the `EksenExceptionHandler` maps to **HTTP 400** — see the [error-handling](error-handling) skill.

## Strongly-typed identifiers vs. value objects

A value object models a domain *value* with invariants (`OrderNumber`, `Money`). A strongly-typed *identity* (`OrderId`, `CustomerId`) is a different concern — use the [ulid](ulid) skill for those. An `Order` aggregate (see the [entities](entities) skill) typically has a ULID `OrderId` for identity **and** an `OrderNumber` value object for its human-facing business key.

## Testing

Pin the contract: validation normalises, invalid input throws the expected descriptor, and equality is by value. The shipped tests use `EksenUnitTestBase` (see the [test-base](test-base) skill) and Shouldly:

```csharp
[Fact]
public void Create_Normalises_To_Upper_And_Trims()
{
    OrderNumber.Create("  ord-1001 ").Value.ShouldBe("ORD-1001");
}

[Fact]
public void Create_Throws_For_Blank()
{
    var ex = Should.Throw<EksenException>(() => OrderNumber.Create("  "));
    ex.Descriptor.ShouldBe(OrderingErrors.EmptyOrderNumber);
}

[Fact]
public void Equal_By_Value()
{
    OrderNumber.Create("ORD-1001").ShouldBe(OrderNumber.Parse("ord-1001"));
}

[Fact]
public void TryParse_Returns_False_For_Invalid()
{
    ValueObject<OrderNumber, string>.TryParse("", out _).ShouldBeFalse();
}
```

Assert the normalisation rule, that each invalid case throws the *specific* `Descriptor`, and that two equal values compare equal — the underlying value is what gets persisted and serialized, so a change to it is a breaking change.

## Checklist

- [ ] Derive from `ValueObject<TSelf, TUnderlyingValue>` and implement `IValueObjectParser<TSelf, TUnderlyingValue>`; make it a `sealed record`.
- [ ] Put all normalisation and invariant checks in `Validate`; keep the constructor private.
- [ ] Implement `Create` (typed value), `Parse` (text), and `ToParseableString`; expose a `MaxLength` const for string-backed types.
- [ ] Reuse the shipped objects (`Money`, `MoneyAmount`, `EmailAddress`, `Quantity`, `AddressLine`) and compose with [smart-enumerations](smart-enumerations) instead of flattening to primitives.
- [ ] Register via `AddEksen(...).AddValueObjects(vo => vo.Configure(o => o.AddAssembly(...)).AddAspNetCoreSupport())`.
- [ ] Persist with `.HasConversion(x => x.Value, x => T.Create(x)).HasMaxLength(T.MaxLength)` — see the [entity-framework-core](entity-framework-core) skill.
- [ ] Raise validation failures via an `ErrorDescriptor` (`ErrorType.Validation` → 400) — see the [error-handling](error-handling) skill.
- [ ] Use `TryParse`/`TryCreate` on untrusted input; reserve `Parse`/`Create` for trusted paths.
