---
name: auditing
description: The EksenOS way to capture an audit trail with Eksen.Auditing — open a per-request audit scope, record intercepted service-method calls and entity changes against the current user/tenant, persist the trail with EF Core, and query it back through IAuditLogManager. Use when you need "who did what, when" — request logs, method-call traces, or entity change history — instead of hand-rolling logging tables.
---

# Auditing (Eksen.Auditing)

An **audit log** is a durable record of activity: each unit of work (typically one HTTP request) becomes an `AuditLog` stamped with the current user, tenant, timing, source IP, correlation id, and — when enabled — the request payload. Within that log, intercepted service-method calls become `AuditLogAction`s and entity mutations become `AuditLogEntityChange`s (with per-property before/after values). You open a scope, the framework fills it in as work happens, and you save it at the end. Prefer this over scattering `ILogger` calls or building your own history tables whenever the trail is part of the domain record (compliance, "who changed this order", request forensics).

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Payment`, `Shipment`).

## Registration

Audit infrastructure plugs into the `IEksenBuilder` root via `AddAuditing`. The configure delegate hands you an `IEksenAuditingBuilder` to set options, opt into method interception, wire ASP.NET Core, and choose a persistence store:

```csharp
services.AddEksen(eksen => eksen
    .AddAuditing(auditing => auditing
        .Configure(options =>
        {
            options.IsEnabled = true;
            options.LogHttpRequestPayload = true;          // capture request bodies
            options.Add<OrderAppService>();                // intercept this service
            options.Add<PaymentAppService>();
        })
        .UseAutofacProxies()                                // enable method interception
        .AddAspNetCoreIntegration()                         // per-request audit scope
        .UseEntityFrameworkCore<OrderingDbContext>()));     // persist the trail
```

`AddAuditing` registers `IAuditLogManager` and the `AuditingInterceptor` as scoped services and returns the root `IEksenBuilder`, so it composes with the other `Add*` modules. Builds on the **core** skill's `AddEksen` / `IEksenBuilder` root.

## Options

`EksenAuditingOptions` is the single options record:

| Member | Behaviour |
|---|---|
| `IsEnabled` | Master switch (default `true`). When `false`, the middleware skips scope creation and `SaveAsync` is a no-op. |
| `LogHttpRequestPayload` | When `true`, the middleware buffers and stores the request body as an `AuditLogHttpRequestPayload`. |
| `LogMethodReturnValues` | Opt-in flag for recording intercepted method return values. |
| `Add<T>()` / `Add(Type)` | Register one service type for method interception. |
| `AddAssembly(Assembly)` | Register every eligible class/interface in an assembly (skips abstract types and anything marked `[ExcludeFromAuditLogs]`). |
| `AuditedTypes` | The resolved read-only set of intercepted types. |

```csharp
options.AddAssembly(typeof(OrderAppService).Assembly);   // audit the whole application layer
```

## Method interception

`UseAutofacProxies()` wraps every registered audited type with the `AuditingInterceptor`. When an audit scope is active, each call to an audited service records an `AuditLogAction` capturing the declaring service type, method name, JSON-serialized parameters, elapsed `Duration`, and any thrown exception message — then the call proceeds normally. With no active scope, the interceptor is transparent. `CancellationToken` parameters are never serialized.

Exclude a whole service with `[ExcludeFromAuditLogs]` on the class (the attribute targets `Class` or `Property`). `AddAssembly` skips classes that carry it, and the interceptor short-circuits any call whose declaring type carries it:

```csharp
public sealed class OrderAppService
{
    public async Task<OrderId> PlaceOrderAsync(PlaceOrderRequest request, CancellationToken ct)
    {
        // recorded as an AuditLogAction: ServiceType, MethodName="PlaceOrderAsync",
        // Parameters={ "request": { ... } }, Duration, ExceptionMessage (if thrown)
    }
}

