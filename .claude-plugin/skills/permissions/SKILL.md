---
name: permissions
description: The EksenOS way to authorize operations with Eksen.Permissions — declare named permissions, register them through AddPermissions, check them with IPermissionChecker, gate endpoints with ASP.NET Core authorization policies, nullify forbidden response fields with [BindPermission], persist grants with EF Core, and cache the lookups. Use when you would otherwise hand-roll role checks, scatter string-typed claim names, or write a custom authorization handler for a "can this user do X" decision.
---

# Permissions (Eksen.Permissions)

A **permission** is a named, defined capability ("Orders.Create", "Shipments.Dispatch") that a user holds either directly or through a role. Eksen.Permissions models each permission as a `PermissionName` value object, requires every checkable permission to be **registered as a definition** up front, and resolves a user's effective grants — union of direct user grants and role grants — through an `IPermissionChecker`. Prefer it over ad-hoc role string comparisons whenever an operation needs a "may this principal do this?" gate, because the set of permissions is closed, the names are validated, and the grants are persisted and cached.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Shipment`, `Payment`).

## Naming a permission

`PermissionName` is a `ValueObject<PermissionName, string>` (see the value-objects skill). Create one with `Create`, or rely on the implicit string conversion. It trims input and rejects blank or over-`MaxLength` (50) names:

```csharp
using Eksen.Permissions;

PermissionName create = PermissionName.Create("Orders.Create");
PermissionName ship   = "Shipments.Dispatch";   // implicit from string
```

A blank name raises `PermissionErrors.EmptyPermissionName`; an over-length one raises `PermissionErrors.PermissionNameOverflow` — both `Validation` errors (see the error-handling skill). Keep the string stable: it is the contract that gets persisted and matched against authorization policies.

## Declaring the permission set

A `DefinedPermission` pairs a `PermissionName` with whether it is excluded from per-tenant seeding:

```csharp
public record DefinedPermission(
    PermissionName Name,
    bool IsTenantSeedDisabled
);
```

Only **defined** permissions are checkable — `IPermissionChecker` throws if you ask about a name that was never registered, so collect them in one place:

```csharp
public static class OrderPermissions
{
    public static readonly DefinedPermission Create   = new("Orders.Create",     IsTenantSeedDisabled: false);
    public static readonly DefinedPermission Update    = new("Orders.Update",     IsTenantSeedDisabled: false);
    public static readonly DefinedPermission Cancel    = new("Orders.Cancel",     IsTenantSeedDisabled: false);
    public static readonly DefinedPermission Dispatch  = new("Shipments.Dispatch", IsTenantSeedDisabled: false);
    public static readonly DefinedPermission Refund    = new("Payments.Refund",    IsTenantSeedDisabled: true);
}
```

## Registration

Register through the `IEksenBuilder` root with `AddPermissions<TUser, TRole, TTenant>`, supplying your identity model types (see the identity skill). The nested `IEksenPermissionBuilder` is where you `Configure` the options, choose a cache, add ASP.NET Core support, and wire EF Core:

```csharp
services.AddEksen(eksen => eksen
    .AddPermissions<AppUser, AppRole, AppTenant>(permissions => permissions
        .Configure(options =>
        {
            options.Permissions.Add(OrderPermissions.Create);
            options.Permissions.Add(OrderPermissions.Update);
            options.Permissions.Add(OrderPermissions.Cancel);
            options.Permissions.Add(OrderPermissions.Dispatch);
            options.Permissions.Add(OrderPermissions.Refund);

            // permissions a user may still exercise while a password change is pending
            options.PasswordChangeAllowedPermissions.Add("Users.ChangePassword");
        })
        .UseDistributedCache()                                       // or .UseInMemoryCache()
        .AddAspNetCoreSupport()                                      // policy provider + handler + result filter
        .UseEntityFrameworkCore<AppUser, AppRole, AppTenant, AppDbContext>()));
```

`AddPermissions` also pulls in value-object support for `PermissionName` and registers `IPermissionChecker`, `IPermissionStore`, and an `IPermissionCache` by default (`DistributedPermissionCache`).

### Options

`EksenPermissionOptions` exposes two collections:

| Member | Purpose |
|---|---|
| `Permissions` | The defined permission set. Only names listed here are checkable, and each becomes an authorization policy. |
| `PasswordChangeAllowedPermissions` | Names a user may still exercise while `IsPasswordChangeRequired` is set — everything else is denied until the password is changed. |

## Checking permissions

