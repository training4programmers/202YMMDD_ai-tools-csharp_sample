# Demo 14: Hooks, Skills, Subagents, and MCP (ToolsApp / Colors)

Instructor demo paired with [Activity 14](../../activities/14-hooks-skills-subagents-and-mcp/). The agent
gets a new capability from a C# MCP server, extends that server, and the team's workflow is packaged as a
hook, a subagent, and a skill, all committed as files that both tools can use.

## Concept

- An **MCP server** gives the agent tools beyond the repository. `src/ToolsApp.Mcp` is a stdio server built
  with the `ModelContextProtocol` SDK; it reads the API's SQLite file through a **read-only** connection.
- Servers are registered per tool: **`.mcp.json`** (`mcpServers`) for Claude Code and **`.vscode/mcp.json`**
  (`servers`) for VS Code. (VS Code's guided flow now labels `.vscode/mcp.json` deprecated, but it still
  works.) Visual Studio reads the solution's `.mcp.json` first and doesn't expand Claude Code's path
  variable, so in Visual Studio add the server with **Add custom MCP server** and an absolute path; see
  the [Tools Review](TOOLS-REVIEW.md).
- Tool methods are plain C#: `[McpServerTool]` plus model-facing `[Description]` text, unit-tested directly.
- A **PostToolUse hook** runs `dotnet format` after edits; a **subagent** or custom agent writes tests in its
  own context; a **skill** packages a procedure the agent loads only when needed.
- Everything is a file in the repository, reviewed and shared like code; `.claude/skills/` serves both
  Claude Code and Copilot.

![MCP architecture: a host containing the model, the MCP client, and the server configuration connects over stdio to the local ToolsApp.Mcp server (find_colors, check_contrast, reading toolsapp.db read-only) and over HTTPS to the remote GitHub MCP server (api.githubcopilot.com/mcp).](../../diagrams/png/07-mcp-architecture-colors.png)

*Figure: The agent decides when to call a tool; you decide which servers it may use.*

**Design choice.** The server reads the same SQLite file the API writes, through `Microsoft.Data.Sqlite`
with `Mode=ReadOnly`, rather than calling the HTTP API. That keeps it a single process with no running API
to depend on, and least privilege is enforced by SQLite itself: no tool can write, whatever the model
asks. Because an AI client starts the server from a working directory you don't control, the database path
is resolved from the solution folder (`src/ToolsApp.Api/toolsapp.db`), and `ConnectionStrings__ToolsApp`
overrides it.

## How to run