[ExcludeFromAuditLogs]
public sealed class OrderQueryService   // read path — never intercepted, even if registered
{
    public Task<OrderSummary> GetOrderSummaryAsync(OrderId orderId, CancellationToken ct)
    {
        // not worth auditing
    }
}
```

## ASP.NET Core integration

`AddAspNetCoreIntegration()` (registration side) pairs with `UseEksenAuditing()` in the pipeline. The `AuditingMiddleware` opens a scope per request, populates the `AuditLogHttpRequest` (method, host, path, query string, scheme, protocol, user agent, content type), stamps source IP/port and the correlation id from `HttpContext.TraceIdentifier`, times the request, records the response status code, captures any unhandled exception message, and saves the log in a `finally` — then re-throws so your error pipeline still runs.

```csharp
var app = builder.Build();

app.UseRouting();
app.UseAuthentication();
app.UseEksenAuditing();   // after auth so the current user/tenant is on the audit log
app.UseAuthorization();
app.MapControllers();
```

Place it after authentication so `IAuthContext` (from the **identity** skill) has resolved the user and tenant — `BeginScope` stamps `UserId`/`TenantId` from it. Enabling `LogHttpRequestPayload` makes the middleware `EnableBuffering()` and rewind the body so downstream model binding still reads it.

## Persistence (EF Core)

`UseEntityFrameworkCore<TDbContext>()` registers the EF Core-backed `IAuditLogRepository`, `IAuditLogActionRepository`, `IAuditLogEntityChangeRepository`, and `IAuditLogPropertyChangeRepository` against your context. The context must derive from `EksenDbContext` (the **entity-framework-core** skill), and its model must apply the auditing configuration:

```csharp
public sealed class OrderingDbContext(
    DbContextOptions<OrderingDbContext> options
) : EksenDbContext(options)
{
    public DbSet<Order> Orders
    {
        get { return Set<Order>(); }
    }

    public DbSet<Payment> Payments
    {
        get { return Set<Payment>(); }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyEksenAuditingConfiguration();   // AuditLogs + related tables
    }
}
```

`ApplyEksenAuditingConfiguration()` maps the full graph: `AuditLogs`, HTTP request and payload, actions, entity changes, and property changes. The identifiers are ULID-based (`AuditLogId`, `AuditLogActionId`, …) per the **ulid** skill, and the filter/include/pagination parameters come from the **repositories** skill. Saving the audit log persists its actions and entity changes in one go (the repository `InsertAsync(..., autoSave: true, ...)`); align that with your request boundary from the **unit-of-work** skill.

## Querying the trail

Inject `IAuditLogManager` to read back the trail. `AuditLogFilterParameters` narrows by user, tenant, time window, correlation id, or free-text search:

```csharp
public sealed class AuditQueryService(IAuditLogManager auditLog)
{
    // Every change ever recorded against a specific Order
    public Task<ICollection<AuditLogEntityChange>> OrderHistoryAsync(OrderId orderId, CancellationToken ct)
    {
        return auditLog.GetEntityChangesAsync<Order>(orderId.ToString(), ct);
    }

    // Recent activity for one customer, newest first
    public Task<ICollection<AuditLog>> CustomerActivityAsync(EksenUserId userId, CancellationToken ct)
    {
        return auditLog.GetAuditLogsAsync(
            new AuditLogFilterParameters
            {
                UserId = userId,
                FromTime = DateTime.UtcNow.AddDays(-30)
            },
            ct);
    }

    // Drill into one request: its intercepted method calls
    public Task<ICollection<AuditLogAction>> ActionsAsync(AuditLogId auditLogId, CancellationToken ct)
    {
        return auditLog.GetActionsForAuditLogAsync(auditLogId, ct);
    }

