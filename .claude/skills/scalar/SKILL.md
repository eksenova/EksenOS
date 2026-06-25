---
name: scalar
description: The EksenOS way to brand and harden a Scalar API-reference UI for your OpenAPI document with Eksen.Scalar — chain client-side plugins (logo/footer branding, token-body and autofill fixes, tenant impersonation, an internal-auth wall) onto MapScalarApiReference. Use when you expose a Scalar reference for an EksenOS API and want consistent branding, OAuth2 token ergonomics, host-user gating, or tenant impersonation.
---

# Scalar (Eksen.Scalar)

**Scalar** renders an interactive API reference from your OpenAPI document. `Eksen.Scalar` is a set of composable **client-side plugins** that layer onto that reference: each is injected as an inline ES-module `<script>` on the page head, so they add no server endpoints and chain freely. They cover branding (sidebar logo + footer), OAuth2 token ergonomics (move client credentials into the token body, normalise autofill), tenant **impersonation** for host users, and an in-page **internal-auth wall** that gates a host-only document.

All examples use the marketplace's e-commerce running example — here, the order-management API whose `Order`, `Customer`, `Product`, `Shipment`, and `Payment` endpoints the reference documents.

## Mapping the reference

`Eksen.Scalar` has **no `AddEksen` registration** — the plugins are `ScalarOptions` extension methods, wired inside the `MapScalarApiReference` callback from `Scalar.AspNetCore`. You expose the OpenAPI document and the reference yourself, then chain the Eksen plugins:

```csharp
using Eksen.Scalar;
using Scalar.AspNetCore;

builder.Services.AddOpenApi();   // describe the order-management API — see the open-api skill

var app = builder.Build();

app.MapOpenApi();
app.MapScalarApiReference("/scalar", scalar => scalar
    .WithEksenLogoPlugin(logo =>
    {
        logo.LogoUrl = "/assets/orders-logo.svg";
        logo.FooterText = "(c) {year} Order Management";
    })
    .WithEksenAutofillPlugin()
    .WithEksenTokenBodyPlugin());
```

Every `WithEksen…` method returns the same `ScalarOptions`, so order does not matter and only the plugins you call are injected. The OpenAPI document itself (enum-as-string schemas, ULID formats, API-key security scheme) is shaped by the **open-api**, **smart-enumerations**, **ulid**, and **api-key-authentication** skills; Scalar just renders it.

## Branding the sidebar and footer

`WithEksenLogoPlugin` injects a sidebar logo and a fixed footer copyright badge, and by default tidies the chrome. `LogoUrl` may be absolute or app-relative; the literal `{year}` in the footer text/title is replaced at render time:

```csharp
app.MapScalarApiReference("/scalar", scalar => scalar
    .WithEksenLogoPlugin(logo =>
    {
        logo.LogoUrl = "https://cdn.example.com/orders-logo.png";
        logo.LogoAltText = "Order Management API";
        logo.FooterText = "(c) {year} Order Management";
        logo.FooterTitle = "Order Management API — {year}";
        logo.HideMcpControls = true;             // default: hide MCP-generation entries
        logo.CollapseSidebarCategories = true;   // default: collapse top-level categories on load
    }));
```

| `ScalarLogoOptions` | Default | Effect |
|---|---|---|
| `LogoUrl` | `null` | Sidebar logo image; `null` injects no image. |
| `LogoAltText` | `"Logo"` | Accessible name for the logo. |
| `FooterText` | `null` | Footer copyright badge; `null` shows no footer. |
| `FooterTitle` | `null` | Footer tooltip/title. |
| `HideMcpControls` | `true` | Hide sidebar entries advertising MCP server generation. |
| `CollapseSidebarCategories` | `true` | Collapse top-level categories on first load. |

The defaults carry nothing product-specific — set `LogoUrl`/`FooterText` to brand a given deployment.

## OAuth2 token ergonomics

Two plugins fix common quirks when "Try it out" calls a `/connect/token` endpoint with client credentials:

```csharp
app.MapScalarApiReference("/scalar", scalar => scalar
    .WithEksenAutofillPlugin()                                          // stop password managers / client-secret autofill
    .WithEksenTokenBodyPlugin(token => token.TokenEndpoint = "/connect/token"));
```

- `WithEksenAutofillPlugin()` — takes no options; normalises field autofill so password managers behave and client-credential fields are not auto-populated.
- `WithEksenTokenBodyPlugin(...)` — rewrites token requests so Basic client credentials are moved out of the `Authorization` header into the form body as `client_id`/`client_secret`. `ScalarTokenBodyOptions.TokenEndpoint` (default `/connect/token`) is the path it matches.

## Tenant impersonation

`WithEksenImpersonationPlugin` lets a **host** user (a non-tenant operator) exchange their bearer token for a tenant-scoped token and have it injected into subsequent calls — handy for reproducing a tenant's view of the order-management API. Defaults assume an OpenIddict-style `/connect/token` and a paged tenants query:

