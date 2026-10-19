# Subagents, Hooks, and Skills: Packaging Repeatable Workflows

By now you have prompts that work: the review checklist you paste every time, the "run the gates and don't
touch the tests" rules, the steps for adding an endpoint the team's way. Typing them again in every
session is slow, and every developer types them slightly differently. This chapter shows three ways to
package that knowledge so it runs the same way for everyone on the team.

- **Subagents** are specialized helpers with their own instructions, tools, and context window.
- **Hooks** are commands that run automatically at fixed points in the agent's loop, whether or not the
  model remembers to.
- **Skills** are folders of instructions (and optional scripts) that the agent loads only when a task
  needs them.

All three are plain files you commit to the repository, so they are reviewed, versioned, and shared like
code. The chapter ends with how to package them for a team, including plugins.

---

## Which mechanism for which job

| You want to... | Use | Why |
|----------------|-----|-----|
| Give guidance that applies to every task | Instruction files (module 06) | Always loaded; cheap; advisory |
| Teach a procedure used now and then | A **skill** | Loaded on demand, so it doesn't fill context when unused |
| Hand off a self-contained job with its own focus | A **subagent** or custom agent | Separate context, its own tools and model |
| Guarantee something happens (or never happens) | A **hook** | Deterministic: runs every time, the model can't skip it |
| Reach a system outside the repo | An **MCP server** (chapter 06) | Adds new tools |
| Ship several of the above together | A **plugin** | One install for the whole bundle |

The key distinction is **advisory versus enforced**. Instructions, skills, and agent descriptions ask the
model to behave a certain way; hooks and permission rules make it happen.

---

## Subagents

A **subagent** is a separate agent that the main agent can delegate to. It starts with a fresh **context
window** (the text the model can see), its own system prompt, its own allowed tools, and optionally a
different model. It does its job and returns a summary, so the main conversation stays focused instead of
filling with every file the helper read.

Good subagent jobs are self-contained and have a clear output: "write tests for this method and report what
you covered", "review the diff and list problems", "find every place we call this API".

### Subagents in Claude Code

Subagents are Markdown files with YAML front matter in `.claude/agents/` (project, committed) or
`~/.claude/agents/` (personal).

```markdown
---
name: test-writer
description: Writes and runs xUnit v3 tests for ToolsApp code. Use it after a domain method, endpoint, or MCP tool is added or changed, or when asked for more test coverage. It only adds or changes files under tests/.
tools: Read, Grep, Glob, Edit, Write, Bash, PowerShell
model: sonnet
---

You write focused, readable xUnit v3 tests for the ToolsApp solution.
Read the code under test and the existing tests next to it, and follow their style ...
```

- `name` and `description` are required. The **description** is how Claude decides when to delegate, so
  say what the agent does and when to use it.
- `tools` restricts what it can use; omit it to inherit everything. A reviewer that should never edit gets
  `tools: Read, Grep, Glob, Bash, PowerShell`.
- `model` picks a tier (`sonnet`, `opus`, `haiku`, `fable`, or `inherit`). A fast, low-cost tier is often
  enough for test writing or searching.
- Other optional fields include `permissionMode`, `skills`, `mcpServers`, `hooks`, and `isolation:
  worktree` (run in a temporary git worktree).

