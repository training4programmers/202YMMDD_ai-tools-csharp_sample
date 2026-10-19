# Bookstore: agent guide

Bookstore is a small ASP.NET Core 10 minimal API for a bookshop: books, authors, ISBN-13 validation, and
order pricing.

## Layout

- `src/Bookstore.Domain`: entities (`Book`, `Author`) and pure logic (`Isbn`, `PriceCalculator`). No
  framework dependencies; everything here gets unit tests.
- `src/Bookstore.Api`: minimal API endpoints, EF Core 10 + SQLite, `Bookstore.Api.http` for manual calls.
- `tests/Bookstore.Tests`: xUnit v3. API tests use `BookstoreApiFactory` (`WebApplicationFactory<Program>`
  over a throwaway SQLite file).

## Commands

- Build: `dotnet build`
- Test: `dotnet test --solution Bookstore.slnx`
- Run: `dotnet run --project src/Bookstore.Api` (listens on http://localhost:5080)

## Conventions

- C# 14 on .NET 10, nullable enabled, file-scoped namespaces. Use primary constructors and collection
  expressions where they make code shorter and clearer.
- One static `XxxEndpoints` class per resource with a `MapXxxEndpoints` extension method and a route group.
  Handlers are private static async methods that return `Results<...>` built with `TypedResults`.
- Pass the `CancellationToken` through every async call.
- Request DTOs are records with DataAnnotations; `AddValidation()` checks them before the handler runs.
  Rules that need the database return `TypedResults.ValidationProblem(...)`.
- Money is `decimal`, rounded with `MidpointRounding.AwayFromZero`. SQLite stores it as `REAL` through a
  value conversion (see `BookstoreDbContext`).
- Package versions live in `Directory.Packages.props`. Ask before adding a package.

## Definition of done

- `dotnet build` reports no warnings and `dotnet test --solution Bookstore.slnx` passes.
- New behavior has tests: domain logic in a unit test, endpoints through `BookstoreApiFactory`.
- Don't change or delete existing tests to make them pass. If a test looks wrong, stop and ask.
