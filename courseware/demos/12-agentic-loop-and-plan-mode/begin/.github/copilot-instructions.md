# ToolsApp: Copilot instructions

ToolsApp is an ASP.NET Core 10 minimal API for colors and palettes (`src/ToolsApp.Domain`,
`src/ToolsApp.Api`, `tests/ToolsApp.Tests`). The full guide is `AGENTS.md`; Visual Studio doesn't read that
file, so the essentials are repeated here.

- C# 14 / .NET 10, nullable enabled, file-scoped namespaces, primary constructors and collection
  expressions where they help.
- Endpoints: one static `XxxEndpoints` class per resource, a route group, private static async handlers
  returning `Results<...>` via `TypedResults`, and the `CancellationToken` passed through.
- Request DTOs are records with DataAnnotations (`AddValidation()` runs them).
- Format machine-readable text with `CultureInfo.InvariantCulture`.
- Tests are xUnit v3; API tests use `ToolsAppApiFactory`. Never change existing tests to make them pass.
- Done means `dotnet build` has no warnings and `dotnet test --solution ToolsApp.slnx` passes.
