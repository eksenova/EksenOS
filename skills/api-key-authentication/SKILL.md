---
name: api-key-authentication
description: The EksenOS way to authenticate machine-to-machine callers with Eksen.Authentication.ApiKeys — issue per-user (and per-tenant) API keys as first-class entities, read them from a custom or Authorization header into a ClaimsPrincipal, persist them by EF Core, and surface them as an OpenAPI security scheme. Use when a partner integration, fulfilment service, or background job needs to call your API without an interactive login.
---

# API-Key Authentication (Eksen.Authentication.ApiKeys)

An **API key** is a long-lived secret a non-interactive caller presents on every request — a fulfilment partner's order-sync service, a warehouse integration polling shipments, a scheduled reconciliation job. Eksen.Authentication.ApiKeys models a key as a real entity (`IEksenApiKey<TId>`) carrying a name, a value, expiry, and revocation state, plugs into ASP.NET Core as a first-class `AuthenticationHandler`, and turns a valid key into a `ClaimsPrincipal` — so the rest of your pipeline (permissions, current-user, tenancy) sees an authenticated caller exactly as it would after a login.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Shipment`, `Payment`).

The family layers cleanly:

| Package | Adds |
|---|---|
| `Eksen.Authentication.ApiKeys` | Core contracts: `IEksenApiKey<TId>`, `IApiKeyAuthenticator<,>`, `IApiKeyGenerator`, the `ApiKeyName`/`ApiKeyValue` value objects, errors. |
| `Eksen.Authentication.ApiKeys.AspNetCore` | The authentication scheme + header-reading handler. |
| `Eksen.Authentication.ApiKeys.Identity` | A ready `EksenUserApiKey<TUser, TTenant>` entity + `DefaultUserApiKeyAuthenticator`. |
| `Eksen.Authentication.ApiKeys.Identity.EntityFrameworkCore` | EF Core repository, entity configuration, model-builder wiring. |
| `Eksen.Authentication.ApiKeys.OpenApi` | An OpenAPI document transformer that publishes the key as a security scheme. |

## The API key as an entity

`IEksenApiKey<TId>` is the contract every key satisfies — a ULID-keyed entity with a name, a value, and lifecycle state:

```csharp
public interface IEksenApiKey<out TId> : IEntity<TId, System.Ulid>
    where TId : IEntityId<TId, System.Ulid>
{
    ApiKeyName Name { get; }
    ApiKeyValue KeyValue { get; }
    DateTime? ExpiresAt { get; }
    DateTime? RevokedAt { get; }
    bool IsRevoked { get; }
    bool IsExpired { get; }
    bool IsActive { get; }
}
```

`Name` and `KeyValue` are `ValueObject<TSelf, string>` types (see the value-objects skill). `ApiKeyName` trims and caps at `ApiKeyName.MaxLength` (100); `ApiKeyValue` caps at `ApiKeyValue.MaxLength` (128). Both validate on `Create` and raise `ApiKeyErrors` on bad input; `ApiKeyValue` also has a non-throwing `TryCreate`:

```csharp
var name  = ApiKeyName.Create("Acme fulfilment sync");
var value = ApiKeyValue.Create(apiKeyGenerator.Generate());

if (!ApiKeyValue.TryCreate(rawHeader, out var parsed))
{
    /* malformed */
}
```

### The Identity-backed key

`Eksen.Authentication.ApiKeys.Identity` ships the concrete entity you'll almost always use — `EksenUserApiKey<TUser, TTenant>` — keyed by `EksenUserApiKeyId` (a ULID strongly-typed id, see the ulid skill) and tied to an `IEksenUser<TTenant>`/`IEksenTenant` from the identity skill. It implements `IHasCreationTime`/`IHasModificationTime` (audited via the entities and auditing skills) and `IMayHaveTenant<TTenant>`, so a key belongs to a user and optionally a tenant. Construct one with a generated value, then drive its lifecycle through behaviour methods:

```csharp
// AppUser : IEksenUser<AppTenant>, AppTenant : IEksenTenant — from the identity skill.
var key = new EksenUserApiKey<AppUser, AppTenant>(
    name: ApiKeyName.Create("Acme fulfilment sync"),
    keyValue: ApiKeyValue.Create(apiKeyGenerator.Generate()),
    user: partnerUser,        // the integration account that may read Shipments
    tenant: partnerUser.Tenant,
    expiresAt: DateTime.UtcNow.AddDays(90));

key.SetName(ApiKeyName.Create("Acme fulfilment sync (prod)"));
key.SetExpiresAt(DateTime.UtcNow.AddDays(180));
key.Regenerate(ApiKeyValue.Create(apiKeyGenerator.Generate())); // rotate the secret
key.Revoke();                                                    // disable the key
```

`Revoke()` throws `ApiKeyErrors.ApiKeyAlreadyRevoked` if called twice, and `Regenerate(...)` throws `ApiKeyErrors.ApiKeyRevoked` on a revoked key — these are domain invariants, not silent no-ops. `IsActive` is `!IsRevoked && !IsExpired`.

## Generating key values

Key values come from `IApiKeyGenerator`. The default `GuidApiKeyGenerator` returns a 32-char `Guid.NewGuid().ToString("N")` and is registered for you (`TryAddSingleton`) by `AddApiKeys`, so the abstraction is always injectable. Override the policy by deriving and replacing the registration:

```csharp
public sealed class PrefixedApiKeyGenerator : GuidApiKeyGenerator
{
    public override string Generate()
    {
        return $"ord_{base.Generate()}";
    }
}
```

## Registration

Everything hangs off the `IEksenBuilder` root (see the core skill). `AddApiKeys` opens an `IEksenApiKeyBuilder` you chain the layers onto:

```csharp
services.AddEksen(eksen => eksen
    .AddApiKeys(apiKeys => apiKeys
        // 1. authenticator that resolves a raw key to a user — Identity-backed:
        .AddIdentitySupport<AppUser, AppTenant>()
        // 2. EF Core store for EksenUserApiKey<AppUser, AppTenant>:
        .UseEntityFrameworkCore<AppUser, AppTenant, StoreDbContext>()
        // 3. the ASP.NET Core authentication scheme:
        .AddAspNetCoreSupport(new EksenApiKeyAspNetCoreOptions<EksenUserApiKey<AppUser, AppTenant>, EksenUserApiKeyId>
        {
            Scheme = "ApiKey",
            AuthenticationMethod = EksenApiKeyAuthenticationMethods.CustomHeader // X-API-KEY
        })
        // 4. publish it in the OpenAPI document:
        .AddOpenApiSecurityScheme(new EksenApiKeyAspNetCoreOptions<EksenUserApiKey<AppUser, AppTenant>, EksenUserApiKeyId>
        {
            Scheme = "ApiKey",
            AuthenticationMethod = EksenApiKeyAuthenticationMethods.CustomHeader
        })));
```

Each call returns the `IEksenApiKeyBuilder`, so order is free; register only the layers you need. Use the **same** `Scheme` string and `AuthenticationMethod` for `AddAspNetCoreSupport` and `AddOpenApiSecurityScheme` so the documented scheme matches the one that runs.

### Where the key is read from

`EksenApiKeyAuthenticationMethod` selects the request location. Two built-ins, both customisable:

```csharp
EksenApiKeyAuthenticationMethods.CustomHeader                    // "X-API-KEY"
EksenApiKeyAuthenticationMethods.CustomHeader.WithHeaderName("X-Order-Api-Key")
EksenApiKeyAuthenticationMethods.AuthorizationHeader            // Authorization: Bearer <key>
EksenApiKeyAuthenticationMethods.AuthorizationHeader.WithScheme("ApiKey") // Authorization: ApiKey <key>
```

`CustomHeader` reads the first value of the named header; `AuthorizationHeader` strips the scheme prefix (case-insensitive) off `Authorization`. If the header is absent the handler returns `NoResult()` (the request stays anonymous) — it does not fail the pipeline, so you can combine API-key auth with other schemes.

## Authenticating a request

The handler delegates the actual lookup to `IApiKeyAuthenticator<TApiKey, TId>`:

```csharp
public interface IApiKeyAuthenticator<TApiKey, TId>
    where TApiKey : class, IEksenApiKey<TId>
    where TId : IEntityId<TId, System.Ulid>
{
    Task<ApiKeyAuthenticationResult> AuthenticateAsync(string apiKeyValue, CancellationToken ct = default);
}
```

