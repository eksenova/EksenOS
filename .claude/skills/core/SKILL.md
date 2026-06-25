---
name: core
description: The root of the EksenOS stack — Eksen.Core gives you the AddEksen / IEksenBuilder registration entry point that every other package plugs into, the AppModules registry, and the Specification primitive for reusable query/domain predicates. Use when you are bootstrapping the Eksen service graph, writing an AddX builder extension for a module, or modelling a named, composable business rule.
---

# Core (Eksen.Core)

**Eksen.Core** is the foundation every other Eksen package builds on. It owns three things: the `AddEksen` / `IEksenBuilder` registration root that modules hang their `AddX(...)` calls off, the `AppModules` / `AppModuleRegistry` bookkeeping that records which modules are wired up, and the `Specification<TObj>` primitive for naming a business rule once and reusing it as both an in-memory check and an EF Core query expression. It has almost no dependencies of its own — it is the seam, not the implementation.

All examples use the marketplace's e-commerce running example (`Order`, `Customer`, `Product`, `Shipment`, `Payment`).

## The registration root

Everything starts with `AddEksen` on `IServiceCollection`. It registers the core services (an `IRandomStringGenerator`) and invokes your callback with an `IEksenBuilder`:

```csharp
using Microsoft.Extensions.DependencyInjection;

services.AddEksen(eksen => eksen
    .AddValueObjects()
    .AddSmartEnums(smartEnums => smartEnums
        .Configure(options => options.AddAssembly(typeof(OrderStatus).Assembly))));
```

`IEksenBuilder` is deliberately tiny — it exposes the underlying `IServiceCollection` so each module can register its own services:

```csharp
public interface IEksenBuilder
{
    IServiceCollection Services { get; }
}
```

Calling `AddEksen()` with no callback still registers the core services, so it is safe (and idempotent in intent) to call once during composition-root setup.

## Writing a module builder extension

This is Eksen.Core's main job: give sibling packages a consistent place to plug in. A module exposes an `AddX(this IEksenBuilder ...)` extension that returns the builder so calls chain. Follow the same shape the framework packages use — define a per-module builder interface, expose `Services` off the root, then register against it:

```csharp
// ReSharper disable once CheckNamespace
namespace Microsoft.Extensions.DependencyInjection;

public static class DependencyInjectionExtensions
{
    public static IEksenBuilder AddOrdering(
        this IEksenBuilder builder,
        Action<IEksenOrderingBuilder>? configureAction = null)
    {
        var orderingBuilder = new EksenOrderingBuilder(builder);
        configureAction?.Invoke(orderingBuilder);
        return builder;
    }
}

public interface IEksenOrderingBuilder
{
    IEksenBuilder EksenBuilder { get; }
}

public class EksenOrderingBuilder(
    IEksenBuilder eksenBuilder
) : IEksenOrderingBuilder
{
    public IEksenBuilder EksenBuilder { get; } = eksenBuilder;
}
```

Reach the service collection through the root in your extension body:

```csharp
builder.Services.AddScoped<IOrderPricer, OrderPricer>();
```

You rarely need to write this yourself — the framework packages (`AddValueObjects`, `AddSmartEnums`, `AddUnitOfWork`, ...) already follow it. Mirror it only when you are authoring a new Eksen module.

## App modules registry

Each package registers a stable module name so the wired-up set is discoverable at runtime. `AppModules.Eksen` is the `"Eksen"` root prefix; packages add their own name as an extension member and register it from a static constructor:

```csharp
using Eksen.Core;

public static class AppModuleExtensions
{
    static AppModuleExtensions()
    {
        AppModuleRegistry.Register(AppModules.Ordering);
    }

    extension(AppModules)
    {
        public static string Ordering
        {
            get { return AppModules.Eksen + ".Ordering"; }
        }
    }
}
```

`AppModuleRegistry.RegisteredModules` is a read-only snapshot of every name that has been registered — handy for diagnostics or a health endpoint. Names are deduplicated (it is backed by a `HashSet<string>`).

## Specifications

A `Specification<TObj>` names a business rule once and lets you use it two ways: as an in-memory predicate (`IsSatisfiedBy`) and as an `Expression<Func<TObj, bool>>` that an ORM can translate to SQL. Derive from `Specification<TObj>` and implement `ToExpression()`:

```csharp
using Eksen.Core;
using System.Linq.Expressions;

public sealed class UnpaidOrderSpecification : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.Status == OrderStatus.Pending;
    }
}
```

Use it in memory against a loaded aggregate:

```csharp
var spec = new UnpaidOrderSpecification();

if (spec.IsSatisfiedBy(order))
{
    // order is still awaiting payment
}
```

Because `Specification<TObj>` defines an implicit conversion to `Expression<Func<TObj, bool>>`, you can pass it straight into LINQ — including a repository query — without calling `ToExpression()` yourself:

```csharp
// IQueryable / EF Core — the spec translates to a SQL predicate
var pending = await ordersRepository.Where(new UnpaidOrderSpecification()).ToListAsync();
```

Parameterise a spec by taking constructor arguments and closing over them in the expression:

```csharp
public sealed class OrdersForCustomerSpecification(
    CustomerId customerId
) : Specification<Order>
{
    public override Expression<Func<Order, bool>> ToExpression()
    {
        return order => order.CustomerId == customerId;
    }
}
```

Keep the expression translation-friendly (no method calls the provider can't map) when you intend to run a spec against the database — see the **entity-framework-core** and **repositories** skills for querying. The `ISpecification<TObj>` interface is available when you want to depend on the abstraction rather than the base class.

## Random strings

`AddEksen` registers `IRandomStringGenerator` as a singleton. Resolve it for non-cryptographic tokens — order references, idempotency suffixes, test fixtures — and tune the character pool via `RandomStringGenerationParameters`:

```csharp
public sealed class OrderNumberAllocator(IRandomStringGenerator random)
{
    public string NextSuffix()
    {
        return random.GenerateRandomString(new RandomStringGenerationParameters
        {
            Length = 6,
            IncludeSpecialCharacters = false,
            IncludeLowercase = false,
        });
    }
}
```

The default parameters produce an 8-character mixed string including `!@#*.+`. It throws `ArgumentException` if you disable every character class. It uses `Random.Shared`, so do not use it where a cryptographically secure value is required.

## Type helper

`Eksen.Core.Helpers.TypeHelper.GetUnderlyingType` peels nullability and collection wrappers off a type, reporting what it unwrapped — useful when reflecting over DTO or entity properties (e.g. a custom OpenAPI or binding transformer):

```csharp
using Eksen.Core.Helpers;

// IReadOnlyList<Money?> -> Money, isCollection: true, isNullable: true
var element = TypeHelper.GetUnderlyingType(propertyType, out var isNullable, out var isCollection);
```

## Testing

Specifications are the most test-worthy unit here — assert `IsSatisfiedBy` against in-memory aggregates so the rule is pinned independently of the database:

```csharp
[Fact]
public void Unpaid_Spec_Matches_Pending_Order()
{
    var order = new Order(/* ... */); // Status == OrderStatus.Pending
    new UnpaidOrderSpecification().IsSatisfiedBy(order).ShouldBeTrue();
}

[Fact]
public void Unpaid_Spec_Rejects_Paid_Order()
{
    var order = OrderBuilder.Paid();
    new UnpaidOrderSpecification().IsSatisfiedBy(order).ShouldBeFalse();
}
```

For services that depend on `IRandomStringGenerator`, swap in a deterministic fake (the **test-base** skill ships one) so generated values are predictable in assertions.

## Checklist

- [ ] Bootstrap the stack with `services.AddEksen(eksen => ...)` and chain each module's `AddX(...)` off the `IEksenBuilder`.
- [ ] When authoring a new Eksen module, expose an `AddX(this IEksenBuilder ...)` extension that returns the builder and reaches services via `builder.Services`.
- [ ] Register your module name through `AppModuleRegistry.Register(AppModules.X)`, deriving `X` from the `AppModules.Eksen` prefix.
- [ ] Model a reusable rule as a `Specification<TObj>`; implement `ToExpression()` and lean on the implicit conversion to use it in LINQ/EF queries.
- [ ] Keep specification expressions translation-friendly when they run against the database (see the entity-framework-core and repositories skills).
- [ ] Resolve `IRandomStringGenerator` for non-secret tokens only; configure the pool via `RandomStringGenerationParameters`.
- [ ] Unit-test specifications with `IsSatisfiedBy`; fake `IRandomStringGenerator` for deterministic tests.
