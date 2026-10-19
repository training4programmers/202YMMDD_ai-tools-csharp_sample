# Activity 14: Extend an MCP Server and Package Workflows

## Objective

`src/Bookstore.Mcp` is a working stdio MCP server with one read-only tool, `find_books`. Extend it so an
agent can answer real shop questions: **`check_stock(isbn)`** reports the copies on the shelf, and
**`quote_order(lines, coupon)`** prices an order with the shop's actual `PriceCalculator` rules without
changing anything. Register the server in both tools, test the new tools, and then package the team's
workflow as files: a format hook, a reviewer or test-writer agent, and a skill.

![MCP architecture for the Bookstore: a host containing the model, the MCP client, and the server configuration connects over stdio to the local Bookstore.Mcp server (find_books, check_stock, quote_order, reading bookstore.db read-only) and over HTTPS to the remote GitHub MCP server.](../../diagrams/png/07-mcp-architecture-bookstore.png)

*Figure: The agent decides when to call a tool; you decide which servers it may use.*

## Prerequisites

- .NET 10 SDK, Git, and PowerShell 7
- GitHub Copilot (agent mode in VS Code or Visual Studio 2026) or Claude Code
- Activity 13, and the chapters [Subagents, Hooks, and Skills](../../docs/module-07-agentic-coding/05-subagents-hooks-and-skills.md)
  and [MCP Servers](../../docs/module-07-agentic-coding/06-mcp-servers.md)

## Skills Practiced

- Adding tools to a C# MCP server with `[McpServerTool]` and model-facing `[Description]` text
- Reusing domain logic (`Isbn`, `PriceCalculator`) behind an MCP tool instead of re-implementing it
- Returning errors the model can act on with `McpException`
- Registering a stdio server for Claude Code (`.mcp.json`) and Copilot (`.vscode/mcp.json`)
- Unit-testing tool methods directly with a fake
- Packaging a hook, an agent, and a skill as committed files

## Instructions