`ApiKeyAuthenticationResult` is a closed success/failure pair built via `ApiKeyAuthenticationResult.Success(principal)` / `ApiKeyAuthenticationResult.Fail(reason)`, exposing `IsAuthenticated`, `Principal`, and `FailureReason`.

`AddIdentitySupport<TUser, TTenant>()` registers `DefaultUserApiKeyAuthenticator<TUser, TTenant>`, which: parses the raw value via `ApiKeyValue.TryCreate`, looks it up through `IEksenUserApiKeyRepository`, then rejects revoked, expired, or inactive-user keys before building a `ClaimsPrincipal` carrying `NameIdentifier`, `Email`, and — when the key has a tenant — `EksenClaims.TenantId` / `EksenClaims.TenantName`. Customise the claims by deriving and overriding the `virtual BuildClaimsPrincipal`:

```csharp
public sealed class FulfilmentApiKeyAuthenticator(
    IEksenUserApiKeyRepository<AppUser, AppTenant> apiKeysRepository
) : DefaultUserApiKeyAuthenticator<AppUser, AppTenant>(apiKeysRepository)
{
    protected override ClaimsPrincipal BuildClaimsPrincipal(EksenUserApiKey<AppUser, AppTenant> apiKey)
    {
        var principal = base.BuildClaimsPrincipal(apiKey);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("api_key_name", apiKey.Name.Value));
        return principal;
    }
}
```

Register your override before `AddIdentitySupport` (which uses `TryAddScoped`), or in place of it. Because the result is a standard `ClaimsPrincipal`, the resolved caller flows into the permissions and identity skills unchanged — protect endpoints with `[Authorize(AuthenticationSchemes = "ApiKey")]` and `[RequirePermission(...)]`:

```csharp
app.MapGet("/orders/{orderNumber}/shipment", async (OrderNumber orderNumber, IShipmentService shipments) =>
        Results.Ok(await shipments.GetByOrderNumberAsync(orderNumber)))
   .RequireAuthorization(policy => policy.AddAuthenticationSchemes("ApiKey").RequireAuthenticatedUser());
```

## Persistence (EF Core)

`UseEntityFrameworkCore<TUser, TTenant, TDbContext>()` registers `IEksenUserApiKeyRepository<TUser, TTenant>` over an `EksenDbContext` (see the entity-framework-core skill). It extends `IIdRepository<...>` (the repositories skill) with two lookups:

```csharp
Task<EksenUserApiKey<TUser, TTenant>?> FindByKeyValueAsync(ApiKeyValue keyValue, CancellationToken ct = default);

Task<ICollection<EksenUserApiKey<TUser, TTenant>>> GetByUserIdAsync(EksenUserId userId, CancellationToken ct = default);
```

`FindByKeyValueAsync` eager-loads `User` and `Tenant` so the authenticator can build claims in one round-trip; use `GetByUserIdAsync` to list the keys an integration account holds.

Map the entity into your context's `OnModelCreating` — `ApplyEksenApiKeyConfigurations` applies the built-in configuration (table `UserApiKeys`, value-object conversions sized from `MaxLength`, a unique index on `KeyValue`, and unique `(UserId, Name[, TenantId])` indexes):

```csharp
protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);
    modelBuilder.ApplyEksenApiKeyConfigurations<AppUser, AppTenant>();
}
```

To extend the mapping (extra columns, an owned type), apply `EksenUserApiKeyEntityTypeConfiguration<TUser, TTenant>` or call `builder.ConfigureEksenUserApiKey()` from your own `IEntityTypeConfiguration` and add to it.

## OpenAPI

`AddOpenApiSecurityScheme<TApiKey, TId>(options)` registers an `IOpenApiDocumentTransformer` that adds a security scheme named after `options.Scheme` and attaches it as a requirement to every operation. The shape follows the `AuthenticationMethod`:

- `CustomHeaderAuthenticationMethod` → `type: apiKey`, `in: header`, `name:` the header name.
- `AuthorizationHeaderAuthenticationMethod` → `type: http`, `scheme:` the scheme, `bearerFormat: API Key`.

