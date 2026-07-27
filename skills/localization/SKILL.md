---
name: localization
description: The EksenOS way to turn message templates into final user-facing strings with Eksen.Localization — register an IMessageFormatter, pass named FormatParameter values, and let SmartFormat substitute {Key} placeholders in error messages, notifications, and rendered copy. Use when you have a message string with named holes ("Order {OrderNumber} shipped via {Carrier}") that must be filled with runtime values.
---

# Localization (Eksen.Localization)

`Eksen.Localization` is a thin, single-purpose service: it takes a **message template** containing named placeholders and fills them with runtime values to produce the final string. The one abstraction is `IMessageFormatter`; the one implementation, `SmartFormatMessageFormatter`, delegates to [SmartFormat](https://github.com/axuno/SmartFormat) so templates use `{Key}` (named) holes rather than positional `{0}` ones. This is the formatting layer you reach for when assembling user-facing text — error messages, order notifications, the body of a templated email — from a template plus a bag of values.

All examples use the marketplace's e-commerce running example (`Order`, `Product`, `Shipment`, `Payment`).

## Registration

Register through the `IEksenBuilder` root. `AddLocalization()` adds `IMessageFormatter` as a singleton:

```csharp
services.AddEksen(eksen => eksen
    .AddLocalization());
```

That is the whole surface — there are no options to configure. Once registered, inject `IMessageFormatter` anywhere.

## The API

Two public types live in `Eksen.Localization.Formatting`:

| Type | Shape |
|---|---|
| `FormatParameter` | `record FormatParameter(string Key, object? Value)` — one named value. `Value` may be `null`. |
| `IMessageFormatter` | `string FormatMessage(string message, params ICollection<FormatParameter> formatParameters)` |

`FormatMessage` replaces each `{Key}` in `message` with the matching `FormatParameter.Value`, leaving any text without placeholders untouched. Because the parameter list is `params`, pass the values inline or as an array:

```csharp
using Eksen.Localization.Formatting;

public sealed class OrderNotificationBuilder(IMessageFormatter formatter)
{
    public string BuildShippedNotice(Order order, Shipment shipment)
    {
        return formatter.FormatMessage(
            "Order {OrderNumber} shipped via {Carrier}. Track it any time.",
            new FormatParameter("OrderNumber", order.OrderNumber.Value),
            new FormatParameter("Carrier", shipment.Carrier.Code));
    }
}
```

```csharp
// => "Order ORD-1001 shipped via Ups. Track it any time."
```

Pass a prepared array when the parameter set is built dynamically:

```csharp
var parameters = new FormatParameter[]
{
    new("OrderNumber", order.OrderNumber.Value),
    new("Total", order.Total.Amount),
    new("Status", order.Status.DisplayName),
};

var line = formatter.FormatMessage(
    "Order {OrderNumber} — {Total} — {Status}", parameters);
```

## Template syntax

Templates are SmartFormat strings, so placeholders are matched by **name**, are case-sensitive against the `Key`, and may repeat:

```csharp
formatter.FormatMessage(
    "{Customer}, your order is ready. Thanks for shopping with us, {Customer}!",
    new FormatParameter("Customer", "Ada"));
// => "Ada, your order is ready. Thanks for shopping with us, Ada!"
```

Values are rendered with their default `ToString()`:

- A `null` `Value` renders as an empty string: `"Note: {Note}."` with `Note = null` → `"Note: ."`.
- Numbers and `Money` amounts render via their invariant text (e.g. `1234.56m` → `"1234.56"`); a `bool` renders as `"True"`/`"False"`.
- A template with no placeholders is returned unchanged.

When a value needs domain-friendly text, project it before passing it in — e.g. feed `order.Status.DisplayName` rather than the raw `OrderStatus` (see the smart-enumerations skill for the `DisplayName` pattern), and `order.Total.Amount` rather than the whole `Money` value object (see the value-objects skill).

## Where it fits

`IMessageFormatter` is a building block other concerns compose on:

- **Error messages** — pair with the error-handling skill. Build the human-readable text of an `EksenException` / `CommonErrors` payload from a template plus the offending values (`"Product {Sku} is out of stock."`) so the wording lives in one place.
- **Templated copy** — pair with the templating skill for full-document rendering (e.g. an order-confirmation email body). Use `IMessageFormatter` for short single-line fills and the template engine for multi-section documents; the rendered output then goes out through the emailing skill's `IEmailSender`.

## Testing

`SmartFormatMessageFormatter` has a parameterless constructor, so unit-test formatting without any container:

```csharp
public class OrderNotificationTests
{
    private readonly IMessageFormatter _formatter = new SmartFormatMessageFormatter();

    [Fact]
    public void Fills_Named_Placeholders()
    {
        _formatter.FormatMessage(
                "Order {OrderNumber} shipped via {Carrier}.",
                new FormatParameter("OrderNumber", "ORD-1001"),
                new FormatParameter("Carrier", "Ups"))
            .ShouldBe("Order ORD-1001 shipped via Ups.");
    }

    [Fact]
    public void Null_Value_Renders_Empty()
    {
        _formatter.FormatMessage(
                "Note: {Note}.", new FormatParameter("Note", null))
            .ShouldBe("Note: .");
    }
}
```

To assert registration, build the provider and resolve the service (this is exactly how the package's own tests verify the wiring):

```csharp
var services = new ServiceCollection();
services.AddEksen(eksen => eksen.AddLocalization());

var formatter = services.BuildServiceProvider().GetRequiredService<IMessageFormatter>();
formatter.ShouldBeOfType<SmartFormatMessageFormatter>();
```

The package also defines the `AppModules.Localization` app module (`"Eksen.Localization"`), which its static `AppModuleExtensions` registers with the core `AppModuleRegistry` — see the core skill.

## Checklist

- [ ] Register once via `AddEksen(eksen => eksen.AddLocalization())`; there are no options to set.
- [ ] Inject `IMessageFormatter`; never new up `SmartFormatMessageFormatter` outside tests.
- [ ] Write templates with **named** `{Key}` holes that match each `FormatParameter.Key` exactly (case-sensitive).
- [ ] Pass already-formatted, human-friendly values (`Status.DisplayName`, `Money.Amount`, `OrderNumber.Value`) — not raw aggregates or value objects.
- [ ] Expect `null` values to render as empty strings and parameter-free templates to pass through verbatim.
- [ ] Use it to build error-message text (error-handling skill) and short notification lines; reach for the templating skill for full documents.
