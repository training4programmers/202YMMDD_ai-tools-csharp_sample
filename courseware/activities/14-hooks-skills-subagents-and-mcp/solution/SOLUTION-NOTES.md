# Activity 14: Solution Notes

> This is **one acceptable result**, produced with an AI assistant and then reviewed by a person. Your
> prompts, your assistant's output, and your final code will differ. That's expected, as long as you
> meet the activity's Requirements.

## How this result was produced

- Claude Code CLI, frontier/reasoning-tier model; plan mode for the two tools, then accept-edits mode with
  `dotnet build` and `dotnet test` pre-approved. The VS Code registration and the Copilot custom agent were
  checked in VS Code with GitHub Copilot.
- About 35 agent turns over five prompts, plus two review follow-ups.

## Prompts used

```text
/plan Add two read-only tools to Bookstore.Mcp, following find_books and its tests as the pattern:
check_stock(isbn) and quote_order(lines, coupon). Reuse Isbn for validation and normalization and
PriceCalculator for pricing; quote_order must not place an order or change stock, and its description
must say so. lines: 1 to 20 ISBN/quantity pairs, each book once, quantity 1 to 1000; coupon optional.
Invalid input must produce an McpException whose message tells the model how to fix the call.
Plan the catalog method you need, the result records, and the tests.
```

```text
Approved, with one change: inject PriceCalculator into BookTools through the constructor and register
it in Program.cs, instead of creating one inside the tool. Implement it and run the tests.
```

```text
Register the server for both tools: .mcp.json (mcpServers) for Claude Code and .vscode/mcp.json (servers)
for VS Code. Run it with dotnet run -c Release, and use each tool's project-folder variable in the path
(${CLAUDE_PROJECT_DIR:-.} and ${workspaceFolder}). Pre-approve the three tools in .claude/settings.json.
```

```text
Add a code-reviewer agent for both tools (.claude/agents/code-reviewer.md and
.github/agents/code-reviewer.agent.md). It reviews git diff against AGENTS.md and common AI mistakes in
C#, runs build and tests, and never edits files, so give it read and shell tools only.
```

```text
Add a skill, .claude/skills/add-mcp-tool/SKILL.md, that captures how we add a tool to this server: design
the contract first, data access with parameters, attributes and descriptions, McpException for bad
input, tests with FakeBookCatalog, verify, then reconnect. Then add a format hook: a PowerShell 7 script,
scripts/format-changed.ps1, that runs dotnet format Bookstore.slnx --include on the .cs files changed
since its last run; a PostToolUse hook on Edit|Write in .claude/settings.json that calls it; and
.github/hooks/dotnet-format.json so the Copilot CLI and cloud agent run the same script.
```

## What review caught

- **A rounding trap in the first `quote_order`.** The draft summed `line.Book.Price * quantity` itself
  and applied the coupon by hand, duplicating `PriceCalculator` and getting the READMORE threshold
  wrong. The plan already said "reuse PriceCalculator"; the review comment pointed back to it, and the
  rewrite calls `calculator.Calculate`, so totals match `POST /api/quotes`.
- **Errors the model couldn't use.** The draft threw `ArgumentException` for bad ISBNs. The SDK reports
  any non-`McpException` to the model only as "An error occurred invoking 'quote_order'.", so the model
  had no way to correct its call. All validation errors now throw `McpException` with a fix-it message.
- **Duplicate ISBNs.** Two lines for the same book (one hyphenated, one not) were priced as separate lines,
  so neither got the bulk discount. Validation now normalizes ISBNs and rejects duplicates.
- **A `CancellationToken` in the middle of the parameter list.** The draft put `CancellationToken` before
  the optional `coupon` parameter. Moved last, as `coupon = null, cancellationToken = default`; the SDK
  keeps it out of the schema either way.
- **SQL built by string concatenation** for the ISBN list in the first draft of `FindByIsbnsAsync`. Now one
  parameter per ISBN.
- **A path that only looked independent of the working directory.** The agent's summary said
  `${CLAUDE_PROJECT_DIR:-.}` made the `.mcp.json` entry work from any folder. It doesn't: Claude Code sets
  `CLAUDE_PROJECT_DIR` in the server's environment, not in its own, so when it expands `args` the `.`
  fallback normally applies and the path resolves against the folder `claude` was started in. The entry
  stays in the documented form, and the rule is to start `claude` from the solution folder.
  `${workspaceFolder}` in `.vscode/mcp.json` really is absolute.
- **Reviewer agent with edit tools.** The first agent file had no `tools` line, so it inherited `Edit` and
  `Write`. Restricted to `Read, Grep, Glob, Bash, PowerShell`.

## Edited by hand

- The `[Description]` text on `quote_order`, to list the actual pricing rules (10% bulk discount at 10 or
  more copies, the two coupons, 6% tax), so a model can explain a quote without guessing.

## Verify

```powershell
dotnet build
dotnet test --solution Bookstore.slnx
```
