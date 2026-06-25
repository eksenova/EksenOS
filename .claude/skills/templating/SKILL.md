---
name: templating
description: The EksenOS way to turn a domain model into HTML and PDF — render Razor templates (order-confirmation emails, receipts) with Eksen.Templating's ITemplateHtmlRenderer, then convert the HTML to a PDF via IHtmlPdfRenderer. Use when you need a templated email body, an HTML document, or a generated PDF from order/shipment data.
---

# Templating (Eksen.Templating)

**Eksen.Templating** renders text by feeding a model into a template, and optionally converts that rendered HTML into a PDF. It is two small abstractions over two engines: `ITemplateHtmlRenderer` compiles a Razor (`.cshtml`) template with [RazorLight](https://github.com/toddams/razorlight), and `IHtmlPdfRenderer` ships the resulting HTML to a [Gotenberg](https://gotenberg.dev) service and gets back PDF bytes. Reach for it whenever a feature needs a templated email body, an HTML receipt, or a downloadable PDF built from domain data — not string concatenation.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Shipment`, `Payment`).

## The two abstractions

```csharp
// Eksen.Templating.Html
public interface ITemplateHtmlRenderer
{
    Task<string> RenderTemplateAsync<TModel>(
        string templateKey,
        TModel model,
        ExpandoObject? viewBag = null,
        CancellationToken cancellationToken = default);
}

// Eksen.Templating.Pdf
public interface IHtmlPdfRenderer
{
    Task<byte[]> ConvertAsync(string html, CancellationToken cancellationToken = default);
}
```

`RenderTemplateAsync` returns the rendered HTML string; `ConvertAsync` returns the PDF as a `byte[]`. They compose: render first, convert second.

## Registration

Register both renderers through the `IEksenBuilder` root — see the **core** skill for `AddEksen`. The HTML renderer takes an `Action<RazorLightEngineBuilder>` so you choose where templates live; the PDF renderer takes the Gotenberg base URL:

```csharp
services.AddEksen(eksen => eksen
    .AddRazorHtmlRenderer(razor => razor
        .UseEmbeddedResourcesProject(typeof(OrderConfirmationModel).Assembly, "Shop.EmailTemplates"))
    .AddGotenbergPdfRenderer(baseUrl: "http://gotenberg:3000"));
```

`AddRazorHtmlRenderer` already wires `UseMemoryCachingProvider()`, builds the `IRazorLightEngine` as a singleton, and registers `ITemplateHtmlRenderer` — your action only needs to point the engine at a template source. `AddGotenbergPdfRenderer` registers `IHtmlPdfRenderer` on a typed `HttpClient` whose `BaseAddress` is the URL you pass.

### Choosing a template source

The action is a raw `RazorLightEngineBuilder`, so any RazorLight project works:

```csharp
// Templates embedded as resources in your assembly (the root namespace is optional;
// when set, you omit it from the template key):
.AddRazorHtmlRenderer(razor => razor
    .UseEmbeddedResourcesProject(typeof(OrderConfirmationModel).Assembly, "Shop.EmailTemplates"))

// Templates as files on disk:
.AddRazorHtmlRenderer(razor => razor
    .UseFileSystemProject("/app/templates"))
```

## Rendering HTML from a model

The `templateKey` is whatever the configured RazorLight project understands — an embedded resource name (minus the root namespace) or a file path. Pass a strongly-typed model; the template binds it as `@Model`:

```csharp
public sealed record OrderConfirmationModel(
    OrderNumber OrderNumber,
    string CustomerName,
    OrderStatus Status,
    Money Total,
    IReadOnlyList<OrderLineModel> Lines
);

public sealed record OrderLineModel(
    string ProductName,
    Quantity Quantity,
    Money LineTotal
);

public sealed class OrderConfirmationEmailFactory(ITemplateHtmlRenderer renderer)
{
    public Task<string> BuildBodyAsync(Order order, Customer customer, CancellationToken ct)
    {
        var model = new OrderConfirmationModel(
            order.OrderNumber,
            customer.Name,
            order.Status,
            order.Total,
            order.Items
                .Select(i => new OrderLineModel(i.ProductName, i.Quantity, i.LineTotal))
                .ToList());

        // Resolves embedded resource "Shop.EmailTemplates.OrderConfirmation.cshtml"
        return renderer.RenderTemplateAsync("OrderConfirmation.cshtml", model, cancellationToken: ct);
    }
}
```

A matching `OrderConfirmation.cshtml` uses ordinary Razor:

```cshtml
@model Shop.Emails.OrderConfirmationModel
<h1>Thanks for your order, @Model.CustomerName!</h1>
<p>Order <strong>@Model.OrderNumber</strong> is now @Model.Status.</p>
<ul>
@foreach (var line in Model.Lines)
{
    <li>@line.Quantity × @line.ProductName — @line.LineTotal</li>
}
</ul>
<p>Total: <strong>@Model.Total</strong></p>
```

Because the model carries real domain types, render-time formatting comes from their `ToString()` — value objects (`OrderNumber`, `Money`, `Quantity`) from the **value-objects** skill and smart enums (`OrderStatus`) from the **smart-enumerations** skill all interpolate as their canonical string.

### Per-render data via the ViewBag

Pass an `ExpandoObject` for data that is not part of the model — a tracking link, a unit-tested heading, a locale token (pair with the **localization** skill for translated strings). The template reads it as `@ViewBag`:

```csharp
dynamic viewBag = new ExpandoObject();
viewBag.SupportEmail = "help@shop.example";
viewBag.TrackingUrl = $"https://shop.example/track/{shipment.TrackingNumber}";

var html = await renderer.RenderTemplateAsync(
    "ShipmentDispatched.cshtml", shipmentModel, viewBag, ct);
```

```cshtml
<p>Your @Model.Carrier parcel is on its way: <a href="@ViewBag.TrackingUrl">track it</a>.</p>
<p>Questions? <a href="mailto:@ViewBag.SupportEmail">@ViewBag.SupportEmail</a></p>
```

## Rendering a PDF

Convert any HTML string — typically the output of `RenderTemplateAsync` — into PDF bytes. The renderer posts to Gotenberg's `/forms/chromium/convert/html` endpoint and returns A4 (210×297 mm) pages with 10 mm margins and backgrounds printed:

```csharp
public sealed class OrderReceiptPdfFactory(
    ITemplateHtmlRenderer renderer,
    IHtmlPdfRenderer pdfRenderer
)
{
    public async Task<byte[]> BuildReceiptAsync(Order order, Payment payment, CancellationToken ct)
    {
        var model = new OrderReceiptModel(order.OrderNumber, order.Total, payment.Status);
        var html = await renderer.RenderTemplateAsync("OrderReceipt.cshtml", model, cancellationToken: ct);
        return await pdfRenderer.ConvertAsync(html, ct);
    }
}
```

The returned `byte[]` is a complete PDF — stream it from an endpoint as `application/pdf`, attach it to an email, or persist it. Keep template CSS self-contained (inline styles or a `<style>` block); Gotenberg renders the HTML headless, so external stylesheet paths must be absolutely resolvable.

## Errors

Both calls surface their underlying engine's failures rather than swallowing them:

- A missing `templateKey`, a Razor compile error, or a binding mismatch throws from RazorLight inside `RenderTemplateAsync`.
- A non-success response from Gotenberg makes `ConvertAsync` throw `HttpRequestException` (it calls `EnsureSuccessStatusCode`).

Let these bubble — at the API edge the `EksenExceptionHandler` from the **error-handling** skill maps unhandled exceptions to a 500, and you can catch-and-rethrow as a domain `EksenException` when a missing template is genuinely a business condition rather than a bug.

## Pairing with email

Templating produces the *body*; sending it is the **emailing** skill's job. The two compose directly — render with `ITemplateHtmlRenderer`, then put the HTML into a `SendEmailParameters` and hand it to `IEmailSender.SendEmailAsync`:

```csharp
var html = await renderer.RenderTemplateAsync("OrderConfirmation.cshtml", model, cancellationToken: ct);
await emailSender.SendEmailAsync(new SendEmailParameters
{
    Subject = "Your order is confirmed",
    To =
    [
        new EmailInstance
        {
            ToAddress = customer.Email,
            Content = html,
            ContentType = EmailContentType.Html,
        },
    ],
}, ct);
```

## Testing

Both renderers are plain interfaces — mock them where you test the orchestration, and test templates against the real engine. The `IRazorLightEngine` is the seam under `ITemplateHtmlRenderer`; the typed `HttpClient` is the seam under `IHtmlPdfRenderer`:

```csharp
[Fact]
public async Task BuildBody_Renders_With_Order_Data()
{
    var renderer = new Mock<ITemplateHtmlRenderer>();
    renderer
        .Setup(r => r.RenderTemplateAsync(
            "OrderConfirmation.cshtml",
            It.IsAny<OrderConfirmationModel>(),
            It.IsAny<ExpandoObject?>(),
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("<h1>Thanks!</h1>");

    var html = await new OrderConfirmationEmailFactory(renderer.Object)
        .BuildBodyAsync(order, customer, CancellationToken.None);

    html.ShouldContain("Thanks");
}
```

For PDF conversion, drive `GotenbergHtmlPdfRenderer` with a fake `HttpMessageHandler`: assert it POSTs `MultipartFormDataContent` to `/forms/chromium/convert/html`, returns the response body bytes, and throws on a non-2xx status. See the **test-base** skill for `EksenUnitTestBase`.

## Checklist

- [ ] Register via `AddEksen(...).AddRazorHtmlRenderer(razor => ...)` and, if you need PDFs, `.AddGotenbergPdfRenderer(baseUrl)`.
- [ ] Point the `RazorLightEngineBuilder` at a template source (`UseEmbeddedResourcesProject` or `UseFileSystemProject`); memory caching is already wired.
- [ ] Match the `templateKey` to the project — embedded-resource name (minus root namespace) or file path.
- [ ] Build a strongly-typed model from domain types; let value objects and smart enums format themselves via `ToString()`.
- [ ] Use the `ExpandoObject` `viewBag` for per-render extras (links, headings, localized tokens) read as `@ViewBag`.
- [ ] Render HTML with `ITemplateHtmlRenderer`, then `IHtmlPdfRenderer.ConvertAsync` for a PDF `byte[]`; keep template CSS self-contained.
- [ ] Hand the rendered HTML to the **emailing** skill's `IEmailSender` to send it; let render/convert failures surface to the **error-handling** skill.