Claude delegates automatically when a task matches a description, or explicitly when you ask ("use the
test-writer subagent to cover check_contrast"), @-mention the agent, or start a whole session as it with
`claude --agent test-writer`. Since version 2.1.198, the `/agents` command no longer opens a creation
wizard; ask Claude to write the file, or create it yourself. Agent files you add or edit during a session
are picked up without a restart, except the first file in a new `agents` folder: restart (for example
with `claude --continue`) to load that one.

### Custom agents in GitHub Copilot

Copilot's equivalent is a **custom agent**: a persona with its own instructions and tools that you select
for a session, or that other agents can call.

```markdown
---
name: test-writer
description: Writes and runs xUnit v3 tests for ToolsApp code. Use it after a domain method, endpoint, or MCP tool is added or changed.
---

You write focused, readable xUnit v3 tests for the ToolsApp solution.
...
```

> **GitHub Copilot:** Save it as `.github/agents/test-writer.agent.md`. Visual Studio 2026 (18.4 and later)
> and VS Code read this folder; `description` is required, and `name`, `tools`, and `model` are optional.
> Tool names differ between Visual Studio and VS Code, so leave `tools` out of a file both IDEs share. The
> personal folders differ too: `%USERPROFILE%\.github\agents` for Visual Studio, `~/.copilot/agents` for
> VS Code. Pick the agent from the agent picker, use `/agent` or `--agent` in the Copilot CLI, or choose it
> when you assign an issue to the cloud agent. In VS Code's Local harness, agents can also run other agents
> as subagents.
>
> **Claude Code:** Save it as `.claude/agents/test-writer.md`. VS Code also reads Claude-format agents from
> `.claude/agents`, so a VS Code user may see the agent twice if both files exist; that's harmless.

---

## Hooks

A **hook** is a command the agent host runs automatically at a lifecycle event, such as before a tool
runs, after a tool runs, or when the agent is about to stop. Hooks are **deterministic**: they run every
time the event fires, whatever the model thinks. That makes them the right tool for rules that must hold,
like "format every edited C# file" or "never edit an existing test".

### Hooks in Claude Code

Claude Code fires many events (the [hooks reference](https://code.claude.com/docs/en/hooks) lists them
all). The ones you will use most:

| Event | Fires | Typical use |
|-------|-------|-------------|
| `SessionStart` | When a session starts or resumes | Print environment facts, check the SDK version |
| `UserPromptSubmit` | Before your prompt reaches the model | Add context, block prompts containing secrets |
| `PreToolUse` | Before a tool runs; **can block it** | Guard paths, block dangerous commands |
| `PostToolUse` | After a tool succeeds | Format or lint the file just edited |
| `Stop` | When the agent is about to finish; **can make it continue** | Require the tests to pass first |
| `SubagentStart`, `SubagentStop` | Around subagent work | Logging, checks on subagent results |
| `PreCompact`, `SessionEnd` | Before compaction, at the end | Save notes, clean up |

Hooks live in the `hooks` section of a settings file. A **matcher** limits a hook to certain tools (`Edit|Write`
matches either), and each handler is a command (or an HTTP call, MCP tool, prompt, or agent):

```json
{
  "hooks": {
    "PostToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "pwsh",
            "args": ["-NoProfile", "-File", "${CLAUDE_PROJECT_DIR}/scripts/format-changed.ps1"],
            "timeout": 120
          }
        ]
      }
    ]
  }
}
```

How a hook talks to Claude Code:

- **Input**: JSON on stdin, including `tool_name`, `tool_input` (for `Edit` and `Write`,
  `tool_input.file_path` is the absolute path), and `cwd`. `CLAUDE_PROJECT_DIR` holds the project root.
- **Exit code 0**: carry on. **Exit code 2** blocks the action on `PreToolUse`, `UserPromptSubmit`, and
  `Stop`, and the text on stderr goes to Claude as the reason. On `PostToolUse` the tool has already run,
  so exit code 2 only feeds stderr back to Claude. Other exit codes are reported but don't block.
- **On Windows**, the *exec form* above (`command` plus `args`) launches `pwsh` directly, with no shell in
  between, and `${CLAUDE_PROJECT_DIR}` is substituted into each argument.
- Run `/hooks` to see what is configured.

### A format-after-edit hook

The script the lab solutions use formats the C# files that changed since the hook last ran. It ignores the
JSON payload and asks git instead, so the same script works for Claude Code and Copilot:

```powershell
$null = [Console]::In.ReadToEnd()   # drain the payload the agent wrote to stdin
$root = Split-Path -Parent $PSScriptRoot
Set-Location $root

$stamp = Join-Path (git rev-parse --git-dir) 'format-hook.stamp'
$since = (Test-Path $stamp) ? (Get-Item $stamp).LastWriteTimeUtc : [datetime]::MinValue
$changed = @(git diff --name-only --relative --diff-filter=d HEAD -- '*.cs') +
    @(git ls-files --others --exclude-standard -- '*.cs')
$files = @($changed | Sort-Object -Unique | Where-Object { (Get-Item $_).LastWriteTimeUtc -gt $since })
if ($files.Count -eq 0) { exit 0 }   # nothing new: return in well under a second

dotnet format ToolsApp.slnx --include @files --verbosity quiet
New-Item -ItemType File -Path $stamp -Force | Out-Null
```

Full `dotnet format` loads the solution, which takes a few seconds per run; the stamp keeps calls after
reads and searches fast. If you only need whitespace fixes, `dotnet format whitespace --folder --include
<file>` skips the solution load. Chapter 04 shows the matching `PreToolUse` guard that keeps existing tests
read-only.

### Hooks in GitHub Copilot

> **GitHub Copilot:** The Copilot CLI and the Copilot cloud agent read hooks from `.github/hooks/*.json`
> (see the [Copilot CLI hooks reference](https://docs.github.com/en/copilot/reference/copilot-cli-reference/cli-hooks-reference)).
> The format has a `version`, camelCase event names, and per-shell commands. On `preToolUse` and
> `postToolUse`, an optional `matcher` is a regex tested against the whole tool name; the file tools are
> `edit` and `create`, and leaving `matcher` out runs the hook after every tool:
>
> ```json
> {
>   "version": 1,
>   "hooks": {
>     "postToolUse": [
>       {
>         "type": "command",
>         "matcher": "edit|create",
>         "bash": "pwsh -NoProfile -File scripts/format-changed.ps1",
>         "powershell": "pwsh -NoProfile -File scripts/format-changed.ps1",
>         "timeoutSec": 120
>       }
>     ]
>   }
> }
> ```
>
> A `preToolUse` hook denies a call by writing `{"permissionDecision": "deny", "permissionDecisionReason":
> "..."}` to stdout. The cloud agent runs only the `bash` (or `command`) entries, on Linux. VS Code also
> reads `.github/hooks/*.json` (agent hooks are in preview there; event names and fields can differ by
> harness, so check the VS Code hooks reference), and can read Claude-format hooks from
> `.claude/settings.json` when `chat.useClaudeHooks` is on. Hook support in Visual Studio isn't documented
> at the time of writing; rely on the build gates and CI there.
>
> **Claude Code:** Hooks go in `.claude/settings.json` (shared), `.claude/settings.local.json` (yours), or
> `~/.claude/settings.json`. A subagent can also carry its own hooks in its front matter, scoped to that
> subagent.

### Hooks are code that runs on your machine

A hook runs with your user account's permissions every time the event fires. Review hook scripts in pull
requests as carefully as production code, keep them short, and never point a hook at a script downloaded
from the internet at run time.

---

## Skills

A **skill** is a folder containing a `SKILL.md` file: a name, a description, and instructions, plus any
supporting files (scripts, templates, reference notes). The agent sees only the names and descriptions
until a task matches one, then loads that skill's full instructions. That makes skills ideal for procedures
you use sometimes but don't want in every prompt.

```markdown
---
name: palette-contrast-report
description: Builds a WCAG contrast report for one ToolsApp palette, using the toolsapp MCP server's find_colors and check_contrast tools. Use it when someone asks which colors in a palette are readable on white or on black.
---

# Palette contrast report

1. Call `find_colors` with the palette name. Keep only colors whose `palette` field equals that name.
2. For each color, call `check_contrast` on `#FFFFFF` and on `#000000`.
3. Reply with a Markdown table: Color | Hex | On white | On black.
...
```

Skills follow an [open standard](https://agentskills.io/specification): `name` is required, at most 64 lowercase letters, digits,
and hyphens, and must match the folder name; `description` is required and at most 1,024 characters. You
can invoke a skill by name (`/palette-contrast-report Ocean`) or let the agent pick it from the description.

The same folder serves both tool families:

> **GitHub Copilot:** Copilot reads skills from `.github/skills/`, `.claude/skills/`, and `.agents/skills/`
> in the cloud agent, code review, the Copilot CLI, VS Code, and Visual Studio 2026 (18.5 and later).
> Personal skills go in `~/.copilot/skills/` (or the `.claude` and `.agents` equivalents).
>
> **Claude Code:** Reads `.claude/skills/<name>/SKILL.md` and `~/.claude/skills/<name>/SKILL.md`; `/skills`
> lists them. Changes to skills are picked up during a session, but a top-level skills folder that didn't
> exist when the session started isn't watched yet: run `/reload-skills` (or restart) after creating it.
> Claude-specific optional fields include `allowed-tools`, `disable-model-invocation`, and
> `context: fork` (run in a subagent). Older custom slash commands in `.claude/commands/*.md` still work and
> behave like skills; `$ARGUMENTS` receives what you type after the command.

Writing skills that work:

- **The description is the trigger.** Say what the skill does and when to use it, in the words a teammate
  would use when asking.
- **Write steps, not essays.** Numbered, checkable steps; name the tools and files.
- **Say what not to do.** "Don't change any files", "never round a failing ratio up".
- **Keep one job per skill.** Two jobs means two skills.

---

## Packaging workflows for the team

Commit the configuration so every developer and every cloud run gets the same behavior.

| | GitHub Copilot | Claude Code |
|---|---|---|
| Always-on instructions | `.github/copilot-instructions.md`, `AGENTS.md` | `CLAUDE.md` (importing `@AGENTS.md`) |
| Path-scoped rules | `.github/instructions/*.instructions.md` (`applyTo`) | `.claude/rules/*.md` (`paths:`) |
| Skills | `.github/skills/` or **`.claude/skills/`** | **`.claude/skills/`** |
| Agents | `.github/agents/*.agent.md` | `.claude/agents/*.md` |
| Hooks | `.github/hooks/*.json` | `.claude/settings.json` (`hooks`) |
| Permissions | VS Code settings (`.vscode/settings.json`) | `.claude/settings.json` (`permissions`) |
| MCP servers | `.vscode/mcp.json` | `.mcp.json` |
| Personal, not committed | User settings folders | `.claude/settings.local.json`, `CLAUDE.local.md` |

Practical rules:

- **Share one skills folder.** `.claude/skills/` is read by both tools, so one copy serves the whole team.
- **Keep scripts beside the config** (for example `scripts/format-changed.ps1`) and call them with paths
  relative to the project root.
- **Treat these files as code.** They change what the agent does on everyone's machine. Consider a
  CODEOWNERS entry for `.claude/`, `.github/agents/`, `.github/hooks/`, and `.github/skills/`.
- **Start small.** One hook, one agent, one skill that solve a real annoyance beat a large framework
  nobody maintains.

### Plugins

When a bundle of commands, agents, skills, hooks, and MCP servers is useful across many repositories,
package it as a **plugin** so others install it once.

> **GitHub Copilot:** The Copilot CLI installs plugins from marketplaces, for example
> `/plugin marketplace add microsoft/upgrade-agent-plugins`. VS Code also supports agent plugins; see its
> documentation for the current format. Organization-wide custom agents can live in the organization's
> `.github` or `.github-private` repository.
>
> **Claude Code:** A plugin is a folder with a `.claude-plugin/plugin.json` manifest. Add a marketplace with
> `/plugin marketplace add <owner>/<repo>`, then install with `/plugin` or
> `claude plugin install <name>@<marketplace>`. The official `claude-plugins-official` marketplace is added
> automatically.

---

## Summary

- **Subagents** (Claude Code, `.claude/agents/*.md`) and **custom agents** (Copilot,
  `.github/agents/*.agent.md`) package a focused job with its own instructions, tools, model, and context.
- **Hooks** run commands at lifecycle events and are **deterministic**; `PreToolUse` can block, and
  `PostToolUse` is ideal for formatting with `dotnet format`.
- Claude Code hooks live in **`.claude/settings.json`**; Copilot CLI and cloud agent hooks live in
  **`.github/hooks/*.json`**; VS Code agent hooks are in **preview**, and Visual Studio's hook support isn't
  documented.
- **Skills** are on-demand procedures in `SKILL.md`; one **`.claude/skills/`** folder serves Claude Code and
  Copilot alike.
- Commit these files, **review them like code**, and use **plugins** to share bundles across repositories.
- Practice it in [Demo 14](../../demos/14-hooks-skills-subagents-and-mcp/) and
  [Activity 14](../../activities/14-hooks-skills-subagents-and-mcp/).
