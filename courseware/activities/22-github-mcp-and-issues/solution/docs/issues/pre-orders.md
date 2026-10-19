# Pre-order books can be quoted but not stocked

> Copy of the GitHub issue the agent created through the GitHub MCP server. The issue on GitHub is the
> source of truth; update this file in the same pull request as any behavior change.

| Field | Value |
|-------|-------|
| Issue | `#<issue-number>` (assigned by GitHub) |
| Title | Pre-order books can be quoted but not stocked |
| Labels | `enhancement`, `ai-assisted` |
| Branch | `<issue-number>-pre-orders`, created from the issue with `gh issue develop` |
| Source | `ISSUE-DRAFT.md`, refined with an AI assistant, decisions confirmed by the product owner |

## Story

As a customer browsing upcoming titles, I want to get a price quote for a book that isn't published
yet, so that I can decide to pre-order it, while the catalog never shows stock for a book that doesn't
exist yet.

## Definitions

- **Today** is the current date in UTC, read from the application's `TimeProvider`.
- A book is a **pre-order** when its `PublishedOn` date is after today. A book published today is not
  a pre-order.

## Acceptance criteria

1. **Pre-orders can't be stocked on create.**
   Given today is 2026-10-16,
   when I create a book with `publishedOn` 2026-12-01 and `stock` 5,
   then the response is `400 Bad Request` with a validation error for `Stock`.

2. **Pre-orders with no stock can be created.**
   Given today is 2026-10-16,
   when I create a book with `publishedOn` 2026-12-01 and `stock` 0,
   then the response is `201 Created` and the book has `isPreOrder: true`.

3. **Moving a stocked book into the future is rejected.**
   Given a book with stock 12 published in 2019,
   when I update it with `publishedOn` 2027-01-15 and `stock` 12,
   then the response is `400 Bad Request` with a validation error for `Stock`, and nothing changes.

4. **Pre-orders can be quoted.**
   Given a pre-order book priced 20.00 with stock 0,
   when I request a quote for 2 copies,
   then the response is `200 OK` with a subtotal of 40.00 and the usual tax: no special pricing.

5. **A book published today isn't a pre-order.**
   Given today is 2026-10-16,
   when I create a book with `publishedOn` 2026-10-16 and `stock` 5,
   then the response is `201 Created` with `isPreOrder: false`.

6. **Book responses say which books are pre-orders.**
   `GET /api/books`, `GET /api/books/{id}`, and `GET /api/authors/{id}/books` include `isPreOrder`.

## Out of scope

- Pre-order discounts or coupons, reserving copies, and notifying customers on publication day.
- Changing stock automatically when a pre-order is published.

## Definition of done

- Each criterion has an automated test that fails if the rule is removed. Date rules are tested with a
  fixed clock, never with the machine's date.
- `dotnet build` has no warnings; `dotnet test --solution Bookstore.slnx` passes.
- The pull request says `Fixes #<issue-number>` in its description and has a human approval.