Start the lab from the course repo root. This copies `begin/` to a folder outside the repo, makes it a
git repository, and commits a baseline, so `git diff` always shows exactly what you and your assistant
changed:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 14
cd ~\labs\activity-14-hooks-skills-subagents-and-mcp
```

Open the folder (or `Bookstore.slnx`) in Visual Studio 2026 or VS Code. To start over, re-run the
command with `-Force`. The reference in `solution/` is **one acceptable result**; yours will differ.

> **In class / take-home.** In class (about 40 minutes): steps 1 to 3, plus step 5's tests for
> `check_stock`. Take-home: step 4 (`quote_order`), step 6 (the hook, agent, and skill), step 7, and the
> stretch goal.

1. **Get the server running.** Build and test, then run the API once to create its database
   (`dotnet run --project src/Bookstore.Api`, wait for "Now listening", press `Ctrl+C`). Read
   `src/Bookstore.Mcp`: how the host is set up, why logs go to stderr, how the connection is made
   read-only, and how `find_books` is described and tested.

   <details>
   <summary>Guided help: step 1</summary>

   Run these from the lab folder. The API run creates the SQLite file that the MCP server reads:

   ```powershell
   dotnet build
   # Build succeeded. 0 Warning(s)

   dotnet test --solution Bookstore.slnx
   # Test run summary: Passed!   total: 29

   dotnet run --project src/Bookstore.Api
   # Now listening on: http://localhost:5080   (wait for this line, then press Ctrl+C)

   Test-Path src\Bookstore.Api\bookstore.db
   # True
   ```

   Then read `src/Bookstore.Mcp` in this order: `Program.cs`, `BookstoreDatabase.cs`, `BookCatalog.cs`,
   `BookTools.cs`, then `tests/Bookstore.Tests/BookToolsTests.cs` and `SqliteBookCatalogTests.cs`. To have
   the assistant walk you through them, stay read-only (Ask mode, or Claude Code's plan mode):

   ```text
   Explain src/Bookstore.Mcp without changing anything: how Program.cs sets up the host and registers
   the tools, why every log line goes to stderr, where BookstoreDatabase finds bookstore.db and how it
   makes the connection read-only, what the [McpServerTool] and [Description] attributes on find_books
   tell a model, and how BookToolsTests tests find_books without a database.
   ```

   **Check:** you can answer each point yourself: the MCP protocol messages travel on stdout, so a log
   line there would corrupt them; `Mode = SqliteOpenMode.ReadOnly` makes SQLite itself refuse writes; the unit
   tests use a `FakeBookCatalog`, and `SqliteBookCatalogTests` runs the SQL against the schema the API's
   own `BookstoreDbContext` creates.

   **If a tool later says "No Bookstore database at ...":** run the API once more from the lab folder; the
   server looks for `src/Bookstore.Api/bookstore.db` next to `Bookstore.slnx`.

   </details>

2. **Register it in both tools.** Add `.mcp.json` for Claude Code and `.vscode/mcp.json` for VS Code. Run
   the server in Release so it doesn't lock your Debug build output, and mind how each tool resolves the
   project path: VS Code's `${workspaceFolder}` always points at the folder you opened, but in `.mcp.json`
   Claude Code normally resolves the path against the folder you start `claude` in, so start it from the
   solution folder. (VS Code's guided flow labels `.vscode/mcp.json` deprecated, but it still works.)
   Visual Studio reads the solution's `.mcp.json` first and can't use Claude Code's path, so in Visual
   Studio add the server through its own dialog instead (see the guided help). Check that your agent can
   call `find_books`.

   <details>
   <summary>Guided help: step 2</summary>

   Two short JSON files. Write them yourself, or ask an agent (agent mode, or Claude Code's Manual mode
   (`default`)):

   ```text
   Register src/Bookstore.Mcp as a stdio MCP server named bookstore for both tools, without changing any
   code: .mcp.json (key mcpServers) for Claude Code and .vscode/mcp.json (key servers) for VS Code. Both
   run dotnet run --project <path>/src/Bookstore.Mcp -c Release, with DOTNET_NOLOGO=1 in env. For
   <path>, use ${CLAUDE_PROJECT_DIR:-.} in .mcp.json and ${workspaceFolder} in .vscode/mcp.json.
   ```

   `.mcp.json` should look like this; `.vscode/mcp.json` is the same with `"servers"` in place of
   `"mcpServers"` and `${workspaceFolder}` at the start of the path:

   ```json
   {
     "mcpServers": {
       "bookstore": {
         "type": "stdio",
         "command": "dotnet",
         "args": ["run", "--project", "${CLAUDE_PROJECT_DIR:-.}/src/Bookstore.Mcp", "-c", "Release"],
         "env": { "DOTNET_NOLOGO": "1" }
       }
     }
   }
   ```

   > **GitHub Copilot:** In VS Code, run **MCP: List Servers**, start `bookstore`, trust it, and turn its
   > tools on with **Configure Tools** in the chat input.
   >
   > In Visual Studio 2026, don't rely on these two files. Visual Studio reads the solution folder's
   > `.mcp.json` before `.vscode\mcp.json`, documents only the `servers` key, and doesn't expand Claude
   > Code's `${CLAUDE_PROJECT_DIR:-.}`, so its `bookstore` entry may not start. Add the server yourself:
   > in agent mode, select **Tools**, then **+** > **Add custom MCP server**, and enter Destination
   > **Global** (the **Solution** destination writes a `.mcp.json` into the solution folder, the file
   > Claude Code reads), Server ID `bookstore-vs`, Type stdio, and the command
   > `dotnet run --project <absolute path>\src\Bookstore.Mcp -c Release`. In the lab folder,
   > `(Resolve-Path src\Bookstore.Mcp).Path` prints the absolute path. Then enable the server's tools in
   > the **Tools** picker (they're off by default), and delete the entry from `%USERPROFILE%\.mcp.json`
   > after the lab. Or do this step in VS Code or Claude Code.
   >
   > **Claude Code:** Start `claude` in the lab folder, approve the project server when asked, and run
   > `/mcp`. In a terminal, `claude mcp list` shows `bookstore` as Connected ("Pending approval" until
   > you approve it in a session).

   Then ask, in agent mode:

   ```text
   Using the bookstore MCP server, which books by Maya Okafor do we stock, and how many copies of each?
   ```

   **Check:** the agent calls `find_books` (Claude Code asks you to allow `mcp__bookstore__find_books`)
   and reports "The Quiet Compiler" (12 copies) and "Rivers of Glass" (3).

   **If it doesn't:** in Claude Code, a server that won't start usually means you started `claude` in
   another folder, so the `.` in the path pointed there. Exit, `cd` to the lab folder, and run `claude`
   (`--continue` only finds conversations started in the current folder, so ask again in the new
   session).

   </details>

3. **Add `check_stock(isbn)`.** Accept an ISBN-13 with or without hyphens; return the title and the
   copies in stock. Invalid or unknown ISBNs produce an error message the model can act on.

   <details>
   <summary>Guided help: step 3</summary>

   Plan first, then implement.

   > **GitHub Copilot:** Agent mode; attach `BookTools.cs`, `BookCatalog.cs`, and `Isbn.cs` with `#file`,
   > and ask for the plan before any edits.
   >
   > **Claude Code:** plan mode (Shift+Tab until the footer shows it); accept the plan to implement it.

   ```text
   Plan, then add a read-only check_stock(isbn) tool to BookTools in src/Bookstore.Mcp, with find_books
   as the pattern. Validate the ISBN-13 with Isbn.IsValid (hyphens optional) and look it up by
   Isbn.Normalize. Add FindByIsbnsAsync(IReadOnlyCollection<string> normalizedIsbns, CancellationToken)
   to IBookCatalog and SqliteBookCatalog (one SQL parameter per ISBN; compare stored ISBNs with hyphens
   and spaces removed), and to the FakeBookCatalog in BookToolsTests.cs so the tests still build;
   quote_order will reuse it. Return a record with the ISBN, title, copies in stock, and whether any are
   in stock. An invalid or unknown ISBN throws McpException with a message that says how to fix the
   call. Write the [Description] text for a model. Run dotnet build.
   ```

   **Check:** the attribute reads `[McpServerTool(Name = "check_stock", ReadOnly = true, Idempotent =
   true, OpenWorld = false)]`, the method and its parameter each have a `[Description]`, and no value is
   concatenated into the SQL. Reconnect the server (`/mcp` in Claude Code; restart it from **MCP: List
   Servers** in VS Code), then ask:

   ```text
   Use the bookstore tools: is 978-1-23456-004-1 in stock? And 978-1-23456-004-0?
   ```

   The first is "Northern Lights Field Guide" with 0 copies; the second has a wrong check digit, and the
   agent should pass on your error message.

   **If the agent only sees "An error occurred invoking 'check_stock'":** the tool threw something other
   than `McpException`, and the SDK hides other exceptions' messages from the model. Ask for
   `McpException` with a fix-it message.

   </details>

