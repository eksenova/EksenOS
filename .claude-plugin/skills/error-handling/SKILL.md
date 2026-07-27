---
name: error-handling
description: The EksenOS way to raise domain errors with Eksen.ErrorHandling — declare an ErrorDescriptor with a stable code and an ErrorType, raise an ErrorInstance carrying data, throw it as an EksenException, and let the ASP.NET Core EksenExceptionHandler map it to the right HTTP status and JSON body. Use when you would otherwise throw a bare exception, return a magic error string/code, or hand-roll a 404/409/422 response.
---

# Error Handling (Eksen.ErrorHandling)

A **domain error** in EksenOS is a value, not an ad-hoc exception. You declare an `ErrorDescriptor` once — it pins a stable `Code` (`Category.Name`) and an `ErrorType` (`NotFound`, `Validation`, `Conflict`, `Authorization`, `RateLimit`) — then `Raise(...)` an `ErrorInstance` that carries structured data and `throw` it. The thrown `EksenException` flows to the edge, where the `EksenExceptionHandler` turns the `ErrorType` into an HTTP status and writes a uniform `{ errorMessage, errorData }` body. Prefer this over a raw `throw new Exception(...)` or returning a sentinel code whenever the failure is part of your domain (a missing order, an illegal status transition, an over-sell).

All examples use the marketplace's e-commerce running example.

## Declaring errors

Group your descriptors in a static class and give them a **category** prefix. Declare each one as a `public static readonly` field — the constructor takes the `code` via `[CallerMemberName]`, so the field name *is* the code:

```csharp
using Eksen.ErrorHandling;

public static class OrderErrors
{
    private const string Category = "Orders";
    public static readonly ErrorDescriptor OrderAlreadyShipped = new(ErrorType.Conflict, Category);
    public static readonly ErrorDescriptor PaymentNotAuthorised = new(ErrorType.Validation, Category);
}
```

`OrderErrors.OrderAlreadyShipped.Code == "Orders.OrderAlreadyShipped"` and `.ErrorType == "Conflict"`. The `Code` is the contract that gets logged, returned, and (optionally) localized, so **keep codes stable** even if you rename the field. Category, code, and error type are all validated as non-blank at construction.

A plain `ErrorDescriptor` has no parameters; `Raise()` returns an empty `ErrorInstance`:

```csharp
if (order.Status == OrderStatus.Shipped)
{
    throw OrderErrors.OrderAlreadyShipped.Raise();
}
```

### Parameterized errors

When an error needs data, declare an `ErrorDescriptor<TDelegate>` with a delegate that builds the `ErrorInstance`. The `self => ...` factory receives the descriptor and returns the raise delegate:

```csharp
public static class OrderErrors
{
    private const string Category = "Orders";

    public delegate ErrorInstance InsufficientStockDelegate(Sku sku, Quantity requested, Quantity available);

    public static readonly ErrorDescriptor<InsufficientStockDelegate> InsufficientStock = new(
        ErrorType.Validation,
        Category,
        self => (sku, requested, available) =>
            new ErrorInstance(self)
                .WithValue(sku)
                .WithValue(requested)
                .WithValue(available));
}
```

`Raise` is invoked as a strongly-typed call — the compiler enforces the argument shape:

```csharp
if (available < requested)
{
    throw OrderErrors.InsufficientStock.Raise(item.Sku, requested, available);
}
```

## Raising and throwing

An `ErrorInstance` is an immutable record pairing a descriptor with a `Data` dictionary. The fluent builders each return a **new** instance:

| Member | Behaviour |
|---|---|
| `new ErrorInstance(descriptor)` | Starts an empty instance. |
| `WithData(string key, object? value)` | Adds/overwrites one entry. |
| `WithData(Dictionary<string,object?>)` | Merges a dictionary in. |
| `WithValue(value)` | Adds the value keyed by its **argument expression** (`WithValue(orderId)` → key `"orderId"`). A `Type` is stored as its `Name`; other non-string values are converted to string via their `TypeConverter` when one exists. |
| implicit `operator Exception` | Converts to an `EksenException`, so you can `throw` an `ErrorInstance` directly. |

Because of the implicit conversion, `throw someErrorInstance;` is idiomatic — there is no need to wrap it yourself. If you do need the exception object, `EksenException` exposes the originating `Descriptor` and copies the instance `Data`; its `Message` is the descriptor `Code`.

```csharp
var error = OrderErrors.PaymentNotAuthorised.Raise()
    .WithValue(order.OrderNumber)
    .WithData("paymentStatus", PaymentStatus.Failed.Code);

throw error;   // implicitly becomes an EksenException
```

## CommonErrors

`CommonErrors` ships the cross-cutting descriptors every app needs, under the `Eksen` category:

| Descriptor | Type | Raise |
|---|---|---|
| `CommonErrors.Unauthorized` | `Authorization` | `Unauthorized.Raise()` |
| `CommonErrors.ObjectNotFound` | `NotFound` | `ObjectNotFound.Raise(Type type, object? id = null)` |
| `CommonErrors.ObjectsNotFound` | `NotFound` | `ObjectsNotFound.Raise(Type type, ICollection<object>? ids = null)` |