    // Before/after property values for a single entity change
    public Task<ICollection<AuditLogPropertyChange>> PropertyDiffAsync(
        AuditLogEntityChangeId changeId, CancellationToken ct)
    {
        return auditLog.GetPropertyChangesForEntityChangeAsync(changeId, ct);
    }
}
```

`GetEntityChangesAsync<TEntity>` matches on the entity's full type name, so `GetEntityChangesAsync<Payment>(paymentId.ToString())` returns the lifecycle of one payment. `EntityChangeType` is `Created` / `Updated` / `Deleted`.

## Building a scope manually

Outside an HTTP request (a background worker, a console seeder) drive the scope yourself through `IAuditLogManager`. `BeginScope` stamps the current user/tenant; the scope is `IDisposable` and flushes its metadata on dispose; `SaveAsync` persists it:

```csharp
public async Task NightlyReconcileAsync(CancellationToken ct)
{
    using (var scope = auditLog.BeginScope())
    {
        scope.SetMetadata("job", "nightly-reconcile");
        scope.AddEntityChange(new AuditLogEntityChange(
            scope.AuditLog.Id,
            EntityChangeType.Updated,
            typeof(Shipment).FullName!,
            shipmentId.ToString()));
        // intercepted service calls inside this using-block attach to scope.AuditLog
    }

    await auditLog.SaveAsync(ct);
}
```

`CurrentScope` is `null` until `BeginScope`, and is cleared back to `null` only after a write actually happens in `SaveAsync`. While `IsEnabled` is `false`, `SaveAsync` returns without writing and leaves the scope in place as `CurrentScope`.

## Errors

The middleware records an unhandled exception's message onto the `AuditLog` (and onto the in-flight `AuditLogAction` for intercepted calls), saves the trail, then re-throws — so auditing never swallows a failure and your `EksenExceptionHandler` from the **error-handling** skill still maps the domain error to its HTTP response. Treat the audit write as best-effort telemetry, not as part of the business transaction's success criteria.

## Testing

`IAuditLogManager` and the repository interfaces are plain abstractions — mock them for unit tests, or run against a real `EksenDbContext` (Sqlite in-memory) after `ApplyEksenAuditingConfiguration()` for integration coverage:

```csharp
[Fact]
public async Task PlaceOrder_Is_Recorded_As_An_Action()
{
    using var scope = manager.BeginScope();
    await orderService.PlaceOrderAsync(request, default);   // intercepted

    scope.AuditLog.Actions
        .ShouldContain(a => a.MethodName == nameof(OrderAppService.PlaceOrderAsync));
}

[Fact]
public async Task Save_Is_Skipped_When_Disabled()
{
    options.Value.IsEnabled = false;
    manager.BeginScope();
    await manager.SaveAsync();

    manager.CurrentScope.ShouldNotBeNull();   // no-op: nothing persisted, scope retained
}
```

Assert that auditable methods produce actions, that calls into `[ExcludeFromAuditLogs]` services do not, and that disabling auditing skips the write. The **test-base** skill's `EksenUnitTestBase` is the base class used across these tests.

## Checklist

- [ ] Register with `AddEksen(...).AddAuditing(...)`; set `IsEnabled` and `LogHttpRequestPayload` via `Configure`.
- [ ] Register audited services with `options.Add<T>()` / `AddAssembly(...)` and call `UseAutofacProxies()` to turn on interception.
- [ ] Add `AddAspNetCoreIntegration()` and place `app.UseEksenAuditing()` after authentication, before authorization.
- [ ] Persist with `UseEntityFrameworkCore<TDbContext>()` and call `modelBuilder.ApplyEksenAuditingConfiguration()` in an `EksenDbContext`.
- [ ] Exclude whole read-only or noisy services with `[ExcludeFromAuditLogs]` on the class.
- [ ] Query history through `IAuditLogManager` with `AuditLogFilterParameters`; use `GetEntityChangesAsync<TEntity>` for per-entity trails.
- [ ] For non-HTTP work, drive `BeginScope()` / `SaveAsync()` yourself and dispose the scope.
- [ ] Let exceptions propagate — auditing records and re-throws, so the error-handling pipeline still runs.