4. **Add `quote_order(lines, coupon)`.** `lines` is a list of ISBN and quantity pairs (1 to 20 lines, each
   book once, quantity 1 to 1000); `coupon` is optional. Price the order with the existing
   `PriceCalculator`, report for each line whether the shelf can fill it, and say clearly in the
   description that the tool doesn't place an order or change stock. Unknown ISBNs and unknown coupons are
   errors the model can act on.

   <details>
   <summary>Guided help: step 4</summary>

   Same mode as step 3: plan, check the plan, then implement.

   ```text
   Plan, then add a read-only quote_order(lines, coupon) tool to BookTools. lines is a list of
   OrderLineInput(Isbn, Quantity) records with a [Description] on each property: 1 to 20 lines, each book
   at most once (compare normalized ISBNs, so 978-1-23456-005-8 and 9781234560058 are the same book),
   quantity 1 to 1000. coupon is optional (string? coupon = null) and the CancellationToken stays last.
   Inject PriceCalculator through BookTools' constructor, register it as a singleton in Program.cs, and
   update how BookToolsTests.cs constructs BookTools. Price only with calculator.Calculate: no pricing
   arithmetic in the tool. Return each line with its unit price, copies in stock, and whether the shelf
   can fill it, plus subtotal, discount, tax, total, and whether the whole order can be filled. Unknown
   ISBNs and unknown coupons throw McpException; the coupon message names the valid coupons. The
   description lists the pricing rules and says the tool doesn't place an order, reserve copies, or
   change anything. Run dotnet build.
   ```

   **Check the plan:** it builds `QuoteLine` objects and calls `calculator.Calculate`, and it turns the
   calculator's `ArgumentException` for an unknown coupon (`ParamName` is `couponCode`) into an
   `McpException`. After implementing and reconnecting, compare one quote with the API: run the API, send
   the "Price an order" request in `src/Bookstore.Api/Bookstore.Api.http` (books 5 and 7 with `READMORE`),
   and ask the agent for the same order by ISBN (10 x 978-1-23456-005-8 and 1 x 978-1-23456-007-2). Both
   must say subtotal 462.99, discount 50.00, tax 24.78, total 437.77.

   **If the totals differ:** the tool is doing its own sums. Reply: "Remove the pricing arithmetic from
   quote_order and call calculator.Calculate, so its totals always match POST /api/quotes."

   </details>