Use `ObjectNotFound` whenever you look up an aggregate by id and come back empty — it records the aggregate `type` (as its name) and the `id`:

```csharp
var order = await ordersRepository.FindAsync(orderId, cancellationToken);
if (order is null)
{
    throw CommonErrors.ObjectNotFound.Raise(typeof(Order), orderId);
}
```

This is the same error a smart-enum `Parse` of an unknown code raises (see the smart-enumerations skill) and that repository lookups surface (see the repositories skill) — so an unknown `OrderStatus` code and a missing `Order` both land as one consistent `NotFound` at the edge.

## Registration

Register through the `IEksenBuilder` root from the core skill. `AddErrorHandling` wires the `IErrorFormatter` / `IErrorMessageTemplateResolver`; `AddAspNetCoreSupport()` plugs the exception handler into ASP.NET Core's pipeline:

```csharp
services.AddEksen(eksen => eksen
    .AddErrorHandling(errors => errors
        .AddAspNetCoreSupport()));   // registers EksenExceptionHandler as IExceptionHandler
```

`AddAspNetCoreSupport()` calls `AddExceptionHandler<EksenExceptionHandler>`, so you still activate the standard middleware in your request pipeline:

```csharp
app.UseExceptionHandler();
```

The descriptors themselves are plain static fields — there is nothing to scan or register for them.

## ASP.NET Core: status mapping and response body

`EksenExceptionHandler` only handles `EksenException` (unwrapping one level of `InnerException` to find it); any other exception is left for the next handler. It maps `ErrorType` to a status code:

| `ErrorType` | HTTP status |
|---|---|
| `NotFound` | 404 Not Found |
| `Validation` | 400 Bad Request |
| `Conflict` | 409 Conflict |
| `Authorization` | 401 Unauthorized |
| `RateLimit` | 429 Too Many Requests |
| *(anything else)* | 500 Internal Server Error |

The body is an `ErrorResponseBody` — the formatted `errorMessage` plus the instance `Data` as `errorData`. `errorData` is omitted entirely when the instance carried no data:

```jsonc
// 409 from OrderErrors.OrderAlreadyShipped.Raise() with no data
{ "errorMessage": "Orders.OrderAlreadyShipped" }

// 400 from InsufficientStock.Raise(sku, requested, available)
{
  "errorMessage": "Orders.InsufficientStock",
  "errorData": { "sku": "SKU-12", "requested": "5", "available": "2" }
}
```

Every handled error is also logged with the request `TraceIdentifier` and a printable dump of the error data.

## Messages and localization

`errorMessage` is produced by `IErrorFormatter`, which resolves a template via `IErrorMessageTemplateResolver` and fills it from the instance `Data`. The default `NullErrorMessageTemplateResolver` returns the **code itself**, so out of the box `errorMessage` is the stable code (e.g. `"Orders.InsufficientStock"`). To return human-readable, parameterized text per code, supply a real resolver backed by localization resources — see the localization skill.

## Testing

Pin the descriptor contract — `Code`, `ErrorType`, and the data a raise attaches — and assert that domain rules throw an `EksenException`:

```csharp
[Fact]
public void OrderAlreadyShipped_Is_A_Conflict()
{
    OrderErrors.OrderAlreadyShipped.ErrorType.ShouldBe(ErrorType.Conflict);
    OrderErrors.OrderAlreadyShipped.Code.ShouldBe("Orders.OrderAlreadyShipped");
}

[Fact]
public void InsufficientStock_Carries_Request_Detail()
{
    var instance = OrderErrors.InsufficientStock.Raise(Sku.Parse("SKU-12"), requested: Quantity.Of(5), available: Quantity.Of(2));

    instance.Descriptor.ShouldBe(OrderErrors.InsufficientStock);
    instance.Data.ShouldContainKey("requested");
}

[Fact]
public void Shipping_A_Shipped_Order_Throws()
{
    Should.Throw<EksenException>(() => shippedOrder.MarkShipped());
}
```

Treat a code or `ErrorType` change as breaking — it changes a logged identifier and the HTTP status a client sees.

## Checklist

- [ ] Declare errors as `public static readonly ErrorDescriptor` fields in a category-scoped static class; the field name is the code.
- [ ] Pick the `ErrorType` deliberately — it drives the HTTP status (`NotFound`→404, `Validation`→400, `Conflict`→409, `Authorization`→401, `RateLimit`→429).
- [ ] Use `ErrorDescriptor<TDelegate>` with a `self => ...` factory when the error needs data; attach it with `WithValue(...)` / `WithData(...)`.
- [ ] `throw` the `ErrorInstance` directly (implicit conversion to `EksenException`); reach for `CommonErrors.ObjectNotFound`/`Unauthorized` for the cross-cutting cases.
- [ ] Register via `AddEksen(...).AddErrorHandling(e => e.AddAspNetCoreSupport())` and call `app.UseExceptionHandler()`.
- [ ] Keep codes stable; supply an `IErrorMessageTemplateResolver` via the localization skill for human-readable `errorMessage` text.
