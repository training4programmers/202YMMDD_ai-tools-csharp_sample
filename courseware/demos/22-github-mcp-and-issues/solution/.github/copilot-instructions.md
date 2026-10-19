# Copilot instructions: ToolsApp

ToolsApp is an ASP.NET Core 10 minimal API (C# 14, .NET 10, EF Core 10 on SQLite) with xUnit v3 tests.
Copilot code review and Copilot chat both read this file. When you review a pull request, check
correctness and maintainability first. Formatting is handled by `dotnet format` and `.editorconfig`,
so don't comment on whitespace or brace style.

## Always check

- **Acceptance criteria.** If the pull request links an issue (`Fixes #<number>`) or changes a file in
  `docs/issues/`, check that every acceptance criterion has a test that would fail if the rule were
  removed. Name any criterion without one.
- **Nullable honesty.** Flag every new `!` (null-forgiving operator) and `= null!`. Ask for a null
  check or a non-nullable design instead, unless the comment beside it proves the value can't be null.
- **Async all the way.** Flag `.Result`, `.Wait()`, `async void`, and any async EF Core or HTTP call
  that doesn't pass the `CancellationToken` it was given.
- **EF Core queries.** Flag queries that load whole tables to filter in memory (`AsEnumerable()` or
  `ToList()` before `Where`), and string comparisons that call `ToLower()`/`ToUpper()` on a column.
- **Tests.** Flag any change that deletes, skips, or weakens an existing test, and any new test that
  can't fail (no assertion, or an assertion that matches whatever the code returns).
- **Dependencies.** Flag every new `PackageReference`. Package versions belong in
  `Directory.Packages.props`.
- **Secrets.** Flag connection strings with passwords, tokens, or keys in any file, including
  `.mcp.json` and `.vscode/mcp.json`: tokens come from environment variables or a sign-in.

## Don't

- Don't suggest switching to controllers, MediatR, AutoMapper, or Newtonsoft.Json.
- Don't suggest `Startup.cs`, `WebClient`, or other pre-.NET 6 patterns.
- Keep each comment to one issue, with a concrete suggested change where you can.