5. **Test the tools.** Unit-test each tool method directly, including every validation error. If you add
   SQL, test it against the database the API's own `DbContext` creates.

   <details>
   <summary>Guided help: step 5</summary>

   A tool is a plain method, so no MCP client is involved. In class, send only the first paragraph.

   ```text
   In tests/Bookstore.Tests/BookToolsTests.cs, test CheckStockAsync directly with FakeBookCatalog: an
   ISBN with and without hyphens (978-1-23456-002-7 and 9781234560027 both give "Rivers of Glass" with 3
   in stock), a wrong check digit (978-1-23456-002-0), and a valid ISBN that isn't in the catalog
   (9781234560096), each failure as an McpException.

   Then test QuoteOrderAsync with the real PriceCalculator, adding the books you need to the fake:
   10 x 978-1-23456-005-8 ($45.00) plus 1 x 978-1-23456-007-2 ($12.99) with READMORE gives subtotal
   462.99, discount 50.00, tax 24.78, total 437.77; 4 copies of a book with 3 in stock can't be filled.
   Add a [Theory] of invalid orders, each an McpException: no lines, quantity 0, quantity 1001, a wrong
   check digit, an unknown ISBN, the same book twice (once with hyphens, once without), and an unknown
   coupon. Add a SqliteBookCatalogTests case for FindByIsbnsAsync against the real schema. Don't change
   the existing tests' assertions. Run dotnet test --solution Bookstore.slnx.
   ```

   **Check:** `dotnet build` shows 0 warnings and every test passes. The starter has 29 tests; the
   reference solution has 42.

   **If the assistant gets an expected total by calling the calculator:** reject that test. The numbers
   above were worked by hand from the pricing rules, and the test should state them as literals.

   </details>

6. **Package the workflow.** Add a format-after-edit hook for your tool (and the other tool's equivalent if
   you can), a **reviewer** or **test-writer** agent definition for both tools, and a **skill** that
   captures a procedure your team would repeat (for example, how to add a tool to this server).

   <details>
   <summary>Guided help: step 6</summary>

   Three prompts, in agent mode or Claude Code's Manual mode so you approve each file (Claude Code always
   asks before it writes under `.claude/`). The hook first:

   ```text
   Every C# file an agent edits should be formatted automatically, in both tools, by one script.
   - scripts/format-changed.ps1 (PowerShell 7): ignore the JSON on stdin; ask git for changed and new
     .cs files; skip any not modified since the last run (keep a stamp file under .git/); run
     dotnet format Bookstore.slnx --include on the rest; on failure, write why to stderr and exit 2.
   - Claude Code: a PostToolUse hook on Edit|Write in .claude/settings.json, exec form (command pwsh,
     args -NoProfile -File ${CLAUDE_PROJECT_DIR}/scripts/format-changed.ps1), timeout 120.
   - Copilot CLI and cloud agent: .github/hooks/dotnet-format.json, version 1, postToolUse with matcher
     edit|create, running the same script.
   ```

   Then the agent, as two files with the same instructions:

   ```text
   Add a code-reviewer agent: .claude/agents/code-reviewer.md with tools: Read, Grep, Glob, Bash,
   PowerShell, and .github/agents/code-reviewer.agent.md with no tools line (Visual Studio and VS Code
   name tools differently). It reads git diff and each changed file in full, runs dotnet build and
   dotnet test --solution Bookstore.slnx, checks the change against AGENTS.md and common AI mistakes in
   C# (dropped CancellationTokens, double for money, culture-sensitive strings, vague MCP descriptions,
   error messages that don't say how to fix the call), and reports findings by file and line. It never
   edits files.
   ```

   And the skill:

   ```text
   Add .claude/skills/add-mcp-tool/SKILL.md with name add-mcp-tool and a description that says when to
   use it. Numbered, checkable steps for adding a read-only tool to Bookstore.Mcp: design the contract,
   data access with SQL parameters, the attributes and model-facing descriptions, McpException for bad
   input, tests with FakeBookCatalog, dotnet build and test, then reconnect the server.
   ```

   **Check:** the skill's `name` matches its folder, and `git status` shows `.claude/`, `.github/agents/`,
   `.github/hooks/`, and `scripts/`.

   **If the Claude agent file has no `tools:` line:** it inherits every tool, `Edit` and `Write` included.
   Ask for `tools: Read, Grep, Glob, Bash, PowerShell`.

   </details>

