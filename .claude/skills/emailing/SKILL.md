---
name: emailing
description: The EksenOS way to send transactional email with Eksen.Emailing — register an IEmailSender backed by SMTP or Gmail, push raw EmailInstance bodies, or render strongly-typed templates through ITemplateEmailSender. Use when an order-management flow must notify a customer (order confirmation, shipment dispatched, refund issued) and you want a provider-agnostic sender wired through the IEksenBuilder root.
---

# Emailing (Eksen.Emailing)

`Eksen.Emailing` is a thin, provider-agnostic email abstraction. Code depends on `IEmailSender` (raw bodies) or `ITemplateEmailSender` (render-then-send); the transport — in-box SMTP or the `Eksen.Emailing.Gmail` provider — is chosen at registration time. Addresses are the `EmailAddress` value object, so a malformed address never reaches the wire.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Shipment`, `Payment`).

## Registration

Register through the `IEksenBuilder` root. `AddEmailing` opens an `IEksenEmailingBuilder` (and wires value-object support for the emailing assembly via `AddValueObjects`); choose exactly one transport on it. `EmailAddress` itself ships in `Eksen.ValueObjects` — see the value-objects skill:

```csharp
services.AddEksen(eksen => eksen
    .AddEmailing(emailing => emailing
        .UseSmtp()             // binds SmtpConfiguration from the "Smtp" section
        .AddEmailTemplates())); // registers ITemplateEmailSender (HTML templates)
```

Swap the transport without touching call sites — the Gmail provider lives in `Eksen.Emailing.Gmail`:

```csharp
services.AddEksen(eksen => eksen
    .AddEmailing(emailing => emailing
        .UseGmail()            // binds GmailConfiguration from "GoogleCloud:Gmail"
        .AddEmailTemplates()));
```

`UseSmtp(configSectionPath)` and `UseGmail(configSectionPath)` each register `IEmailSender` as a singleton and bind their options with `ValidateDataAnnotations().ValidateOnStart()`, so a missing host or from-address fails fast at startup, not on first send. The defaults are the `"Smtp"` and `"GoogleCloud:Gmail"` configuration sections respectively.

`AddEmailTemplates()` registers `ITemplateEmailSender`, which renders bodies through `ITemplateHtmlRenderer` — register that separately (see the templating skill). Skip it if you only ever send pre-built bodies via `IEmailSender`.

## Sending a raw email

`IEmailSender.SendEmailAsync` takes `SendEmailParameters`, whose `To` is a collection of `EmailInstance` — one per recipient, each carrying its own body and optional per-recipient overrides:

```csharp
public sealed class OrderConfirmationNotifier(IEmailSender emailSender)
{
    public Task NotifyAsync(Order order, Customer customer, CancellationToken ct)
    {
        var parameters = new SendEmailParameters
        {
            Subject = $"Order {order.Number.Value} confirmed",
            To =
            [
                new EmailInstance
                {
                    ToAddress = customer.Email,           // EmailAddress value object
                    Content = $"<p>Thanks! Order {order.Number.Value} is {order.Status.Code}.</p>",
                    ContentType = EmailContentType.Html
                }
            ]
        };

        return emailSender.SendEmailAsync(parameters, ct);
    }
}
```

`EmailContentType` is `Html` or `Plaintext`. On SMTP an unset `ContentType` is treated as plain text (`IsBodyHtml` is true only for `Html`); the Gmail provider defaults an unset `ContentType` to `Html`.

### Subject, from-name and from-address resolution

Both the per-recipient `EmailInstance` and the envelope `SendEmailParameters` carry `Subject`, `FromName` and `FromAddress`. The sender resolves each by falling back in order:

- **Subject** — `EmailInstance.Subject` → `SendEmailParameters.Subject` → **throws** `ArgumentNullException` if both are null/blank.
- **FromName** — `EmailInstance.FromName` → `SendEmailParameters.FromName` → `DefaultFromName` from configuration.
- **FromAddress** — `EmailInstance.FromAddress` → `SendEmailParameters.FromAddress` → `DefaultFromAddress` from configuration.

So the common case — one subject and the configured default sender — needs only the envelope `Subject` and a bare `EmailInstance`. Override per recipient only when a single send fans out to recipients that need different subjects or reply-to identities.

## Sending a templated email

`ITemplateEmailSender.SendTemplateEmailAsync<TModel>` renders an HTML body per recipient and then delegates to `IEmailSender`. The `To` here is a collection of `EmailAddress` (not `EmailInstance`), and `TemplateKey` names the template:

```csharp
public sealed record OrderConfirmationModel
{
    public required OrderNumber OrderNumber { get; init; }

    public required string CustomerName { get; init; }

    public required Money Total { get; init; }
}