Inject `IPermissionChecker`. It resolves the **current** user from the ambient auth context, or takes an explicit `EksenUserId`:

```csharp
public sealed class CancelOrderHandler(
    IPermissionChecker permissionChecker,
    IRepository<Order, OrderId> ordersRepository
)
{
    public async Task CancelAsync(OrderId orderId)
    {
        if (!await permissionChecker.HasPermissionAsync("Orders.Cancel"))
        {
            throw CommonErrors.Unauthorized.Raise();   // see the error-handling skill
        }

        var order = await ordersRepository.GetAsync(orderId);
        order.Cancel();
    }
}
```

The four members:

| Member | Behaviour |
|---|---|
| `HasPermissionAsync(PermissionName)` | Current user. Also returns `false` if the user/tenant is inactive, or a password change is required and the permission is not in `PasswordChangeAllowedPermissions`. |
| `HasPermissionAsync(EksenUserId, PermissionName)` | A specific user — grant check only, without the active/password-state gating. |
| `HasPermissionsAsync(PermissionName[])` | Current user holds **all** of the listed permissions. |
| `HasPermissionsAsync(EksenUserId, PermissionName[])` | A specific user holds all of the listed permissions. |

Effective grants are the union of the user's direct `EksenUserPermission` grants and the permissions of every role the user holds (`EksenUserRole` → `EksenRolePermission`), de-duplicated and excluding disabled definitions. Asking about an **undefined** permission throws — register it in `Permissions` first.

## ASP.NET Core authorization

`AddAspNetCoreSupport()` registers a `PermissionAuthorizationPolicyProvider` that turns every defined permission name into an authorization policy on demand. Gate an endpoint by naming the permission as the policy — no custom attribute, just `[Authorize]`:

```csharp
[ApiController]
[Route("orders")]
public sealed class OrdersController : ControllerBase
{
    [HttpPost]
    [Authorize("Orders.Create")]
    public Task<OrderDto> Create(CreateOrderRequest request)
    {
        /* ... */
    }

    [HttpPost("{id}/dispatch")]
    [Authorize("Shipments.Dispatch")]
    public Task Dispatch(OrderId id)
    {
        /* ... */
    }
}
```

The policy resolves case-insensitively, builds a `PermissionAuthorizationRequirement`, and the handler succeeds only when `IPermissionChecker.HasPermissionAsync` passes. An unsatisfied requirement yields the standard **403 Forbidden**.

### Field-level redaction with [BindPermission]

`AddAspNetCoreSupport()` also installs a result filter that walks successful (`2xx`) response objects — including nested objects and collections — and **nulls out** any property annotated with `[BindPermission]` when the current user lacks that permission. The property must be nullable:

```csharp
public sealed class OrderDto
{
    public OrderNumber OrderNumber { get; set; }

    public Money Total { get; set; }

    [BindPermission("Payments.ViewDetails")]
    public PaymentDto? Payment { get; set; }   // nulled for users without the permission
}
```

A non-nullable `[BindPermission]` property throws `InvalidOperationException` when the filter cannot null it, so keep redacted fields nullable.

## Persistence (EF Core)

`UseEntityFrameworkCore<TUser, TRole, TTenant, TDbContext>()` registers EF Core implementations of the four repositories (`IEksenPermissionDefinitionRepository`, `IEksenUserPermissionRepository`, `IEksenRolePermissionRepository`, `IEksenUserRoleRepository`) over your `EksenDbContext` (see the entity-framework-core skill). Apply the entity configurations in `OnModelCreating`:

```csharp
public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options
) : EksenDbContext(options)
{
    public DbSet<PermissionDefinition> PermissionDefinitions
    {
        get { return Set<PermissionDefinition>(); }
    }

    public DbSet<EksenUserPermission<AppUser, AppTenant>> UserPermissions
    {
        get { return Set<EksenUserPermission<AppUser, AppTenant>>(); }
    }

    public DbSet<EksenRolePermission<AppRole, AppTenant>> RolePermissions
    {
        get { return Set<EksenRolePermission<AppRole, AppTenant>>(); }
    }

    public DbSet<EksenUserRole<AppUser, AppRole, AppTenant>> UserRoles
    {
        get { return Set<EksenUserRole<AppUser, AppRole, AppTenant>>(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyEksenPermissionsConfigurations<AppUser, AppRole, AppTenant>();
    }
}
```

