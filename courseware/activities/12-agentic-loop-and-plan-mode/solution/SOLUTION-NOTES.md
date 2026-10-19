# Activity 12: Solution Notes

> This is **one acceptable result**, produced with an AI assistant and then reviewed by a person. Your
> prompts, your assistant's output, and your final code will differ. That's expected, as long as you
> meet the activity's Requirements.

## How this result was produced

- Claude Code CLI, frontier/reasoning-tier model. Plan mode for the design (two rounds of edits), then
  accept-edits mode with each `dotnet` command approved.
- About 25 agent turns for the implementation, plus one follow-up request after review.

## Prompts used

```text
/plan Read TASK.md and AGENTS.md and plan the orders feature. Don't change any files yet.
In the plan, list: the files you'll create or change; where order placement logic lives; the EF Core
mapping for the new entities; how stock is checked and removed; the status code for each failure; and
the unit and API tests you'll add. List anything in TASK.md that's ambiguous as a question.
```

```text
Changes to the plan:
1. Put placement in the domain: Order.Place(lines, coupon, calculator, placedAt). Price first, then check
   every line's stock, and only then call RemoveStock, so a bad coupon or a short line changes nothing.
2. Throw a dedicated InsufficientStockException that lists every short book; the endpoint maps it to a
   409 ProblemDetails. Don't map InvalidOperationException in general.
3. Two orders can read the same stock and both save. Make Book.Stock a concurrency token and map
   DbUpdateConcurrencyException to 409 too, with a test that proves the token works.
4. Inject TimeProvider for PlacedAt instead of calling DateTimeOffset.UtcNow.
5. Store money with HasConversion<double>() like Book.Price. No new packages.
Show me the revised plan.
```

```text
Implement the approved plan. After each change run dotnet build and dotnet test --solution Bookstore.slnx
and fix what fails. Don't change existing tests. Finish with the files you changed and the final test
summary line.
```

```text
Review follow-up: an order request with the same bookId on two lines is accepted and priced as two
lines, so neither gets the bulk discount. TASK.md says that's a 400. Add the check and an API test.
```

### The approved plan (summary)

1. `Bookstore.Domain/Order.cs`: `Order` and `OrderLine` entities, `StockShortage` record,
   `InsufficientStockException`, and `Order.Place(...)` (price, check all stock, then remove stock, copy unit
   prices onto lines).
2. `BookstoreDbContext`: `DbSet<Order>`; money columns as `double`; `Book.Stock` as a concurrency token;
   `Order` to `OrderLine` (cascade) and `OrderLine` to `Book`.
3. `Contracts/OrderContracts.cs`: `OrderRequest` (1 to 20 lines), `OrderLineRequest` (quantity 1 to 1000),
   `OrderDto`, `OrderLineDto`, and `ToDto()`.
4. `Endpoints/OrderEndpoints.cs`: `POST /api/orders` (201 with `CreatedAtRoute`, 400, 409) and
   `GET /api/orders/{id}` (200, 404); one `SaveChangesAsync` for the order and all stock changes.
5. `Program.cs`: register `TimeProvider.System`; map the endpoints. Add requests to `Bookstore.Api.http`.
6. Tests: `OrderTests` (domain) and `OrderApiTests` (a new factory per test, because orders change stock).

## What review caught

- **No protection against concurrent orders.** The first plan read stock, checked it, and saved. Two
  requests for the last three copies of "Rivers of Glass" could both pass the check. Fixed in the plan by
  making `Stock` a concurrency token; the test opens two `DbContext` scopes to prove the second save fails.
- **Partial updates on failure.** The first plan called `RemoveStock` line by line inside the loop, so a
  short third line threw after two books were already decremented in memory. It happened not to persist
  (the failed request never saved), but it relied on that accident. The plan now checks every line first.
- **Catching too much.** The draft mapped every `InvalidOperationException` to 409, which would also hide
  real bugs. A dedicated exception type makes the mapping exact.
- **Duplicate book ids.** Not handled in the first implementation; caught by reviewing against the 400
  cases in `TASK.md`.
- **Shared test database.** The first API tests used `IClassFixture`, so the order tests changed stock that
  other tests then read. Each test now gets its own factory and database.

## Edited by hand

- Nothing in the code. The `Bookstore.Api.http` requests were reordered by hand so they run top to bottom
  against a fresh database.

## Verify

```powershell
dotnet build
dotnet test --solution Bookstore.slnx
```
