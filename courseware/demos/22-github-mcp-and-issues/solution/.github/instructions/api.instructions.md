---
applyTo: "src/ToolsApp.Api/**/*.cs"
---

# ToolsApp API endpoint rules

- Endpoints live in `Endpoints/*Endpoints.cs` as static handlers mapped on a route group
  (`app.MapGroup("/api/...")`). Handlers are `private static async Task<Results<...>>` methods.
- Return `TypedResults`, and list every outcome in the `Results<...>` union so the OpenAPI document
  is accurate. Add `.ProducesProblem(...)` or `.ProducesValidationProblem()` for outcomes the union
  can't describe.
- Request bodies are `sealed record` types in `Contracts/` with DataAnnotations. `AddValidation()`
  runs them before the handler, so don't re-check `[Required]` or `[StringLength]` by hand.
- Use the right status code: 400 for a malformed request, 404 for an unknown id, 409 (as problem
  details) when a valid request conflicts with existing data, such as a duplicate name.
- Every handler takes a `CancellationToken` and passes it to every EF Core call.
- Read-only queries use `AsNoTracking()`. Filtering, sorting, and paging happen in SQL, not after
  `ToListAsync()`.
- Palette names are unique ignoring case. The column uses the `NOCASE` collation, so compare with
  `p.Name == name`; don't call `ToLower()` on the column.