public sealed class OrderEmails(ITemplateEmailSender templateSender)
{
    public Task SendConfirmationAsync(Order order, Customer customer, CancellationToken ct)
    {
        var parameters = new SendTemplateEmailParameters<OrderConfirmationModel>
        {
            To = [customer.Email],
            Subject = $"Order {order.Number.Value} confirmed",
            TemplateKey = "OrderConfirmation",
            Model = new OrderConfirmationModel
            {
                OrderNumber = order.Number,
                CustomerName = customer.Name,
                Total = order.Total   // Money value object
            }
        };

        return templateSender.SendTemplateEmailAsync(parameters, ct);
    }
}
```

What the template sender does for each address in `To`:

- Wraps the model in `EmailViewModel<TModel>` (`{ To, Data = Model, Subject, FromName, FromAddress }`) — your template binds against `Data` and may set `Subject` on the view model.
- Renders `Emails/{TemplateKey}` via `ITemplateHtmlRenderer` — the `Emails/` folder prefix is added for you, so `TemplateKey = "OrderConfirmation"` resolves `Emails/OrderConfirmation`.
- Sends with `ContentType = EmailContentType.Html`.

Subject for a templated send resolves `SendTemplateEmailParameters.Subject` → the view model's `Subject` (a template may set it) → **throws** `ArgumentNullException` if both are blank. So a template can own its own subject line; otherwise pass one in.

Author the matching `Emails/OrderConfirmation` template and its model with the templating skill — `Eksen.Emailing` only orchestrates render-then-send.

## Configuration

`UseSmtp()` binds `SmtpConfiguration` from `"Smtp"`. All string/address/host/port/credential fields are `[Required]`:

```jsonc
{
  "Smtp": {
    "DefaultFromAddress": "orders@shop.example.com",
    "DefaultFromName": "Shop Orders",
    "Host": "smtp.example.com",
    "Port": 587,
    "UserName": "apikey",
    "Password": "…",
    "EnableTls": true            // default true
  }
}
```

`UseGmail()` binds `GmailConfiguration` from `"GoogleCloud:Gmail"` and authenticates via a service-account JSON key with domain-wide delegation:

```jsonc
{
  "GoogleCloud": {
    "Gmail": {
      "DefaultFromAddress": "orders@shop.example.com",
      "DefaultFromName": "Shop Orders",
      "ServiceAccountFile": "service-account.json",  // default; [Required]
      "ImpersonatedUser": "orders@shop.example.com", // optional; defaults to DefaultFromAddress
      "ApplicationName": "Eksen"                      // default
    }
  }
}
```

`EmailAddress` fields bind from their string form; the value-objects skill covers value-object binding in general. Because both options use `ValidateOnStart()`, a malformed default-from address or a missing host stops the host from starting.

## Errors

The sole sender-raised failure is an **`ArgumentNullException`** when no subject can be resolved (instance/envelope/template-model all blank) — this is an argument-guard, not an `EksenException`, so it is not mapped to a tidy HTTP response by the error-handling pipeline. Validate or default the subject before you call. Transport failures (SMTP connect, Gmail API) surface as the underlying provider exception. To convert a missing-recipient or unsendable-order situation into a domain error with a proper HTTP mapping, raise it yourself via the error-handling skill before reaching the sender.

## Testing

`IEmailSender` and `ITemplateEmailSender` are plain interfaces — mock them. To assert template orchestration, mock `ITemplateHtmlRenderer` and verify the `Emails/`-prefixed key and the resulting `SendEmailParameters`:

```csharp
[Fact]
public async Task Confirmation_Renders_Prefixed_Template_And_Sends_Html()
{
    var to = EmailAddress.Create("buyer@example.com");
    var renderer = new Mock<ITemplateHtmlRenderer>();
    renderer
        .Setup(r => r.RenderTemplateAsync(
            "Emails/OrderConfirmation",
            It.IsAny<EmailViewModel<OrderConfirmationModel>>(),
            null,
            It.IsAny<CancellationToken>()))
        .ReturnsAsync("<h1>Confirmed</h1>");

    var sender = new Mock<IEmailSender>();
    var sut = new TemplateEmailSender(sender.Object, renderer.Object);

    await sut.SendTemplateEmailAsync(new SendTemplateEmailParameters<OrderConfirmationModel>
    {
        To = [to],
        Subject = "Order ORD-1001 confirmed",
        TemplateKey = "OrderConfirmation",
        Model = new OrderConfirmationModel { OrderNumber = OrderNumber.Parse("ORD-1001"), CustomerName = "Ada", Total = Money.Parse("42.00 GBP") }
    });

    sender.Verify(s => s.SendEmailAsync(
        It.Is<SendEmailParameters>(p =>
            p.To.Count == 1 &&
            p.To.First().ToAddress == to &&
            p.To.First().Content == "<h1>Confirmed</h1>" &&
            p.To.First().ContentType == EmailContentType.Html),
        It.IsAny<CancellationToken>()), Times.Once);
}
```

Pin the contract: the `Emails/` prefix, that bodies are sent as `Html`, that a blank subject throws `ArgumentNullException`, and the fan-out — one `EmailInstance` (and one render call) per address in `To`. The Gmail MIME factory round-trips, so a unit test can decode `Message.Raw` back to assert subject/from/to without touching the network. Use the `EksenUnitTestBase` from the test-base skill.

## Checklist

- [ ] Register through `AddEksen(...).AddEmailing(...)` and pick exactly one transport: `UseSmtp()` or `UseGmail()`.
- [ ] Add `AddEmailTemplates()` only when you send templated bodies, and register `ITemplateHtmlRenderer` via the templating skill.
- [ ] Inject `IEmailSender` for pre-built bodies, `ITemplateEmailSender` for rendered templates.
- [ ] Type recipients as the `EmailAddress` value object (see the value-objects skill); never raw strings.
- [ ] Always supply a resolvable subject (instance, envelope, or template) — a blank one throws `ArgumentNullException`.
- [ ] Set `ContentType` explicitly (`Html`/`Plaintext`); don't rely on a transport's default.
- [ ] Configure `DefaultFromAddress`/`DefaultFromName` in the `"Smtp"` or `"GoogleCloud:Gmail"` section so envelope/instance from-fields are optional.
- [ ] Name templates by their key only; the sender prefixes `Emails/`.
