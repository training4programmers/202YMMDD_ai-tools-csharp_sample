# ToolsApp: agent guide

ToolsApp is a small ASP.NET Core 10 minimal API for colors and palettes: hex, RGB, and HSL conversion,
and WCAG contrast checks.

## Layout

- `src/ToolsApp.Domain`: entities (`Color`, `Palette`) and pure logic (`ColorMath`). No framework
  dependencies; everything here gets unit tests.
- `src/ToolsApp.Api`: minimal API endpoints, EF Core 10 + SQLite, `ToolsApp.Api.http` for manual calls.
- `tests/ToolsApp.Tests`: xUnit v3. API tests use `ToolsAppApiFactory` (`WebApplicationFactory<Program>`
  over a throwaway SQLite file).

## Commands

- Build: `dotnet build`
- Test: `dotnet test --solution ToolsApp.slnx`
- Run: `dotnet run --project src/ToolsApp.Api` (listens on http://localhost:5090)

## Conventions

- C# 14 on .NET 10, nullable enabled, file-scoped namespaces. Use primary constructors and collection
  expressions where they make code shorter and clearer.
- One static `XxxEndpoints` class per resource with a `MapXxxEndpoints` extension method and a route group.
  Handlers are private static async methods that return `Results<...>` built with `TypedResults`.
- Pass the `CancellationToken` through every async call.
- Request DTOs are records with DataAnnotations; `AddValidation()` checks them before the handler runs.
  Rules that need the database return `TypedResults.ValidationProblem(...)`.
- Format machine-readable text (CSS, JSON, file formats) with `CultureInfo.InvariantCulture`.
- Package versions live in `Directory.Packages.props`. Ask before adding a package.

## Definition of done

- `dotnet build` reports no warnings and `dotnet test --solution ToolsApp.slnx` passes.
- New behavior has tests: domain logic in a unit test, endpoints through `ToolsAppApiFactory`.
- Don't change or delete existing tests to make them pass. If a test looks wrong, stop and ask.
