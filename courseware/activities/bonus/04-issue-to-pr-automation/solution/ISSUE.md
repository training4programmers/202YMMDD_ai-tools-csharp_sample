# Show an author's other books for a book

<!-- One acceptable way to turn REQUEST.md into an agent-ready issue. Paste everything below into GitHub. -->

## Goal

Customers who like a book ask what else the author wrote. Add an endpoint that returns the author's
**other** books for a given book, so the book page can show them. By default only books we can sell today
(in stock) are listed; staff can ask for all of them to take special orders.

## Contract

`GET /api/books/{id}/more-by-author?includeOutOfStock=false`

- Returns `200 OK` with a JSON array of books in the existing `BookDto` shape.
- Lists the other books by the same author as book `{id}`; the book itself is never included.
- `includeOutOfStock` is optional and defaults to `false`. When `false`, books with `stock` 0 are left out.
- Ordered newest first by `publishedOn`, then by title.
- An empty array is a valid answer (the author has no other books, or none in stock).
- Unknown book id: `404 Not Found`.

Example with the seed data, for book 1 ("The Quiet Compiler", Maya Okafor):

```json
[
  { "id": 2, "title": "Rivers of Glass", "isbn": "978-1-23456-002-7", "price": 24.50, "stock": 3,
    "genre": "Fiction", "publishedOn": "2022-09-13", "authorId": 1, "authorName": "Maya Okafor" }
]
```

## Acceptance criteria

- [ ] Book 1 returns only "Rivers of Glass".
- [ ] Book 3 ("A Short History of Bridges") returns an empty array, because the author's other book is out
      of stock; with `includeOutOfStock=true` it returns "Northern Lights Field Guide".
- [ ] An unknown id returns 404.
- [ ] API tests cover each case above through `BookstoreApiFactory`.
- [ ] `dotnet build` has no warnings and `dotnet test --solution Bookstore.slnx` passes.
- [ ] The new request is added to `src/Bookstore.Api/Bookstore.Api.http`.

## Constraints and out of scope

- Follow `AGENTS.md`. No new NuGet packages. Don't change existing endpoints, DTOs, or tests.
- No paging: authors have only a handful of books.

## Pointers

- `src/Bookstore.Api/Endpoints/BookEndpoints.cs`: the route group, `GetBookAsync`, and `ToDto()`.
- `src/Bookstore.Api/Endpoints/AuthorEndpoints.cs`: `GetAuthorBooksAsync` already lists an author's books.
