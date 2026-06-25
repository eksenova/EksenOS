---
name: open-api
description: OpenAPI document polish for EksenOS APIs with Eksen.OpenApi — a set of IOpenApiSchemaTransformer implementations that render plain C# enums as strings, inject example values via [ExampleValue], drop [NotMapped] properties from request/response schemas, and flag [Obsolete] types as deprecated. Use when your generated OpenAPI document leaks internal fields, shows enums as integers, or needs examples and deprecation notices.
---

# OpenAPI Schema Transformers (Eksen.OpenApi)

`Eksen.OpenApi` is a small bag of **schema transformers** for the built-in ASP.NET Core OpenAPI pipeline (`Microsoft.AspNetCore.OpenApi`). Each one is an `IOpenApiSchemaTransformer` that fixes a single recurring wart in the generated document: plain enums serialized as integers, internal `[NotMapped]` properties leaking onto the wire, missing example values, and removed-but-not-yet-deleted types that should read as deprecated. There is no Eksen builder wrapper here — these are raw transformers you hand to `AddOpenApi`.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Product`, `Shipment`, `Payment`).

## What's in the box

| Transformer | Trigger | Effect on the schema |
|---|---|---|
| `EnumStringSchemaTransformer` | The schema's CLR type is a plain C# `enum` | Switches `type` from integer to `string` and lists the member names (honouring `[EnumMember(Value = …)]`) in `enum`. |
| `ExampleValueSchemaTransformer` | A property/field carries `[ExampleValue(…)]` | Sets the schema's `example` to the serialized attribute value. |
| `NotMappedSchemaTransformer` | An object's property/field carries `[NotMapped]` | Removes that property from the object schema entirely. |
| `ObsoleteSchemaTransformer` | The schema's CLR type carries `[Obsolete("…")]` | Sets `deprecated: true` and appends `**Deprecated**: <message>` to the description. |

All four live in the `Eksen.OpenApi` namespace and are `sealed`.

## Registration

These transformers plug straight into the framework's `AddOpenApi(...)` call via `AddSchemaTransformer<T>()`. Register the ones you want — there is no all-in-one `AddOpenApiSupport()` on this package:

```csharp
using Eksen.OpenApi;

builder.Services.AddOpenApi("v1", options =>
{
    options.AddSchemaTransformer<EnumStringSchemaTransformer>();
    options.AddSchemaTransformer<ExampleValueSchemaTransformer>();
    options.AddSchemaTransformer<NotMappedSchemaTransformer>();
    options.AddSchemaTransformer<ObsoleteSchemaTransformer>();
});
```

Then map the document as usual:

```csharp
app.MapOpenApi();
```

The domain's closed value sets — `OrderStatus`, `PaymentStatus`, `ShipmentCarrier` — are **smart enumerations**, not plain C# enums, and they bring their own OpenAPI transformer; register those through `AddSmartEnums(...).AddOpenApiSupport()` (see the **smart-enumerations** skill). Strongly-typed ULID ids (`OrderId`, `CustomerId`) likewise have their own schema transformer (see the **ulid** skill). `EnumStringSchemaTransformer` here is for any residual *plain* `enum` still on your API surface.

## Enums as strings

Reach for a smart enumeration for genuine domain status/type sets. For an incidental plain `enum` — say a shipping-speed selector on an order request — `EnumStringSchemaTransformer` keeps the wire contract human-readable:

```csharp
using System.Runtime.Serialization;

public enum ShippingSpeed
{
    Standard,

    [EnumMember(Value = "express")]
    Express,

    Overnight,
}

public sealed record PlaceOrderRequest(
    OrderNumber OrderNumber,
    ShippingSpeed ShippingSpeed
);
```

Without the transformer the OpenAPI document types `shippingSpeed` as an integer. With it registered, the property becomes a string with the member names enumerated, and any `[EnumMember(Value = …)]` override wins over the field name:

```yaml
shippingSpeed:
  type: string
  enum: [Standard, express, Overnight]
```

The transformer resolves the underlying type first (via `TypeHelper.GetUnderlyingType`), so nullable enums (`ShippingSpeed?`) and enum collections are handled too.

## Example values

Annotate a DTO property with `[ExampleValue(…)]` and `ExampleValueSchemaTransformer` writes it into the schema's `example`, so the generated document and any UI render a realistic sample instead of a zero/empty placeholder:

```csharp
using Eksen.OpenApi;

public sealed record CustomerSummaryDto
{
    [ExampleValue("Ada Lovelace")]
    public required string DisplayName { get; init; }

    [ExampleValue(3)]
    public required int OpenOrderCount { get; init; }
}
```

```yaml
displayName:
  type: string
  example: Ada Lovelace
openOrderCount:
  type: integer
  example: 3
