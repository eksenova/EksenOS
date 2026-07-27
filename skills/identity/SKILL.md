---
name: identity
description: The EksenOS identity model with Eksen.Identity — multi-tenant User/Role/Tenant entities, a claims-based IAuthContext current-user/tenant accessor, ASP.NET Core Identity integration (managers, stores, sign-in, claims factory) and EF Core repositories + type configurations. Use when you need to know who is calling, which tenant they belong to, or to persist users, roles and tenants.
---

# Identity (Eksen.Identity)

EksenOS identity is split into three packages: **Eksen.Identity** defines the tenant-aware domain contracts (`IEksenUser`, `IEksenRole`, `IEksenTenant`, the strongly-typed ids, the `RoleName`/`TenantName` value objects, and `IAuthContext` — the ambient "who am I" accessor). **Eksen.Identity.AspNetCore** plugs those contracts into ASP.NET Core Identity (`UserManager`, `RoleManager`, `SignInManager`, stores, claims-principal factory) and supplies the claims-backed `IAuthContext`. **Eksen.Identity.EntityFrameworkCore** provides EF Core repository implementations and `ModelBuilder` type configurations. You bring your own concrete entity classes; the framework owns the keys, identity, and multi-tenancy story.

All examples use the marketplace's e-commerce running example. The system is a multi-tenant order-management SaaS: each **`Store`** is a tenant, **`StaffUser`** records are the back-office people who manage `Order`s, `Shipment`s and `Payment`s, and `StaffRole` grants them duties.

## Defining your entities

Implement the three open generic contracts. Identity is parameterised by your tenant type so a user/role always knows its tenant (or knows it is a host-level, tenant-less record via `IMayHaveTenant`):

```csharp
using Eksen.Identity.Tenants;
using Eksen.Identity.Users;
using Eksen.Identity.Roles;
using Eksen.ValueObjects.Emailing;
using Eksen.ValueObjects.Hashing;

public sealed class Store : IEksenTenant
{
    public EksenTenantId Id { get; init; } = new(Ulid.NewUlid());

    public TenantName Name { get; private set; } = TenantName.Create("Unnamed store");

    public bool IsActive { get; private set; } = true;
}

public sealed class StaffRole : IEksenRole<Store>
{
    public EksenRoleId Id { get; init; } = new(Ulid.NewUlid());

    public RoleName Name { get; private set; } = RoleName.Create("OrderManager");

    public Store? Tenant { get; init; }

    public void SetName(RoleName roleName)
    {
        Name = roleName;
    }
}

public sealed class StaffUser : IEksenUser<Store>
{
    public EksenUserId Id { get; init; } = new(Ulid.NewUlid());

    public EmailAddress? EmailAddress { get; private set; }

    public PasswordHash? PasswordHash { get; private set; }

    public bool IsActive { get; private set; } = true;

    public bool IsPasswordChangeRequired { get; private set; }

    public Store? Tenant { get; init; }

    public void SetPasswordHash(PasswordHash? passwordHash)
    {
        PasswordHash = passwordHash;
    }

    public void SetActive(bool isActive)
    {
        IsActive = isActive;
    }
}
```

`EksenUserId`, `EksenRoleId` and `EksenTenantId` are ULID-based strongly-typed ids — see the **ulid** skill. `RoleName` and `TenantName` are `ValueObject<T, string>` types (`MaxLength == 50`, created via `Create`/`Parse`, validated and trimmed) — see the **value-objects** skill. `EmailAddress` and `PasswordHash` come from `Eksen.ValueObjects`. A `null` `Tenant` means a host-level record (a platform operator across all `Store`s); a non-null `Tenant` scopes the record to one store. The entities satisfy `IEntity<TId, Ulid>` from the **entities** skill.

### Tenant-aware domain types

Two interfaces let your own aggregates carry tenant/creator context the same way:

```csharp
using Eksen.Identity;

// An Order that must belong to a Store, and tracks the StaffUser who placed it.
public sealed class Order : IHasTenant<Store>, IHasCreator<StaffUser, Store>
{
    public Store Tenant { get; init; } = null!;     // IHasTenant: always present

    public StaffUser? Creator { get; init; }         // IHasCreator: nullable

    // ... OrderNumber, OrderStatus, items ...
}
```

`IHasTenant<TTenant>` requires a tenant; `IMayHaveTenant<TTenant>` (implemented by every user/role) allows a host-level null. Pair `IHasCreator` with the **auditing** skill for automatic creator/created-time stamping.

## Registration

Wire identity through the `AddEksen` / `IEksenBuilder` root (see the **core** skill). `AddIdentity` registers the value-object binders for `RoleName`/`TenantName`; the nested builder registers your repositories and the ASP.NET Core integration:

```csharp
services.AddEksen(eksen => eksen
    .AddIdentity(identity => identity
        .AddTenantRepository<EfCoreEksenTenantRepository<OrderingDbContext, Store>, Store>()
        .AddUserRepository<EfCoreEksenUserRepository<OrderingDbContext, StaffUser, Store>, StaffUser, Store>()
        .AddRoleRepository<EfCoreEksenRoleRepository<OrderingDbContext, StaffRole, Store>, StaffRole, Store>()
        .AddAspNetCoreSupport<StaffUser, StaffRole, Store>()));
```

