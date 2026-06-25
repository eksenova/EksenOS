# EksenOS

EksenOS is a collection of lightweight, composable building-block packages for building
ASP.NET Core applications the domain-driven way. Each package owns one concern (value objects,
repositories, the event bus, …), ships independently, and plugs into a single composition root
through `AddEksen` / `IEksenBuilder`. You assemble the packages you need into **vertical
slices** — one feature, modelled top to bottom.

This file is the high-level map. The per-package how-to lives in the skills under
`.claude/skills/` (one skill per capability family); the rules every slice follows are in
`.claude/conventions/` and the **code-conventions** skill.

## A vertical slice

A feature is built as a slice through four layers. Using the running example (placing and
fulfilling an `Order`):

```
Host / API            Endpoints, controllers, model binding, OpenAPI/Scalar docs, auth
   │  (OrderNumber, OrderId, OrderStatus bind & serialize as bare primitives; errors → HTTP)
   ▼
Application           App services & event handlers — orchestrate a use case
   │  (depend on IRepository<Order, OrderId> ordersRepository + IUnitOfWork; publish events)
   ▼
Infrastructure        EF Core DbContext + configs, repository impls, auditing, event transport
   │  (HasConversion for value objects; outbox; SQL Server / Sqlite)
   ▼
Domain                Aggregates, entities, value objects, smart enums, ULID ids, domain rules
                      (Order.Place(...), Order.MarkPaid(); OrderNumber, Money, OrderStatus)
```

Top to bottom is both the **call flow** of a request and the **dependency** direction: the Host
depends on the Application, the Application on the Infrastructure (repositories, DbContext,
transport), and the Infrastructure on the Domain. Each layer depends only on the ones beneath it,
down to the Domain at the core — which depends on nothing and has no outward dependencies.

## The building blocks

| Layer | Concern | Packages | Skill |
|---|---|---|---|
| Host | Error → HTTP mapping | `Eksen.ErrorHandling` (+ `.AspNetCore`) | **error-handling** |
| Host | Localized text | `Eksen.Localization` | **localization** |
| Host | OpenAPI document | `Eksen.OpenApi` | **open-api** |
| Host | API reference UI | `Eksen.Scalar` | **scalar** |
| Application | Data access | `Eksen.Repositories` | **repositories** |
| Application | Transaction boundary | `Eksen.UnitOfWork` (+ `.AspNetCore`) | **unit-of-work** |
| Application | Integration/domain events | `Eksen.EventBus` (+ `InMemory`/`RabbitMq`/`EntityFrameworkCore`/`Dashboard`/`Alerts`) | **event-bus** |
| Application | Cross-instance mutual exclusion | `Eksen.DistributedLocks` (+ `PostgreSql`/`SqlServer`) | **distributed-locks** |
| Application | Multi-resource sagas | `Eksen.DistributedTransactions` | **distributed-transactions** |
| Application | Authorization | `Eksen.Permissions` (+ `.AspNetCore`, `.EntityFrameworkCore`) | **permissions** |
| Application | Identity / current user | `Eksen.Identity` (+ `.AspNetCore`, `.EntityFrameworkCore`) | **identity** |
| Infrastructure | ORM, repositories, UoW impl | `Eksen.EntityFrameworkCore` (+ `SqlServer`/`Sqlite`) | **entity-framework-core** |
| Infrastructure | Audit stamping | `Eksen.Auditing` (+ `.AspNetCore`, `.EntityFrameworkCore`) | **auditing** |
| Infrastructure | Seed data | `Eksen.DataSeeding` | **data-seeding** |
| Infrastructure | Email | `Eksen.Emailing` (+ `.Gmail`) | **emailing** |
| Infrastructure | Templating | `Eksen.Templating` | **templating** |
| Infrastructure | API-key auth | `Eksen.Authentication.ApiKeys` (+ adapters) | **api-key-authentication** |
| Domain | Aggregate/entity base types | `Eksen.Entities` | **entities** |
| Domain | Value objects (immutable typed values) | `Eksen.ValueObjects` (+ `.AspNetCore`) | **value-objects** |
| Domain | Smart enumerations (named value sets) | `Eksen.SmartEnums` (+ `.AspNetCore`, `.OpenApi`) | **smart-enumerations** |
| Domain | ULID strongly-typed ids | `Eksen.Ulid` (+ `.AspNetCore`, `.OpenApi`) | **ulid** |
| Domain | Composable business rules | `Eksen.Core` (`Specification<>`) | **core** |
| Tests | Fixtures & test hosts | `Eksen.TestBase` (+ `.AspNetCore`, `.SqlServer`) | **test-base** |

## Composition root

Everything is registered off `AddEksen`, so a project's startup reads as the list of slices'
building blocks:

```csharp
services.AddEksen(eksen => eksen
    .AddValueObjects(valueObjects => valueObjects
        .Configure(options => options.AddAssembly(typeof(OrderNumber).Assembly))
        .AddAspNetCoreSupport())
    .AddSmartEnums(smartEnums => smartEnums
        .Configure(options => options.AddAssembly(typeof(OrderStatus).Assembly))
        .AddAspNetCoreSupport())
    .AddUlid(ulid => ulid.AddAspNetCoreSupport())
    .AddEntityFrameworkCore<OrderingDbContext>(db => db.UseSqlServer(connectionString))
    .AddEventBus(bus => bus
        .Configure(options => options.AppName = "orders")
        .SubscribeFromAssembly(typeof(OrderPlacedIntegrationEvent).Assembly)
        .UseInMemory()));
```

## Conventions (read before writing code)

- **`.claude/conventions/running-example.md`** — the e-commerce domain every example uses.
- **`.claude/conventions/code-style.md`** — mechanical C# rules (value-objects-over-primitives,
  repository naming, primary-constructor layout, member ordering, braces, no expression bodies).
- **code-conventions skill** — the architectural rulebook (value objects required, smart enums
  over `enum`, ULID identities, repositories inside a unit of work, errors at the edge).

## Repository layout

```
src/eksenos/
├── packages/            # one folder per package: <Name>/src + <Name>/test
├── common.props         # shared build properties
├── EksenOS.slnx         # the solution
└── .claude/             # Claude Code plugin marketplace
    ├── conventions/     # running-example.md, code-style.md
    └── skills/          # one skill per capability family
```