```

`[ExampleValue]` accepts any `object?` and targets properties or fields. The value is a
compile-time constant serialized with the member's JSON type info, so its type must match the
member — which means `[ExampleValue]` applies to the residual *primitive* members (counts,
flags, free-form labels). Value objects, smart enumerations, and ULID ids carry their examples
through their own schema transformers (see the **value-objects**, **smart-enumerations**, and
**ulid** skills), so you never annotate those. A `null` value is ignored.

## Hiding internal properties

A property you persist or compute but never want on the wire gets `[NotMapped]` (from `System.ComponentModel.DataAnnotations.Schema`); `NotMappedSchemaTransformer` strips it from the object schema. This is the same attribute EF Core honours, so a field excluded from the database column set stays out of the API document too — one annotation, both boundaries:

```csharp
using System.ComponentModel.DataAnnotations.Schema;

public sealed class Order
{
    public OrderId Id { get; private set; }

    public OrderNumber OrderNumber { get; private set; }

    public OrderStatus Status { get; private set; }

    [NotMapped]
    public decimal InternalRiskScore { get; private set; }
}
```

`InternalRiskScore` is dropped from the `Order` schema. The transformer only touches object schemas with a properties collection; everything else passes through untouched.

## Marking types deprecated

Tag a request/response type with `[Obsolete("…")]` and `ObsoleteSchemaTransformer` marks its schema `deprecated: true` and folds the message into the description, so consumers reading the spec see why and what to use instead:

```csharp
[Obsolete("Use ShipmentDto instead — Shipment now carries the carrier value object.")]
public sealed record LegacyShipmentDto(
    OrderNumber OrderNumber,
    ShipmentCarrier Carrier
);
```

```yaml
LegacyShipmentDto:
  deprecated: true
  description: |
    **Deprecated**: Use ShipmentDto instead — Shipment now carries the carrier value object.
```

An existing description is preserved; the deprecation note is appended on a new line.

## Pairing with sibling packages

`Eksen.OpenApi` only shapes schemas. The rest of the API surface is documented by the packages that own those types:

- **smart-enumerations** — `OrderStatus`, `PaymentStatus`, `ShipmentCarrier` render as strings with their codes via `AddSmartEnums(...).AddOpenApiSupport()`, not via `EnumStringSchemaTransformer`.
- **ulid** — strongly-typed ids (`OrderId`, `CustomerId`) get their string format and schema from `Eksen.Ulid.OpenApi`.
- **value-objects** — `Money`, `OrderNumber`, `EmailAddress` bind and serialize through `Eksen.ValueObjects`; transform their schemas there.
- **scalar** — once the document is clean, surface it through the Scalar API reference UI; see the **scalar** skill.
- **error-handling** — the `EksenExceptionHandler` defines the error response shape consumers see for 4xx/5xx; document those via the **error-handling** skill.

## Testing

Each transformer is a plain class with a `TransformAsync(schema, context, ct)` method, so you can unit-test it directly against an `OpenApiSchema` and an `OpenApiSchemaTransformerContext` built from a `JsonTypeInfo` — no web host required:

```csharp
[Fact]
public async Task Plain_Enum_Becomes_A_String_Schema()
{
    var schema = new OpenApiSchema { Type = JsonSchemaType.Integer };
    var context = CreateContext(typeof(ShippingSpeed)); // builds JsonTypeInfo for the type

    await new EnumStringSchemaTransformer()
        .TransformAsync(schema, context, CancellationToken.None);

    (schema.Type!.Value & JsonSchemaType.String).ShouldBe(JsonSchemaType.String);
    (schema.Type!.Value & JsonSchemaType.Integer).ShouldBe((JsonSchemaType)0);
}

[Fact]
public async Task NotMapped_Property_Is_Removed()
{
    var schema = new OpenApiSchema
    {
        Type = JsonSchemaType.Object,
        Properties = new Dictionary<string, IOpenApiSchema>
        {
            ["OrderNumber"] = new OpenApiSchema { Type = JsonSchemaType.String },
            ["InternalRiskScore"] = new OpenApiSchema { Type = JsonSchemaType.Number },
        },
    };
    var context = CreateContext(typeof(Order));

    await new NotMappedSchemaTransformer()
        .TransformAsync(schema, context, CancellationToken.None);

    schema.Properties.ShouldContainKey("OrderNumber");
    schema.Properties.ShouldNotContainKey("InternalRiskScore");
}
```

Build the context from a `JsonSerializerOptions.GetTypeInfo(type)` and assert the post-transform schema. Use the `EksenUnitTestBase` from the **test-base** skill for the fixture.

## Checklist

- [ ] Register only the transformers you need inside `AddOpenApi("v1", options => options.AddSchemaTransformer<T>())`; call `app.MapOpenApi()`.
- [ ] Use smart enumerations (and their `AddOpenApiSupport()`) for domain status/type sets; reserve `EnumStringSchemaTransformer` for plain C# enums.
- [ ] Override an enum member's wire value with `[EnumMember(Value = "…")]` when the field name isn't the contract.
- [ ] Add `[ExampleValue(…)]` to primitive DTO members for realistic samples; keep the constant's type matching the member (value objects, smart enums, and ULID ids get examples from their own transformers).
- [ ] Mark internal-only properties `[NotMapped]` to drop them from both the schema and the EF Core model.
- [ ] Tag retiring types `[Obsolete("use X instead")]` so the document shows `deprecated: true` with guidance.
- [ ] Unit-test each transformer directly against an `OpenApiSchema` + `OpenApiSchemaTransformerContext`.