```yaml
components:
  securitySchemes:
    ApiKey:
      type: apiKey
      in: header
      name: X-API-KEY
```

This makes the key a first-class auth input in the generated document and any UI over it (pairs with the open-api and scalar skills).

## Errors

`ApiKeyErrors` are `ErrorDescriptor`s in the `AppModules.AuthenticationApiKeys` category that raise an `EksenException` (see the error-handling skill):

| Descriptor | `ErrorType` |
|---|---|
| `EmptyApiKeyName`, `ApiKeyNameOverflow`, `EmptyApiKeyValue`, `ApiKeyValueOverflow`, `ApiKeyAlreadyRevoked` | `Validation` |
| `ApiKeyRevoked`, `ApiKeyExpired` | `Authorization` |
| `ApiKeyNotFound` | `NotFound` |

Value-object construction and the `Revoke`/`Regenerate` invariants raise these; with `Eksen.ErrorHandling.AspNetCore` registered, the `EksenExceptionHandler` maps each `ErrorType` to its HTTP status. Note the *authentication* path is different: a missing/invalid key surfaces as a standard `AuthenticateResult.Fail` (→ 401 challenge) with `ApiKeyAuthenticationResult.FailureReason`, not as an `EksenException`.

## Testing

The authenticator is plain DI — mock the repository and assert the result contract:

```csharp
[Fact]
public async Task AuthenticateAsync_Fails_For_Revoked_Key()
{
    var key = new EksenUserApiKey<AppUser, AppTenant>(
        ApiKeyName.Create("Acme fulfilment sync"),
        ApiKeyValue.Create("revoked-key"),
        new AppUser(), tenant: null, expiresAt: null);
    key.Revoke();

    var apiKeysRepository = new Mock<IEksenUserApiKeyRepository<AppUser, AppTenant>>();
    apiKeysRepository.Setup(x => x.FindByKeyValueAsync(It.IsAny<ApiKeyValue>(), It.IsAny<CancellationToken>()))
        .ReturnsAsync(key);

    var result = await new DefaultUserApiKeyAuthenticator<AppUser, AppTenant>(apiKeysRepository.Object)
        .AuthenticateAsync("revoked-key");

    result.IsAuthenticated.ShouldBeFalse();
    result.FailureReason.ShouldContain("revoked");
}

[Fact]
public void Revoke_Twice_Throws()
{
    var key = NewKey();
    key.Revoke();
    Should.Throw<EksenException>(() => key.Revoke())
        .Descriptor.ShouldBe(ApiKeyErrors.ApiKeyAlreadyRevoked);
}
```

Assert the entity invariants (`Revoke`/`Regenerate` throw the right descriptor), the authenticator's accept/reject branches (valid, not-found, revoked, expired, inactive user, tenant claims), and that `IsActive`/`IsExpired` track `ExpiresAt`/`RevokedAt`. Use the fixtures from the test-base skill for an EF-backed repository test.

## Checklist

- [ ] Model the key as `EksenUserApiKey<TUser, TTenant>`; generate the value via `IApiKeyGenerator`, never hand-roll the secret.
- [ ] Drive lifecycle through `Revoke()` / `Regenerate(...)` / `SetExpiresAt(...)` — let the invariants throw `ApiKeyErrors`, don't bypass them.
- [ ] Register via `AddEksen(...).AddApiKeys(apiKeys => apiKeys.AddIdentitySupport<...>().UseEntityFrameworkCore<...>().AddAspNetCoreSupport(options).AddOpenApiSecurityScheme(options))`.
- [ ] Use one `Scheme` + `AuthenticationMethod` for both the runtime scheme and the OpenAPI scheme; pick `CustomHeader` (`X-API-KEY`) or `AuthorizationHeader` (`Bearer`) and customise with `WithHeaderName`/`WithScheme`.
- [ ] Map the store with `modelBuilder.ApplyEksenApiKeyConfigurations<TUser, TTenant>()`; rely on the unique `KeyValue` index for lookups.
- [ ] Protect endpoints with the scheme name and let the resolved `ClaimsPrincipal` flow into the permissions and identity skills.
- [ ] Override `BuildClaimsPrincipal` (not the handler) when you need extra claims.