`AddAspNetCoreSupport<TUser, TRole, TTenant>()` registers `IAuthContext`, the Eksen managers/stores/sign-in/claims-factory, and calls `AddIdentityCore<TUser>().AddRoles<TRole>()` with `RequireUniqueEmail = true`. It maps the standard ASP.NET Core `UserManager<TUser>`, `RoleManager<TRole>`, `SignInManager<TUser>`, `IUserStore<TUser>` and `IRoleStore<TRole>` onto the Eksen implementations, so you inject the familiar types and get tenant-aware behaviour for free.

## The current user: IAuthContext

`IAuthContext` is the ambient accessor for the authenticated principal. Inject it anywhere you need to know who/which tenant is calling — the ASP.NET Core implementation reads it from the current `HttpContext`'s claims:

```csharp
public sealed class OrderService(
    IAuthContext authContext,
    IOrderRepository ordersRepository
)
{
    public async Task PackAsync(OrderId orderId, CancellationToken ct)
    {
        if (!authContext.IsAuthenticated)
        {
            throw CommonErrors.Unauthorized.Raise();
        }

        // Tenant staff may only pack orders inside their own Store.
        if (authContext.IsTenant)
        {
            EksenTenantId storeId = authContext.Tenant!.TenantId;
            // ... scope the query to storeId ...
        }

        EksenUserId? packedBy = authContext.User?.UserId;
        // ... transition OrderStatus to Packed, stamp packedBy ...
    }
}
```

Members on `IAuthContext`:

| Member | Meaning |
|---|---|
| `IsAuthenticated` | A user principal resolved from claims. |
| `User` | `IAuthContextUser?` — `UserId` (`EksenUserId?`) and `EmailAddress`. |
| `Tenant` | `IAuthContextTenant?` — `TenantId` and `TenantName`; null for host users. |
| `OriginalTenant` | The pre-impersonation tenant, when impersonating. |
| `IsImpersonating` | True when the `eks_is_impersonating` claim is set. |
| `IsTenant` / `IsHost` | Convenience over `UserType`. |
| `UserType` | `UserType.Tenant`, `UserType.Host`, or `UserType.Anonymous`. |

A user is `Tenant` when authenticated **and** a tenant resolves from claims, `Host` when authenticated without a tenant, and `Anonymous` otherwise.

## Claims

The tenant/impersonation context travels in claims. The constants live in `EksenClaims`:

```csharp
using Eksen.Identity.Claims;

EksenClaims.TenantId;            // "eks_tenant_id"
EksenClaims.TenantName;          // "eks_tenant_name"
EksenClaims.OriginalTenantId;    // "eks_original_tenant_id"
EksenClaims.OriginalTenantName;  // "eks_original_tenant_name"
EksenClaims.IsImpersonating;     // "eks_is_impersonating"
```

`EksenUserClaimsPrincipalFactory<TUser, TRole, TTenant>` writes the tenant claims onto the principal at sign-in (when `user.Tenant != null`). `ClaimExtensions` helps you read/write claims idempotently:

```csharp
using Eksen.Identity.Claims;

string? tenantId = principal.GetClaim(EksenClaims.TenantId);   // last value, or null
identity.AddIfNotExists(new Claim(EksenClaims.TenantId, store.Id.Value.ToString()));
identity.AddOrReplace(new Claim(EksenClaims.TenantName, store.Name.Value));
```

`EksenUserSignInManager<TUser, TTenant>` invalidates the user's permission cache on sign-in — pair identity with the **permissions** skill for `[RequirePermission]` authorization.

## Repositories

The repository contracts extend `IIdRepository<...>` from the **repositories** skill, so you get the standard query/insert/update/delete surface plus identity-specific finders and filters:

```csharp
public sealed class OrderStaffLookup(IEksenUserRepository<StaffUser, Store> usersRepository)
{
    public Task<StaffUser?> FindPackerAsync(EmailAddress email, CancellationToken ct)
    {
        return usersRepository.FindByEmailAddressAsync(
            email,
            includeOptions: new EksenUserIncludeOptions<StaffUser, Store> { IncludeTenant = true },
            cancellationToken: ct);
    }

    public Task<ICollection<StaffUser>> ActiveStaffForStoreAsync(EksenTenantId storeId, CancellationToken ct)
    {
        return usersRepository.GetListAsync(
            new EksenUserFilterParameters<StaffUser, Store> { IsActive = true, TenantId = storeId },
            cancellationToken: ct);
    }
}
```

- `IEksenUserRepository<TUser, TTenant>` adds `FindByEmailAddressAsync(...)` and `FindByIdAsync(...)`. Filter on `SearchFilter` (email contains), `IsActive`, `TenantId`; include `IncludeTenant`.
- `IEksenRoleRepository<TRole, TTenant>` filters on `SearchFilter`, `Name` (`RoleName`), `TenantId`; include `IncludeTenant`.
- `IEksenTenantRepository<TTenant>` filters on `SearchFilter`.