From the course repo root, copy the starting point to a lab folder outside the repo (so the repo's own
AGENTS.md/CLAUDE.md don't leak into the session) and open it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 14 -Open
cd ~\labs\demo-14-hooks-skills-subagents-and-mcp
dotnet build
dotnet test --solution ToolsApp.slnx
```

Create the database the server reads, then stop the API:

```powershell
dotnet run --project src/ToolsApp.Api     # wait for "Now listening on: http://localhost:5090", then Ctrl+C
```

Also copy the recorded outcome, because later steps take files from it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 14 -Solution     # -> ~\labs\demo-14-hooks-skills-subagents-and-mcp-solution
```

Use Claude Code for the full trail; show the GitHub Copilot equivalents in VS Code (MCP, custom agent,
skill) as you go. Allow about 20 minutes, with the *Short on time* list at the top of the Demo trail
(the full trail takes 25 to 30). If the live run goes sideways, the solution copy is the recorded
outcome, with every configuration file in place.

## Demo trail

> **Short on time (about 20 minutes).** Run Steps 1 and 2 live: registering the server, the first tool
> call, and adding `check_contrast`. For Steps 3 to 5, walk the files in the solution copy instead
> (`scripts/format-changed.ps1` and its hook entry, both `test-writer` definitions, and
> `palette-contrast-report/SKILL.md`); copy in and run the skill only if time allows. Step 6 is a quick
> `git status` and test run.

**Code to open (in order):**

1. `src/ToolsApp.Mcp/Program.cs`: the generic host, logging to **stderr** (stdout carries the protocol),
   `AddMcpServer().WithStdioServerTransport().WithTools<ColorTools>()`.
2. `src/ToolsApp.Mcp/ToolsAppDatabase.cs`: `Mode = SqliteOpenMode.ReadOnly` and the path resolution.
3. `src/ToolsApp.Mcp/ColorCatalog.cs`: parameterized SQL, escaped `LIKE` wildcards, and an `McpException`
   that tells the model how to create a missing database.
4. `src/ToolsApp.Mcp/ColorTools.cs`: `[McpServerToolType]`, `[McpServerTool(Name = "find_colors",
   ReadOnly = true, ...)]`, and the `[Description]` text written for a model.
5. `tests/ToolsApp.Tests/ColorToolsTests.cs` and `SqliteColorCatalogTests.cs`: tools tested as plain
   methods, and the SQL tested against the schema the API really creates.

**Step 1: Register the server and watch the agent call it.** Copy both registrations from the solution:

```powershell
$sol = "$HOME\labs\demo-14-hooks-skills-subagents-and-mcp-solution"
Copy-Item "$sol\.mcp.json" .
New-Item -ItemType Directory -Force .vscode | Out-Null
Copy-Item "$sol\.vscode\mcp.json" .vscode\
```

Show the two files side by side: `mcpServers` versus `servers`, `${CLAUDE_PROJECT_DIR:-.}` versus
`${workspaceFolder}`, and `-c Release` so the running server doesn't lock the Debug build output. Be
honest about the first one: Claude Code sets `CLAUDE_PROJECT_DIR` only in the server's own environment,
so when it expands `args` the `.` fallback normally applies and the path is relative to the folder you
started `claude` in. `${workspaceFolder}` always resolves. Start `claude` from the lab folder (the
solution root), approve the project server when asked, and run `/mcp` to show it connected. Prompt
(Manual mode):

```text
Use the toolsapp MCP server to list the colors in the Sunset palette with their hex values.
```

Point out the permission prompt for `mcp__toolsapp__find_colors` and the JSON the tool returned. In VS
Code, run **MCP: List Servers**, start `toolsapp`, trust it, and enable its tools in **Configure Tools**.

**Step 2: Add a tool.** Prompt (accept-edits mode):

```text
Add a check_contrast tool to ToolsApp.Mcp with parameters foregroundHex and backgroundHex (#RGB or
#RRGGBB). Return the normalized hex values, the WCAG contrast ratio, the rating (AAA, AA, AALarge, Fail),
and whether it passes AA for body text. Reuse ColorMath for parsing and the math. Mark it read-only, write
the [Description] text for a model, and throw McpException naming the bad argument for invalid input.
Don't write tests yet. Run dotnet build when you're done.
```

Review it before reconnecting: is the description specific enough for a model, does it reuse `ColorMath`,
and how does it present the ratio? If it rounds, Lagoon on white (2.998:1) shows as "3" next to "Fail";
ask it to truncate the displayed ratio instead. Reconnect (`/mcp`, then reconnect `toolsapp`) and ask:

```text
Is the Lagoon color readable as body text on white? Use the toolsapp tools.
```

**Step 3: Have the agent write its own format hook.** Prompt (accept-edits mode):

```text
Add a PostToolUse hook to .claude/settings.json that runs dotnet format on the C# files that changed.
Put the logic in scripts/format-changed.ps1 (PowerShell 7): exit quickly when no .cs file changed, format
only the changed files with dotnet format ToolsApp.slnx --include, and exit 2 with a message on stderr
if formatting fails. Use the exec form (command "pwsh" plus args) with ${CLAUDE_PROJECT_DIR}. Also add
.github/hooks/dotnet-format.json so the Copilot CLI and cloud agent run the same script.
```

Claude Code asks before writing anything under `.claude/`: that folder is protected. Compare the result
with `scripts/format-changed.ps1` in the solution copy (the stamp file that keeps runs after reads fast,
`--include` with relative paths). Run `/hooks` to load it.

**Step 4: Delegate tests to a subagent.** Copy the agent definitions and show their front matter:

```powershell
New-Item -ItemType Directory -Force .claude\agents, .github\agents | Out-Null
Copy-Item "$sol\.claude\agents\test-writer.md" .claude\agents\
Copy-Item "$sol\.github\agents\test-writer.agent.md" .github\agents\
```

Point out `description` (how the agent decides to delegate), `tools`, and `model: sonnet` (a faster tier is
enough for test writing). Agent files reload on their own, except the first one in a new `agents` folder,
so restart with `claude --continue` to load this one, then prompt:

```text
Use the test-writer subagent to cover check_contrast, including invalid input and a ratio just under a
WCAG threshold.
```

The subagent works in its own context and returns a summary; the main conversation stays short. In VS
Code, pick **test-writer** from the agent picker to show the Copilot custom agent.

**Step 5: Package a procedure as a skill.**

```powershell
New-Item -ItemType Directory -Force .claude\skills | Out-Null
Copy-Item "$sol\.claude\skills\palette-contrast-report" .claude\skills\ -Recurse
```

Open `SKILL.md` (name, description as the trigger, numbered steps, what not to do). Claude Code doesn't
watch a skills folder that didn't exist when the session started, so run `/reload-skills` (or restart with
`claude --continue`) before you use it. Then run `/palette-contrast-report Ocean`. The agent calls
`find_colors` once and `check_contrast` six times and builds the table; Lagoon fails on white even for
large text. Run the same skill from Copilot chat in VS Code: it reads `.claude/skills/` too.

**Step 6: Review.**

```powershell
git status                            # .mcp.json, .vscode/, .claude/, .github/, scripts/, src/, tests/
dotnet test --solution ToolsApp.slnx
```

**Key teaching beat:** Each mechanism has one job. MCP adds a capability, the hook guarantees a behavior,
the subagent focuses a task in its own context, and the skill packages a procedure the agent loads only
when needed. Because they are all files in the repository, the next developer, and the next cloud agent
run, gets the same workflow after a `git pull`.

## Reference

- Concepts: [Module 07: Agentic Coding](../../docs/module-07-agentic-coding/):
  [Subagents, Hooks, and Skills](../../docs/module-07-agentic-coding/05-subagents-hooks-and-skills.md),
  [MCP Servers](../../docs/module-07-agentic-coding/06-mcp-servers.md)
- Tools: [Tools Review](TOOLS-REVIEW.md)
- Practice: [Activity 14](../../activities/14-hooks-skills-subagents-and-mcp/)
