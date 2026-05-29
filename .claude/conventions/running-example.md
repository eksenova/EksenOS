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

ULID-based (`OrderId`, `CustomerId`, etc.) per EksenOS convention.

## Tone

- Skill code samples mention this domain only.
- No mention of eksenapi, Eksenova, banking, invoicing, or any consumer-specific concept.
- Real EksenOS APIs (`IRepository<>`, `Enumeration<>`, `ValueObject<>`, `IEksenBuilder`, etc.) are used verbatim.
