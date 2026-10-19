# Bookstore: Copilot instructions

Bookstore is an ASP.NET Core 10 minimal API for a bookshop (`src/Bookstore.Domain`, `src/Bookstore.Api`,
`src/Bookstore.Mcp`, `tests/Bookstore.Tests`). The full guide is `AGENTS.md`; Visual Studio doesn't read
that file, so the essentials are repeated here.

- C# 14 / .NET 10, nullable enabled, file-scoped namespaces, primary constructors and collection
  expressions where they help.
- Endpoints: one static `XxxEndpoints` class per resource, a route group, private static async handlers
  returning `Results<...>` via `TypedResults`, and the `CancellationToken` passed through.
- Request DTOs are records with DataAnnotations (`AddValidation()` runs them).
- Money is `decimal`, rounded with `MidpointRounding.AwayFromZero`; SQLite stores it as `REAL`.
- Tests are xUnit v3; API tests use `BookstoreApiFactory`. Never change existing tests to make them pass.
- Done means `dotnet build` has no warnings and `dotnet test --solution Bookstore.slnx` passes.
