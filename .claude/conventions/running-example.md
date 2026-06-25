# Running Example: E-Commerce Ordering System

Every EksenOS skill in this marketplace illustrates concepts using the same fictional e-commerce ordering domain. Do not invent new domains in skill examples.

## Aggregates

- **Customer** — identifies a buyer. Holds shipping addresses, payment instruments (by reference), and contact details.
- **Product** — a sellable item. SKU, name, price, inventory snapshot.
- **Order** — a placed order. OrderNumber, OrderStatus, customer, line items.
- **OrderItem** — a single line of an order. Product, quantity, unit price at time of order.
- **Shipment** — a fulfilled order's delivery record.
- **Payment** — captures a payment attempt against an order.

## Value Objects

`OrderNumber`, `Sku`, `Money`, `Quantity`, `EmailAddress`, `PostalCode`, `Address`.

## Smart Enumerations

- `OrderStatus`: Pending, Paid, Packed, Shipped, Delivered, Cancelled, Refunded.
- `PaymentStatus`: Authorised, Captured, Failed, Refunded.
- `ShipmentCarrier`: Ups, Fedex, Dhl, Local.

## Identifiers

ULID-based strongly-typed ids (`OrderId`, `CustomerId`, `ProductId`, `OrderItemId`,
`ShipmentId`, `PaymentId`) per EksenOS convention — never a bare `string` or `Guid`.

## Value objects everywhere

Domain values are modelled as value objects (or smart enumerations, or ULID ids), **never** as
bare primitives — on every boundary, including DTOs and integration-event payloads. An event
carries `OrderNumber`/`MoneyAmount`, not `string`/`decimal`; the event bus serializes value
objects, smart enums, and ULID ids to their underlying primitive on the wire, so the typed
payload round-trips. A primitive is only used where the value has no domain meaning. This is
rule 1 of [`code-style.md`](./code-style.md); the full rationale is in the **code-conventions** skill.

## Coding conventions

Every code sample — in every skill — also obeys the mechanical C# rules in
[`code-style.md`](./code-style.md) (repository fields named `<entities>Repository`, multi-line
primary constructors, `const`/`static` first, blank line between properties but not fields,
braces on all control statements, no expression-bodied members) and the architectural rules in
the **code-conventions** skill.

## Tone

- Skill code samples mention this domain only.
- No mention of eksenapi, Eksenova, banking, invoicing, or any consumer-specific concept.
- Real EksenOS APIs (`IRepository<>`, `Enumeration<>`, `ValueObject<>`, `IEksenBuilder`, etc.) are used verbatim.
