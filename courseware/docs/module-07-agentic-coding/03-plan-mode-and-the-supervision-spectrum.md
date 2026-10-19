# Plan Mode and the Supervision Spectrum

An agent can change twenty files before you finish reading its first message. Supervision is how you
decide **where** you step in: before every action, before commands only, once at the plan, or only at the
end when you review the diff. None of these is the right answer for every task. The skill is matching the
level of supervision to the risk of the task and the strength of your safety net.

This chapter covers **plan mode** (the agent researches and proposes, but doesn't edit, until you approve),
the **permission modes** each tool offers from per-action prompts to autopilot, **allow and deny rules** that
fine-tune those modes, how to choose a level by risk, and the **checkpoints** and git habits that make any
level recoverable.

---

## Why plan first

Changing a plan costs a sentence. Changing an implementation costs a review, a revert, and another run. In
plan mode the agent reads the code, may run read-only commands to explore, and writes a proposal, but it
doesn't edit your source until you approve.

A good plan for a C# change answers these questions:

- **Files**: which files it will create or change, and which it will leave alone.
- **Approach**: the design in a few bullets: where the logic lives, the types, the endpoint shape.
- **Tests**: which tests it will add, at which level (unit, `WebApplicationFactory`).
- **Risks and unknowns**: anything it isn't sure about, stated as a question.
- **Out of scope**: what it won't do.

### Reviewing a plan

Read the plan the way you would read a design note from a new teammate. The questions that catch the most
problems in .NET work:

1. Does it put logic in the right project (domain logic in `*.Domain`, HTTP concerns in `*.Api`)?
2. Does it follow the patterns already in the solution, or invent new ones (a new mediator library, a
   repository layer the solution doesn't use, Swagger UI the task didn't ask for)?
3. Does it add packages? Is each one real, current, and necessary ([module 12](../module-12-csharp-pitfalls-and-verification/))?
4. Are the tests at the right level, and do they cover the error cases in the acceptance criteria?
5. What happens on the unhappy path: concurrency, invalid input, culture, cancellation?

Here is an **illustrative** excerpt from a first plan for the Demo 12 export task, and the edits a reviewer
made before approving it:

```text
Plan (draft):
1. Add PaletteExporter to ToolsApp.Domain with ToCss, ToJson, ToGpl.
2. Parse the format with Enum.TryParse<PaletteExportFormat>(format, ignoreCase: true).
3. Build GPL lines with Environment.NewLine.
4. Add Swashbuckle so designers can try the endpoint in a browser.
5. Add unit tests for the exporter and API tests for the endpoint.
```

```text
Reviewer edits:
2. Enum.TryParse also accepts "1" and "99". Use an explicit lookup of css, json, gpl.
3. The GPL format requires LF. Use "\n", never Environment.NewLine.
4. Remove: no new packages (TASK.md). The .http file is enough.
```

Each edit is a bug that never got written.

---

## Plan mode in each tool

> **GitHub Copilot:** Visual Studio 2026 and VS Code both include a built-in **Plan** agent: choose it in
> the chat's agent or mode picker, describe the task, and refine the plan in conversation before switching
> to Agent to implement it. In VS Code sessions that run on the Copilot harness, `/plan <task>` starts
> planning. In the Copilot CLI, press `Shift+Tab` to cycle to plan mode, or type `/plan <task>`; Copilot asks
> clarifying questions and builds a structured plan before writing code.
>
> **Claude Code:** Press `Shift+Tab` to cycle to plan mode (the status bar shows `plan mode on`), prefix one
> prompt with `/plan`, or start with `claude --permission-mode plan`. When the plan is ready, Claude asks
> how to proceed: **Yes, and use auto mode** (or **Yes, auto-accept edits** where auto mode isn't
> available), **Yes, manually approve edits**, or **No, keep planning**. Press `Ctrl+G` to open the proposed
> plan in your editor and change it directly before approving.

You can make plan mode the default for a project's terminal sessions with a project setting:

```json
{
  "permissions": {
    "defaultMode": "plan"
  }
}
```

In Claude Code that goes in `.claude/settings.json`. (The VS Code extension uses its own
`claudeCode.initialPermissionMode` user setting for new conversations.)

---

## The supervision spectrum

Think of supervision as a dial that moves the point where you approve: from every single action, to the
plan, to only the finished result.

![Four supervision levels from left to right: ask before every action, accept edits but ask for commands, plan mode read-only until the plan is approved, and auto or autopilot where you review the result. Under each, the Claude Code mode name, the GitHub Copilot equivalent, and the kind of work it suits. A banner notes that deny rules, hooks, git diff, and tests apply at every level.](../../diagrams/png/07-supervision-spectrum.png)

*Figure: The supervision spectrum. Move right as the risk drops and your verify loop gets stronger.*

| Level | You approve | Typical use |
|-------|-------------|-------------|
| Ask before every action | Each edit and each command | Unfamiliar code, production configuration, anything near secrets |
| Accept edits, ask for commands | Commands only; edits land and you review them | Everyday work on a branch you can diff and reset |
| Plan mode | The plan, before any edit; then a level you pick | Multi-file features, anything you'd sketch first |
| Auto / autopilot | The final diff | Well-tested code, a strong verify loop, an isolated environment |

Plan mode isn't really a fourth level of autonomy; it is a gate in front of the others. Approving the plan
switches the session to whichever level you choose for the implementation.

---

## Claude Code permission modes

The [permission modes reference](https://code.claude.com/docs/en/permission-modes) has the full details;
this table is the short version.

| Mode | Runs without asking | Use it for |
|------|---------------------|------------|
| `default` (shown as **Manual**) | Reads only | Reviewing every action yourself |
| `acceptEdits` | Reads, file edits, and common file-system commands | Iterating on code you're reviewing |
| `plan` | Reads (plus classifier-approved commands when auto mode is available) | Exploring and proposing before editing |
| `auto` | Everything, with a background classifier checking each action | Long tasks where you trust the direction |
| `dontAsk` | Only pre-approved tools; anything that would prompt is denied | Locked-down CI and scripts |
| `bypassPermissions` | Everything | Isolated containers and VMs only |

Points that trip people up:

- **Auto mode is now the default starting mode** for interactive terminal and VS Code sessions (Claude Code
  2.1.283 and later). A separate classifier model reviews actions instead of you. It reduces prompts; it
  does not guarantee safety. Administrators can switch it off with `permissions.disableAutoMode`, so don't
  assume every student sees it.
- `Shift+Tab` cycles the modes. From auto, the first press goes to Manual, then `acceptEdits`, then `plan`.
- `auto` and `bypassPermissions` are ignored when set as `defaultMode` in a project's `.claude/settings.json`;
  they belong in your user settings or on the command line.
- Writes to **protected paths**, such as the repository's `.git` folder and Claude Code's own `.claude`
  configuration, are never auto-approved outside `bypassPermissions`.

---

## Copilot approvals

> **GitHub Copilot:** In **VS Code**, the **permissions** dropdown in the chat input sets the level for the
> session: **Manual permissions** (the default; your approval settings apply), **Assisted permissions**
> (experimental; a separate model judges each tool call), or **Allow all** (no confirmations). **Autopilot**
> (preview) is an agent mode chosen from the mode picker: it approves tools, retries on errors, and answers
> its own blocking questions until it decides the task is done. The first time you pick Allow all or
> Autopilot, VS Code asks you to confirm.
>
> In **Visual Studio 2026**, approve each tool call from the **Confirm** dropdown, for this session, this
> solution, or always. Reset those choices under Tools > Options > GitHub > Copilot > Tools.
>
> In the **Copilot CLI**, `/permissions` switches between `default`, `assisted`, and `allow-all`, and
> autopilot is a mode you reach with `Shift+Tab`.

VS Code also has fine-grained settings that work like allow and deny lists (see
[Manage approvals and permissions](https://code.visualstudio.com/docs/agents/approvals)):

```jsonc
{
  // Terminal commands: true auto-approves, false always asks. /regex/ patterns are allowed.
  "chat.tools.terminal.autoApprove": {
    "/^dotnet (build|test|format)\\b/": true,
    "git push": false
  },
  // File edits: glob patterns. true auto-approves, false asks first; the last matching pattern decides.
  "chat.tools.edits.autoApprove": {
    "**/*": true,
    "**/.env": false,
    "**/tests/**": false
  }
}
```

A `false` terminal rule asks for approval; it doesn't block the command. Blocking needs a hook
(chapter 05).

---

## Allow and deny rules in Claude Code

Modes set the baseline; **permission rules** adjust it per tool and per pattern. They live in
`.claude/settings.json` (shared with the team), `.claude/settings.local.json` (yours, not committed), or
`~/.claude/settings.json` (all your projects), and `/permissions` shows and edits them.

```json
{
  "permissions": {
    "allow": [
      "Bash(dotnet build *)",
      "Bash(dotnet test *)",
      "PowerShell(dotnet build *)",
      "PowerShell(dotnet test *)"
    ],
    "ask": [
      "Bash(git push *)",
      "PowerShell(git push *)"
    ],
    "deny": [
      "Read(./.env)",
      "Read(./secrets/**)",
      "Edit(/tests/**)"
    ]
  }
}
```

How they are evaluated:

- **Deny, then ask, then allow**; the first match wins. Deny rules apply in every mode, including
  `bypassPermissions`.
- `Bash(...)` rules don't cover the `PowerShell` tool. On Windows without Git Bash, Claude Code runs
  commands through PowerShell, so add both forms.
- In project settings, `Edit(/tests/**)` is anchored at the project folder; `Read(./.env)` is relative to
  the current directory. File rules are written as `Edit(...)` and `Read(...)`; an `Edit` rule covers every
  file-writing tool.
- Settings precedence, highest first: managed (organization) settings, command-line `--settings`,
  `.claude/settings.local.json`, `.claude/settings.json`, then `~/.claude/settings.json`.

A deny rule is a hard stop, which makes it blunt: `Edit(/tests/**)` also stops the agent from **adding** a
test. Chapter 04 shows a hook that protects existing tests but allows new ones.

---

## Choosing a level by risk

Ask four questions about the task:

| Question | Lower risk | Higher risk |
|----------|------------|-------------|
| Can I undo it? | Code in a lab or feature branch | Database migrations, deployments, pushes, deletions |
| What can it reach? | Source files only | Secrets, production data, network services, shared infrastructure |
| How strong are the checks? | Good tests, strict analyzers, CI | Little or no test coverage |
| How well do I know the code? | Code I wrote or reviewed | A legacy area nobody understands |

The more answers fall in the right-hand column, the further left on the spectrum you should be. A sensible
default for the course labs: **plan mode for the design, accept-edits for the implementation, approvals for
anything that leaves the working folder** (pushes, network calls, package installs).

Two rules of thumb:

- **Autonomy needs a verify loop.** Auto mode on a project with no tests just produces unverified code
  faster.
- **Autonomy needs isolation.** The more you let the agent do unattended, the more you want it in a lab
  folder, a git worktree, a container, or a cloud runner, not your only working copy.

---

## Checkpoints and rollbacks

Every level of supervision assumes you can undo. You have three layers of undo.

### Git: the checkpoint that always works

Commit (or stash) before you start, and the agent's entire effect is one `git diff` away.

```powershell
git status                    # what changed
git diff --stat               # which files, how much
git diff -- src/              # read the code changes
git restore .                 # discard changes to tracked files
git clean -fd                 # remove new, untracked files (check git clean -n first)
```

For longer tasks, commit at good intermediate points ("export endpoint compiles, tests pending") so a bad
later turn can't cost you the good earlier ones.

### Tool checkpoints: quick, session-level undo

> **GitHub Copilot:** In **VS Code**, checkpoints are on by default: hover over an earlier request and choose
> **Restore Checkpoint** to roll files back to that point, and **Redo** to bring the changes back. In
> **Visual Studio**, use **Undo** in the Total changes list for a chunk, a file, or all edits. Checkpoints
> don't reverse terminal commands.
>
> **Claude Code:** Every prompt that starts a turn creates a checkpoint. Run `/rewind` (or press `Esc` twice
> with an empty prompt) and choose **Restore code and conversation**, **Restore conversation**, or **Restore
> code**. Checkpoints survive `/resume`, but they only track edits made with Claude's file tools: changes
> made by shell commands (`dotnet format`, `git checkout`, `rm`) and most subagent edits aren't restored.

### The limits of undo

No checkpoint can take back a command with external effects: a `git push`, a database update, a package
published to a feed, a comment posted to an issue. That is why commands like these belong in the
**ask** or **deny** list whatever mode you are in.

---

## Summary

- **Plan mode** makes the agent research and propose before it edits; reviewing and **editing the plan**
  is the cheapest place to catch design mistakes.
- The **supervision spectrum** runs from approving every action, to approving commands only, to approving
  the plan, to reviewing only the result.
- Claude Code's **permission modes** are Manual (`default`), `acceptEdits`, `plan`, `auto` (now the
  default starting mode), `dontAsk`, and `bypassPermissions` (isolated environments only).
- Copilot offers **Manual, Assisted, and Allow all** permissions and an **Autopilot** mode in VS Code, the
  **Confirm** dropdown in Visual Studio, and modes plus `/permissions` in the CLI.
- **Allow, ask, and deny rules** fine-tune any mode; deny wins, and `Bash(...)` and `PowerShell(...)` rules
  are separate.
- Choose a level by **reversibility, reach, test strength, and familiarity**, and give autonomous runs
  **isolation**.
- **Git** is the checkpoint that always works; tool **checkpoints** (`/rewind`, Restore Checkpoint) are
  quick but don't undo shell commands or external effects.
- Practice it in [Demo 12](../../demos/12-agentic-loop-and-plan-mode/) and
  [Activity 12](../../activities/12-agentic-loop-and-plan-mode/).