```csharp
app.MapScalarApiReference("/scalar", scalar => scalar
    .WithEksenImpersonationPlugin(impersonation =>
    {
        impersonation.TokenEndpoint = "/connect/token";
        impersonation.TenantsEndpoint = "/api/tenants?MaxResultCount=1000&Sorting=Name%20ASC";
        impersonation.GrantType = "tenant_impersonation";
        impersonation.ClientId = "scalar";              // fallback if none derivable from the captured token
        impersonation.HostClaim = "is_host";            // JWT claim marking a host (non-tenant) token
        impersonation.ImpersonatingClaim = "is_impersonating";
    }));
```

The `HostClaim`/`ImpersonatingClaim` names must match the claims your token issuer emits — these come from the identity layer (see the **identity** skill). The plugin only enables impersonation for tokens carrying `HostClaim`.

## Internal-auth wall

`WithEksenInternalAuthPlugin` gates the page behind an in-page sign-in card that **only host users pass**, for documents you do not want tenant users to browse — for example an internal admin slice of the order-management API. You map that internal Scalar reference (route, title, OpenAPI documents) yourself; the plugin only adds the auth wall:

```csharp
app.MapScalarApiReference("/scalar/internal", scalar => scalar
    .WithEksenInternalAuthPlugin(auth =>
    {
        auth.InternalDocumentPath = "/openapi/internal";   // the host-only OpenAPI document to gate
        auth.TokenEndpoint = "/connect/token";
        auth.ClientId = "scalar";
        auth.HostClaim = "is_host";                        // only host tokens pass the wall
        auth.BrandText = "Internal Order API";
        auth.SubtitleText = "Available to host operators only.";
    }));
```

`ClientSecret` (default `null`, usually empty for public clients) and `DefaultUsername` (default `null`) let you pre-fill the sign-in form. The wall is a client-side convenience for hiding the document in the UI; enforce the real host-only authorization server-side via the **permissions** and **identity** skills.

## Composition

Plugins are independent and the per-request options snapshot never accumulates — each config global is assigned exactly once per render. Chain only what you need; the rest stay out of the page:

```csharp
app.MapScalarApiReference("/scalar", scalar => scalar
    .WithEksenLogoPlugin(logo => logo.LogoUrl = "/assets/orders-logo.svg")
    .WithEksenAutofillPlugin()
    .WithEksenTokenBodyPlugin()
    .WithEksenImpersonationPlugin());
```

## Testing

Each plugin announces itself through a `window` config global on the rendered HTML, so assert presence/absence and configured values over HTTP against a real Scalar reference:

```csharp
[Fact]
public async Task Logo_Plugin_Injects_Configured_Url()
{
    await using var host = await ScalarTestHost.StartAsync(scalar => scalar
        .WithEksenLogoPlugin(logo =>
        {
            logo.LogoUrl = "https://cdn.example.com/orders-logo.png";
            logo.FooterText = "(c) {year} Order Management";
        }));

    var html = await host.GetReferenceHtmlAsync();

    html.ShouldContain("__eksenScalarLogoConfig");
    html.ShouldContain("https://cdn.example.com/orders-logo.png");
}

[Fact]
public async Task Plain_Reference_Injects_Nothing()
{
    await using var host = await ScalarTestHost.StartAsync(_ => { });

    var html = await host.GetReferenceHtmlAsync();

    html.ShouldNotContain("__eksenScalarLogoConfig");
    html.ShouldNotContain("__eksenScalarImpersonationConfig");
}
```

The config-global markers are: `__eksenScalarLogoConfig`, `__eksenScalarAutofillLoaded`, `__eksenScalarTokenBodyConfig`, `__eksenScalarImpersonationConfig`, `__eksenScalarInternalAuthConfig`. Enabling one plugin must not inject the others, and repeated requests render an identical page (the global is assigned exactly once).

## Checklist

- [ ] Map the reference with `app.MapScalarApiReference("/scalar", scalar => …)` — there is no `AddEksen` registration for this package.
- [ ] Shape the underlying OpenAPI document first (enum-as-string, ULID, API-key security) — see the **open-api**, **smart-enumerations**, **ulid**, and **api-key-authentication** skills.
- [ ] Brand per deployment with `WithEksenLogoPlugin` (`LogoUrl`, `FooterText` with `{year}`); the defaults are intentionally unbranded.
- [ ] Fix OAuth2 "Try it out" with `WithEksenAutofillPlugin()` and `WithEksenTokenBodyPlugin()` when client credentials hit `/connect/token`.
- [ ] Match `HostClaim`/`ImpersonatingClaim` to the claims your issuer emits (see the **identity** skill) before relying on `WithEksenImpersonationPlugin`.
- [ ] Treat `WithEksenInternalAuthPlugin` as UI gating only; enforce host-only access server-side via the **permissions** and **identity** skills.
- [ ] Chain only the plugins you need — each is independent and injected at most once per render.
- [ ] Assert plugins over the rendered HTML by their `window.__eksenScalar…Config` markers.