7. **Try it end to end.** Reconnect the server and ask your agent a shop question that needs both new
   tools, then invoke your skill and your agent once each. In Claude Code, load what you added first: a
   skills folder that didn't exist when the session started needs `/reload-skills`, the first file in a
   new `agents` folder needs a restart (`claude --continue`), and `/hooks` shows and reloads your hook.

   <details>
   <summary>Guided help: step 7</summary>

   > **GitHub Copilot:** In VS Code, restart `bookstore` from **MCP: List Servers**. For the review, pick
   > **code-reviewer** from the agent picker; Copilot reads skills from `.claude/skills/` too.
   >
   > **Claude Code:** Restart with `claude --continue`, which loads the new agent and the new skill (for
   > the skill alone, `/reload-skills` is enough). `/skills` lists `add-mcp-tool`, `/hooks` shows the
   > hook, and `/mcp` reconnects `bookstore`.

   A shop question that needs both new tools:

   ```text
   Is 978-1-23456-004-1 in stock? If it isn't, quote 2 copies of 978-1-23456-003-4 with coupon READMORE
   instead, and tell me whether we can fill the order. Use the bookstore tools.
   ```

   Then the skill (in plan mode, so it only plans) and the agent, once each:

   ```text
   /add-mcp-tool Plan a books_by_author(name) tool. Plan only: don't write any code.
   ```

   ```text
   Use the code-reviewer agent to review my uncommitted changes.
   ```

   **Check:** the agent calls `check_stock` (0 copies), then `quote_order` for "A Short History of
   Bridges": subtotal 64.00, discount 5.00, tax 3.54, total 62.54, and the shelf's 7 copies can fill it.
   The review lists findings with file and line (or "No blocking issues"), and `git status` shows it
   changed nothing. Last, ask the agent to add a blank line to any `.cs` file so the hook has an edit to
   format: `Test-Path .git\format-hook.stamp` then returns `True`. (Remove the blank line afterwards by hand;
   `git restore` would also discard any uncommitted work in that file.)

   **If the stamp never appears:** run the hook script by hand and read its error:
   `'{}' | pwsh -NoProfile -File scripts/format-changed.ps1; $LASTEXITCODE`.

   </details>

<details>
<summary>Prompting hints</summary>

- Point the agent at the existing `find_books` tool and its tests as the pattern to follow, and at
  `PriceCalculator` and `Isbn` as the logic to reuse.
- Say what each tool must not do (write data, reserve stock) and ask for descriptions written for a
  model: purpose, inputs and limits, output, and non-effects.
- Ask which exception type reaches the model with its message, and for error messages that say how to
  fix the call.
- For registration, mention both file formats (`mcpServers` and `servers`) and the path variable each tool
  supports.
- For the agent definition, ask for least-privilege tools: a reviewer needs to read and run commands, not
  edit.
- For the skill, give it the name, the trigger situations, and the steps; ask for numbered, checkable
  steps.

</details>

## Requirements

- `check_stock` and `quote_order` exist alongside `find_books`, are marked read-only, and have specific
  `[Description]` text on each tool and parameter.
- `quote_order` uses `PriceCalculator`, so its totals match `POST /api/quotes` for the same order.
- Bad input (invalid ISBN, unknown ISBN, out-of-range quantity, duplicate book, empty order, unknown coupon)
  returns an error message that tells the model what to fix.
- The server is registered in `.mcp.json` and `.vscode/mcp.json`, and your agent can call all three tools.
- Each new tool has unit tests, and `dotnet test --solution Bookstore.slnx` passes with no build warnings.
- The repository contains a format hook, an agent definition (reviewer or test-writer), and a skill with a
  valid `name` and `description`.
- The database connection is still read-only.

## Expected behavior

```powershell
dotnet build                          # Build succeeded. 0 Warning(s)
dotnet test --solution Bookstore.slnx # all tests pass, including the new tool tests
```

Ask your agent, with the server connected (illustrative answers):

```text
How many copies of 9781234560027 do we have?
-> check_stock: "Rivers of Glass", 3 in stock

Quote 10 copies of 978-1-23456-005-8 and 1 copy of 9781234560072 with coupon READMORE.
-> quote_order: subtotal 462.99, discount 50.00, tax 24.78, total 437.77, can fulfil: yes

Quote 4 copies of Rivers of Glass.
-> quote_order: total 103.88, can fulfil: no (3 in stock)
```

## Stretch goal

Add an MCP **resource** or a fourth tool, `books_by_author(name)`, and expose it to only one agent:
restrict your reviewer agent's MCP tools, or add the tool to `permissions.allow` while leaving the others
to ask. (Leave `solution/` as is; this is extra practice.)

## Reference

- Concepts: [Module 07: Agentic Coding](../../docs/module-07-agentic-coding/), especially
  [Subagents, Hooks, and Skills](../../docs/module-07-agentic-coding/05-subagents-hooks-and-skills.md) and
  [MCP Servers](../../docs/module-07-agentic-coding/06-mcp-servers.md)
- Worked example: [matching demo](../../demos/14-hooks-skills-subagents-and-mcp/)