`ApplyEksenPermissionsConfigurations` maps `PermissionDefinition` to the `PermissionDefinitions` table — `Id` as a ULID string, `Name` converted through `PermissionName.Create` and sized at `PermissionName.MaxLength` — plus the user/role/user-role join entities. To configure `PermissionDefinition` inside a hand-written `IEntityTypeConfiguration`, call `builder.ConfigurePermissionDefinition()`.

`PermissionDefinition` is an `ISoftDelete` entity (see the entities skill): `IsDisabled` excludes it from effective grants without deletion, toggled via `SetIsEnabled(bool)`; `IsDeleted` soft-deletes it. The join entities (`EksenUserPermission`, `EksenRolePermission`, `EksenUserRole`) are `IMayHaveTenant`, so grants are tenant-scoped.

## Caching

Permission lookups are read-heavy, so they go through an `IPermissionCache`. Pick the backing store on the builder:

- `UseDistributedCache()` — `DistributedPermissionCache` over `IDistributedCache` (the default; shared across instances).
- `UseInMemoryCache()` — `InMemoryPermissionCache` for single-instance scenarios.

After mutating grants or definitions, invalidate the relevant entries so the next check re-reads:

```csharp
public sealed class GrantOrderPermissionHandler(IPermissionCache cache)
{
    public async Task GrantAsync(EksenUserId userId)
    {
        // ... persist the new EksenUserPermission ...
        await cache.InvalidateForUserAsync(userId);
    }
}
```

`IPermissionCache` also offers `InvalidateForCurrentUserAsync`, `InvalidateForUserIdsAsync(...)`, and `InvalidateForPermissionDefinitionsAsync()` (call the last after changing the defined permission set).

## Errors

`PermissionName` validation raises `PermissionErrors.EmptyPermissionName` and `PermissionErrors.PermissionNameOverflow`, both `ErrorType.Validation`. With `Eksen.ErrorHandling.AspNetCore` registered, the `EksenExceptionHandler` maps these to **HTTP 400** (see the error-handling skill). A failed `[Authorize]` permission policy yields **403** through the standard ASP.NET Core authorization pipeline. When you gate inside a handler with `IPermissionChecker`, raise `CommonErrors.Unauthorized` (`ErrorType.Authorization`), which the handler maps to **HTTP 401**.

## Testing

Mock `IPermissionChecker` for handler tests, and `IPermissionCache` when exercising the checker itself:

```csharp
[Fact]
public async Task Cancel_Requires_Permission()
{
    var checker = new Mock<IPermissionChecker>();
    checker.Setup(c => c.HasPermissionAsync(It.Is<PermissionName>(n => n.Value == "Orders.Cancel")))
           .ReturnsAsync(false);

    var handler = new CancelOrderHandler(checker.Object, ordersRepository);

    await Should.ThrowAsync<EksenException>(() => handler.CancelAsync(orderId));
}

[Fact]
public async Task PolicyProvider_Builds_Policy_For_Defined_Permission()
{
    var options = new EksenPermissionOptions();
    options.Permissions.Add(new DefinedPermission("Orders.Create", IsTenantSeedDisabled: false));
    var provider = new PermissionAuthorizationPolicyProvider(
        Options.Create(new AuthorizationOptions()), Options.Create(options));

    var policy = await provider.GetPolicyAsync("orders.create");   // case-insensitive

    policy.ShouldNotBeNull();
    policy.Requirements.OfType<PermissionAuthorizationRequirement>().ShouldHaveSingleItem();
}
```

For the EF Core repositories, drive them against a real `EksenDbContext` on the Sqlite/SQL Server test database (see the test-base skill).

## Checklist

- [ ] Name permissions as stable `PermissionName` strings; keep them ≤ `PermissionName.MaxLength`.
- [ ] Define every checkable permission as a `DefinedPermission` and add it to `options.Permissions` — undefined names throw.
- [ ] Register via `AddEksen(...).AddPermissions<TUser, TRole, TTenant>(p => p.Configure(...))`.
- [ ] Gate operations with `IPermissionChecker.HasPermissionAsync`, or endpoints with `[Authorize("Permission.Name")]` after `AddAspNetCoreSupport()`.
- [ ] Mark redactable response fields nullable and annotate them with `[BindPermission("...")]`.
- [ ] Persist grants with `UseEntityFrameworkCore<...>()` + `ApplyEksenPermissionsConfigurations<...>()`.
- [ ] Choose `UseDistributedCache()` (default) or `UseInMemoryCache()`, and invalidate the cache after changing grants or definitions.
- [ ] Let name validation surface as `Validation` errors (→ 400) and failed policies as 403 at the edge.
