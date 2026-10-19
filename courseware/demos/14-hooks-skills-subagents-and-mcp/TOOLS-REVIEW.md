# Demo 14: Tools Review

This demo introduces **MCP server registration**, **approving and reconnecting MCP tools**, **subagents
and custom agents**, **skills**, and how to **package all of it** for a team. (Hooks themselves were
introduced in Demo 13.) The concepts are tool-neutral; the file names and commands are GitHub Copilot's
and Claude Code's at the time of writing. Configuration formats in this area change often, so the vendors'
documentation is the authority.

---

## Registering an MCP server

Each tool reads a JSON file listing the servers it may start or connect to. The two formats differ in one
key, which is the most common reason a server doesn't appear.

| | GitHub Copilot | Claude Code |
|---|---|---|
| File | `.vscode/mcp.json` (VS Code); Visual Studio reads `<SolutionDir>\.mcp.json` before `.vscode\mcp.json` | `.mcp.json` at the project root |
| Top-level key | `servers` | `mcpServers` |
| Project path variable | `${workspaceFolder}` (always the opened folder) | `${CLAUDE_PROJECT_DIR:-.}` in `args`; the `.` fallback normally applies, so start `claude` from the solution root |
| Add from a command | `/mcp add` in the Copilot CLI | `claude mcp add --scope project <name> -- <command>` |
| Watch out | Visual Studio tools are off until you enable them in **Tools** | A file with `servers` instead of `mcpServers` is ignored |

Why the fallback: Claude Code sets `CLAUDE_PROJECT_DIR` in the server's environment, not in its own, and it
expands `args` from its own environment. The server can still read the variable at run time.

> **Currency note:** VS Code still reads `.vscode/mcp.json`, but its guided add-server flow now labels that
> file deprecated, kept for compatibility. The team-sharing alternative it offers, a workspace-root
> `.mcp.json` in the `mcpServers` format, needs the **experimental** `chat.mcp.workspaceRootConfig.enabled`
> setting. The demo keeps `.vscode/mcp.json` so the Claude Code and VS Code files stay separate.
>
> **Visual Studio 2026:** it reads the solution folder's `.mcp.json` (Claude Code's file) before
> `.vscode\mcp.json`, documents only the `servers` key, and doesn't expand `${CLAUDE_PROJECT_DIR:-.}`, so
> the `toolsapp` entry may not start there. Add the server with **Tools** > **+** > **Add custom MCP
> server** instead: Destination **Global**, a new Server ID such as `toolsapp-vs`, Type stdio, and
> `dotnet run --project <absolute path>\src\ToolsApp.Mcp -c Release`. (The **Solution** destination
> writes a `.mcp.json` into the solution folder, Claude Code's file.)

```json
{
  "mcpServers": {
    "toolsapp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "${CLAUDE_PROJECT_DIR:-.}/src/ToolsApp.Mcp", "-c", "Release"]
    }
  }
}
```

---

## Approving, checking, and reconnecting MCP tools

A server you just registered isn't trusted yet, and a server you just changed isn't reloaded yet.

> **GitHub Copilot:** In VS Code, start the server from **MCP: List Servers** (or the prompt in the chat),
> confirm you trust it, and switch its tools on in **Configure Tools**; the server's output log is one of
> the actions in **MCP: List Servers**. In Visual Studio 2026, enable tools in the agent-mode **Tools**
> picker and approve calls with the **Confirm** dropdown; from 18.7 it asks you to trust a server again when
> its tools change.
>
> **Claude Code:** The first session that uses a project server asks you to approve it (`claude mcp
> reset-project-choices` clears those answers). `/mcp` shows status, and `/mcp reconnect toolsapp` restarts a
> server after you change its code. Pre-approve read-only tools by name:
> `"permissions": { "allow": ["mcp__toolsapp__find_colors"] }`.

---

## Subagents and custom agents

A focused helper with its own instructions, tools, and model. Claude Code runs a subagent in a separate
context and gets a summary back; Copilot's custom agents are personas you select (and, in VS Code, can be
run as subagents).

| | GitHub Copilot | Claude Code |
|---|---|---|
| File | `.github/agents/test-writer.agent.md` | `.claude/agents/test-writer.md` |
| Required front matter | `description` | `name`, `description` |
| Useful fields | `name`, `tools`, `model` (VS Code adds `handoffs` and more) | `tools`, `model`, `permissionMode`, `skills`, `isolation: worktree` |
| How to use | Agent picker; `/agent` or `--agent` in the CLI; choose one when assigning an issue | Automatic from the description; "use the test-writer subagent"; `@`-mention; `claude --agent` |
| Watch out | Tool names differ between Visual Studio and VS Code; personal folders differ too | `/agents` no longer has a creation wizard; edit the file or ask Claude. Agent files reload in a running session, except the first one in a new `agents` folder: restart (`claude --continue`) |

---

## Skills

A `SKILL.md` with a name and a description, loaded only when a task matches it or when you invoke it by
name. One folder works for both tool families.

| | GitHub Copilot | Claude Code |
|---|---|---|
| Project folders | `.github/skills/`, `.claude/skills/`, `.agents/skills/` | `.claude/skills/` |
| Where it works | Cloud agent, code review, CLI, VS Code, Visual Studio 18.5 and later | CLI, VS Code extension, desktop, GitHub Action |
| Invoke | `/palette-contrast-report` or automatically | `/palette-contrast-report Ocean` or automatically |
| List | Type `/` in the chat input | `/skills` |
| Watch out | `name` must match the folder: lowercase letters, digits, and hyphens | Claude-only fields such as `allowed-tools` are ignored by other tools. A skills folder created after the session started isn't watched yet: run `/reload-skills` (or restart) |

---

## Packaging it for the team

After this demo the lab contains everything a teammate needs, as reviewable files:

```text
.mcp.json                                   Claude Code MCP servers
.vscode/mcp.json                            VS Code MCP servers
.claude/settings.json                       permissions and hooks (Claude Code)
.github/hooks/dotnet-format.json            hooks (Copilot CLI and cloud agent)
scripts/format-changed.ps1                  the script both hooks call
.claude/agents/test-writer.md               subagent (Claude Code)
.github/agents/test-writer.agent.md         custom agent (Copilot)
.claude/skills/palette-contrast-report/     skill (both tools)
```

> **GitHub Copilot:** Organization-wide custom agents can live in the organization's `.github` or
> `.github-private` repository, and the Copilot CLI installs plugins from marketplaces.
>
> **Claude Code:** Bundle commands, agents, skills, hooks, and MCP servers as a plugin
> (`.claude-plugin/plugin.json`) and share it through a marketplace (`/plugin marketplace add`). Keep
> personal overrides in `.claude/settings.local.json`, which isn't committed.

---

## Summary

- Register MCP servers in **`.mcp.json`** (`mcpServers`) and **`.vscode/mcp.json`** (`servers`).
- **Trust, enable, and reconnect** servers deliberately: `/mcp` in Claude Code, **MCP: List Servers** and
  **Configure Tools** in VS Code, the **Tools** picker in Visual Studio.
- **Subagents** (`.claude/agents`) and **custom agents** (`.github/agents`) package a focused job; the
  description is what triggers them.
- **Skills** in **`.claude/skills/`** work in both Claude Code and Copilot.
- Commit the configuration so the whole team, and every cloud run, shares the same workflow.
