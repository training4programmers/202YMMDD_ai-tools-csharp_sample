---
name: add-mcp-tool
description: Adds a new read-only tool to the Bookstore.Mcp server the way this team does it, with model-facing descriptions, input validation, and unit tests. Use it when asked to expose a new capability, lookup, or calculation to AI agents through the Bookstore MCP server.
---

# Add a tool to Bookstore.Mcp

Follow these steps in order. Stop and ask if the tool would need to change data: this server is read-only
by design, and its database connection is opened with `Mode=ReadOnly`.

1. **Design the contract first.** Write down the tool name (snake_case, verb first, such as `check_stock`),
   each parameter with its type and limits, the result record, and every error case. Show it to the user
   before writing code if anything is unclear.
2. **Data access.** If the tool needs new data, add a method to `IBookCatalog` and `SqliteBookCatalog`.
   Use SQL parameters for every value (never string concatenation), and compare ISBNs with hyphens and
   spaces removed. Prices are stored as REAL: convert with `(decimal)reader.GetDouble(i)`.
3. **The tool method** goes on `BookTools`:
   - `[McpServerTool(Name = "...", ReadOnly = true, Idempotent = true, OpenWorld = false)]`.
   - A `[Description]` on the method and on every parameter, written for a model: what it does, what it
     returns, the units and limits, and what it does **not** do.
   - Reuse the domain (`Isbn`, `PriceCalculator`) instead of re-implementing a rule.
   - Validate input first. Throw `McpException` with a message that tells the model how to fix the call;
     other exceptions reach the model only as a generic error.
   - Take a `CancellationToken` as the last parameter and pass it to every async call.
4. **Tests** in `tests/Bookstore.Tests/BookToolsTests.cs`: call the method directly with `FakeBookCatalog`.
   Cover the happy path, each validation error, and an unknown ISBN. If you added SQL, add a case to
   `SqliteBookCatalogTests`, which runs against the schema the API really creates.
5. **Verify:** `dotnet build` (no warnings) and `dotnet test --solution Bookstore.slnx`.
6. **Tell the user** to reconnect the server (`/mcp` in Claude Code, or restart it from `MCP: List Servers`
   in VS Code) so the client sees the new tool, and to add it to the `permissions.allow` list in
   `.claude/settings.json` if it should run without a prompt.
