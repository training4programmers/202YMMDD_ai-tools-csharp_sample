# Task: place and read orders

Today `POST /api/quotes` prices an order but changes nothing. The shop now wants real orders: pricing the
order, taking the copies off the shelf, and saving the order so it can be looked up later.

## Endpoints

### `POST /api/orders`

Request body:

```json
{
  "lines": [
    { "bookId": 5, "quantity": 10 },
    { "bookId": 7, "quantity": 1 }
  ],
  "couponCode": "READMORE"
}
```

- 1 to 20 lines; each `quantity` is 1 to 1000; `couponCode` is optional.
- Price the order with the existing `PriceCalculator` (same rules as `/api/quotes`).
- Take the copies off the shelf with the existing `Book.RemoveStock`.
- Copy each book's current price onto its order line, so later price changes don't rewrite old orders.
- Success: `201 Created`, a `Location` header of `/api/orders/{id}`, and the order in the body.

### `GET /api/orders/{id}`

`200 OK` with the order, or `404 Not Found`.

### The order, as both endpoints return it

```json
{
  "id": 1,
  "placedAt": "2026-10-16T14:03:00+00:00",
  "couponCode": "READMORE",
  "lines": [
    { "bookId": 5, "title": "Patterns in the Noise", "quantity": 10, "unitPrice": 45.00, "lineTotal": 450.00 },
    { "bookId": 7, "title": "The Lighthouse Cat", "quantity": 1, "unitPrice": 12.99, "lineTotal": 12.99 }
  ],
  "subtotal": 462.99,
  "discount": 50.00,
  "tax": 24.78,
  "total": 437.77
}
```

## Rules

- **All or nothing.** If any line can't be filled, no stock changes and no order is saved.
- Not enough copies of any book: `409 Conflict` as a ProblemDetails response whose `detail` names each
  short book with the copies requested and available.
- Unknown book ids, the same book on two lines, or quantities out of range: `400` validation problem.
- Unknown coupon code: `400` validation problem keyed `couponCode`.

## Data

- Add an `Order` entity (with its lines) to the domain and to `BookstoreDbContext`. The database is
  created with `EnsureCreated`, so no migrations are needed.
- Store money the way `Book.Price` is stored (see `BookstoreDbContext`).

## Acceptance criteria

- [ ] The example request above returns 201 with the totals shown, and book 5's stock drops from 20 to 10.
- [ ] `GET /api/orders/{id}` returns the same order; an unknown id returns 404.
- [ ] Ordering more copies than are in stock returns 409, and no book's stock changes.
- [ ] The 400 cases above each return a validation problem.
- [ ] Order placement logic has unit tests; both endpoints have API tests.
- [ ] `dotnet build` has no warnings and `dotnet test --solution Bookstore.slnx` passes.
- [ ] No new NuGet packages; existing tests unchanged.