## Persistence (EF Core)

The EF Core package ships `EfCoreEksenUserRepository`, `EfCoreEksenRoleRepository` and `EfCoreEksenTenantRepository` (each takes your `EksenDbContext` — see the **entity-framework-core** skill) and three `ModelBuilder` extensions that configure keys, conversions and the tenant-scoped unique indexes:

```csharp
using Eksen.Identity.EntityFrameworkCore.Tenants;
using Eksen.Identity.EntityFrameworkCore.Roles;
using Eksen.Identity.EntityFrameworkCore.Users;

public sealed class OrderingDbContext(
    DbContextOptions<OrderingDbContext> options
) : EksenDbContext(options)
{
    public DbSet<Store> Stores
    {
        get { return Set<Store>(); }
    }

    public DbSet<StaffRole> StaffRoles
    {
        get { return Set<StaffRole>(); }
    }

    public DbSet<StaffUser> StaffUsers
    {
        get { return Set<StaffUser>(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Store>(e => e.ConfigureEksenTenant());
        modelBuilder.Entity<StaffRole>(e => e.ConfigureEksenRole<StaffRole, Store>());
        modelBuilder.Entity<StaffUser>(e => e.ConfigureEksenUser<StaffUser, Store>());
    }
}
```

`ConfigureEksenUser` maps the id/email/password-hash conversions, the `Tenant` relationship (FK `TenantId`, `DeleteBehavior.Restrict`), and two filtered unique indexes — email is unique **per tenant** for tenant users and globally unique for host users (null `TenantId`). `ConfigureEksenRole` does the same for `Name`, `ConfigureEksenTenant` makes `Name` globally unique.

## Errors

Identity raises typed `EksenException`s via `ErrorDescriptor`s (see the **error-handling** skill). `RoleName`/`TenantName` validation surfaces `RoleErrors.RoleNameEmpty` / `RoleErrors.RoleNameOverflow` and `TenantErrors.EmptyTenantName` / `TenantErrors.TenantNameOverflow`. Application code raises the dedicated descriptors:

```csharp
if (await usersRepository.FindByEmailAddressAsync(email, cancellationToken: ct) is not null)
{
    throw UserErrors.EmailAddressAlreadyExists.Raise(email);
}

throw RoleErrors.RoleNameAlreadyExists.Raise(roleName);   // duplicate role name
throw RoleErrors.CannotDeleteWithUsers.Raise();           // role still assigned
```

`UserErrors` also exposes `EmptyPassword`, `ShortPassword`, `WeakPassword` (all `ErrorType.Validation`). With `Eksen.ErrorHandling.AspNetCore` registered these map to HTTP 400.

## Testing

Implement the contracts with plain mutable test doubles (settable `Id`, `Tenant`, etc.), build an in-memory `EksenDbContext`, and exercise the real repository:

```csharp
var connection = new SqliteConnection("DataSource=:memory:");
connection.Open();
var options = new DbContextOptionsBuilder<OrderingDbContext>().UseSqlite(connection).Options;

await using var db = new OrderingDbContext(options);
await db.Database.EnsureCreatedAsync();
var usersRepository = new EfCoreEksenUserRepository<OrderingDbContext, StaffUser, Store>(db);

var found = await usersRepository.FindByEmailAddressAsync(EmailAddress.Parse("packer@store.test"));
found.ShouldBeNull();
```

For `IAuthContext`, build a `ClaimsPrincipal` with the relevant `EksenClaims` and the standard `IdentityOptions.ClaimsIdentity` claim types, set it on a `DefaultHttpContext`, and assert `UserType`/`Tenant`/`IsImpersonating`. The **test-base** skill provides `EksenUnitTestBase`.

## Checklist

- [ ] Implement `IEksenTenant`, `IEksenUser<TTenant>`, `IEksenRole<TTenant>` with ULID ids; a null `Tenant` means a host-level record.
- [ ] Register through `AddEksen(...).AddIdentity(...)` with `AddTenantRepository`/`AddUserRepository`/`AddRoleRepository` and `AddAspNetCoreSupport<TUser, TRole, TTenant>()`.
- [ ] Read the caller via injected `IAuthContext`; branch on `UserType` / `IsTenant` / `IsHost` and scope queries by `Tenant.TenantId`.
- [ ] Use `EksenClaims` constants and `ClaimExtensions` to read/write tenant + impersonation claims — never hard-code the strings.
- [ ] Query users/roles/tenants through `IEksenUserRepository`/`IEksenRoleRepository`/`IEksenTenantRepository` filter and include options.
- [ ] Configure persistence with `ConfigureEksenTenant()` / `ConfigureEksenRole<,>()` / `ConfigureEksenUser<,>()` on an `EksenDbContext`.
- [ ] Raise `UserErrors` / `RoleErrors` / `TenantErrors` descriptors for domain failures; let the error-handling skill map them to HTTP.
