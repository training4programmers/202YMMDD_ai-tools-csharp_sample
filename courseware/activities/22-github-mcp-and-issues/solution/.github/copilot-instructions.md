# Copilot instructions: Bookstore

The Bookstore API is ASP.NET Core 10 minimal APIs (C# 14, .NET 10), EF Core 10 on SQLite, and xUnit v3.
Copilot code review and Copilot chat both read this file. When you review a pull request, check
correctness against the linked issue first, then maintainability. `dotnet format` and `.editorconfig`
handle formatting, so skip formatting comments.

## Always check

- **Acceptance criteria.** If the pull request links an issue (`Fixes #<number>`) or changes a file in
  `docs/issues/`, name any criterion that has no test, and any test that can't fail.
- **Money.** Prices and totals are `decimal`, rounded with `MidpointRounding.AwayFromZero` as
  `PriceCalculator` does. Flag `double` or `float` for money and rounding without a mode.
- **Dates.** Flag `DateTime.Now`, `DateTime.Today`, and `DateTime.UtcNow` in application code. "Today"
  comes from the injected `TimeProvider`, so tests can fix the clock.
- **Validation.** Request shape rules belong in DataAnnotations on the request record; rules that need
  the database or the clock belong in the endpoint's `ValidateAsync`. Flag rules enforced in only one
  of create and update.
- **Async.** Every EF Core call is async and receives the handler's `CancellationToken`.
- **Nullable.** Flag new `!` operators and `= null!` without a comment explaining why the value can't
  be null.
- **Tests and packages.** Flag deleted or weakened tests, and any new `PackageReference`.
- **Secrets.** Flag tokens, keys, and passwords in any file, including `.mcp.json` and
  `.vscode/mcp.json`.

## Don't

- Don't suggest controllers, AutoMapper, MediatR, or Newtonsoft.Json.
- Keep each comment to a single issue and include a suggested change.
