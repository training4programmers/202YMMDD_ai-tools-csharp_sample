# Demo 21: Tools Review

A quick reference to the **AI-tool features this demo introduces**: having the agent verify packages
(and giving it real package data through the NuGet MCP server), requiring approval before any package
is installed, grounding the agent in current documentation with the Microsoft Learn MCP server, and
letting the agent see analyzer output so it fixes rather than suppresses. The concepts are tool-neutral
and come from Module 12; the GitHub Copilot and Claude Code specifics below are a snapshot. Menus and
settings move often, so the vendors' docs are the authority.

---

## Verifying a package from the agent

The agent can run the same checks you would, as long as you ask for the command and its raw output
rather than a summary. `dotnet package search` is part of the .NET SDK, so no extra tooling is needed.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Where** | Agent mode in Visual Studio 2026 or VS Code; the agent runs the command in a terminal | Any session; the agent runs it with its shell tool |
| **How** | Ask it to run `dotnet package search <id> --exact-match --source https://api.nuget.org/v3/index.json` and paste the output | Same prompt; allow `Bash(dotnet package search *)` (or the `PowerShell(...)` form) so it doesn't stop to ask each time |
| **Watch out** | An "alternative" package the agent proposes needs the same checks | Same; in the recorded run the agent offered a real but oversized library as an alternative |

```powershell
dotnet package search ToolsApp.ColorSpaces.Extensions --exact-match --source https://api.nuget.org/v3/index.json
# No results found.
dotnet package search Colourful --take 1 --verbosity detailed --source https://api.nuget.org/v3/index.json
# adds Project URL, Description, Vulnerable, and Deprecation columns
```

`--exact-match` lists every version but shows owners and downloads as `N/A`; drop it (with `--take 1`)
to see the publisher signals.

---

## The NuGet MCP server

Microsoft's **NuGet MCP server** gives an agent live package data: versions, vulnerabilities, and
updates, instead of whatever the model remembers. It runs from nuget.org with `dnx`, which ships with
the .NET 10 SDK.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Where** | Visual Studio 2026: built in; enable the server named "nuget" in the chat **Tools** menu. VS Code: add it to `.vscode/mcp.json` (or use the install button on the NuGet docs page) | `claude mcp add nuget -- dnx NuGet.Mcp.Server --source https://api.nuget.org/v3/index.json --yes` |
| **How** | Prompts such as "Fix my package vulnerabilities" or "What's the latest stable version of X?" | Same prompts; `/mcp` shows whether it connected |
| **Watch out** | It informs the decision; your package policy still decides | The Claude Code command is assembled from the server's documented configuration; confirm with `/mcp` |

```jsonc
// .vscode/mcp.json
{
  "servers": {
    "nuget": {
      "type": "stdio",
      "command": "dnx",
      "args": ["NuGet.Mcp.Server", "--source", "https://api.nuget.org/v3/index.json", "--yes"]
    }
  }
}
```

---

## Requiring approval before a package is installed

An agent that can run `dotnet add package` can install a slopsquatted package in one step. Keep that
one command behind a human approval, even if you let the agent run builds and tests freely.

> **GitHub Copilot:** Agent mode asks before running terminal commands that aren't auto-approved. If
> your VS Code settings use `chat.tools.terminal.autoApprove`, add a `false` entry for package
> installs so they always need confirmation:
>
> ```json
> "chat.tools.terminal.autoApprove": {
>   "/^dotnet (build|test|format)\\b/": true,
>   "/^dotnet (add package|package add)\\b/": false
> }
> ```
>
> That holds only at the default permission level: the **Allow all** level and **Autopilot** run
> every tool call without asking. In Visual Studio 2026, keep the confirmation prompt for terminal
> commands.
>
> **Claude Code:** `ask` rules always prompt, whatever the permission mode. On Windows, Claude Code
> can run commands through its PowerShell tool, which `Bash(...)` rules don't match, so write each rule
> both ways:
>
> ```json
> { "permissions": {
>     "allow": ["Bash(dotnet build *)", "Bash(dotnet test *)", "Bash(dotnet format *)", "Bash(dotnet package search *)",
>               "PowerShell(dotnet build *)", "PowerShell(dotnet test *)", "PowerShell(dotnet format *)",
>               "PowerShell(dotnet package search *)"],
>     "ask":   ["Bash(dotnet add package *)", "Bash(dotnet package add *)",
>               "PowerShell(dotnet add package *)", "PowerShell(dotnet package add *)"] } }
> ```
>
> Rules are evaluated deny, then ask, then allow; the first match wins.

---

## Grounding in current docs: the Microsoft Learn MCP server

When the agent isn't sure an API exists in .NET 10 (or exists the way it remembers), it can search
Microsoft Learn instead of guessing. The **Microsoft Learn MCP Server** is a free, public, remote server
at `https://learn.microsoft.com/api/mcp` (streamable HTTP, no authentication), with tools to search the
docs, fetch an article, and search code samples.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Where** | VS Code: `.vscode/mcp.json`; Visual Studio 2026: `<SolutionDir>\.mcp.json`. Both use `"servers"` | `claude mcp add --transport http microsoft-learn https://learn.microsoft.com/api/mcp` |
| **How** | Ask it to check the docs and link the page it used | Same; add `--scope project` to share the server through `.mcp.json` |
| **Watch out** | Make sure the server's tools are enabled in the Tools picker | Approve the project server on first use |

```text
Before you write the HSL parser, use the Microsoft Learn MCP server to check how CultureInfo.InvariantCulture
affects double.Parse, and link the page you used.
```

---

## Letting the agent see analyzer output

Gates only help if the agent reads them. With `TreatWarningsAsErrors` on, every finding appears as an
error in `dotnet build` output, which both tools' agents read after running the build. Two prompt
habits make the difference between fixes and suppressions: **"fix the code, not by suppressing,"** and
**"if a rule doesn't fit, stop and explain it before you suppress anything."** In the recorded run the
agent fixed every finding in code and then stopped to ask about `CA1707`.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Build output** | The agent reads the terminal output of `dotnet build`; in VS Code, `#read/problems` adds the Problems panel | The agent reads the output of its own shell tool |
| **Standing rule** | `.github/copilot-instructions.md`: "zero warnings; never add #pragma, SuppressMessage, or NoWarn without asking" | `CLAUDE.md`, same rule; a `PostToolUse` hook can run `dotnet format` after each edit (Module 07) |
| **Watch out** | Suggestion-severity IDE hints never reach a terminal build | Same; raise rules you care about to `warning` |

---

## Summary

- Ask the agent for the **exact `dotnet package search` command and its output**; verify any
  **alternative** it proposes the same way.
- The **NuGet MCP server** (`dnx NuGet.Mcp.Server`, built into Visual Studio 2026) gives the agent real
  package and vulnerability data.
- Keep **`dotnet add package` behind an approval**: a `false` auto-approve entry in VS Code (at the
  default permission level), the
  confirmation prompt in Visual Studio 2026, an **`ask`** rule in Claude Code.
- The **Microsoft Learn MCP Server** (`https://learn.microsoft.com/api/mcp`) grounds the agent in
  current docs; ask for **links**.
- With the gates on, the agent sees every finding as a build error; tell it to **fix rather than
  suppress**, and to **stop and ask** when a rule doesn't fit.
