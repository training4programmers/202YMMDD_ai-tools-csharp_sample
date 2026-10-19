# Your Coding Agent in Depth

Module 05 introduced agent mode inside the IDE. This chapter goes deeper into the agents you will use
every day for the rest of the course: where each one runs, how you start, resume, and clear a session, how
you see what it is costing, and the habits that keep long agent sessions productive. Both tool families
now ship several **surfaces**, the different places the same agent runs: inside an IDE, in a terminal, on
GitHub, or in a desktop app.

The concepts carry across tools, so the chapter starts with what every surface has in common, then gives
the concrete steps for GitHub Copilot and Claude Code, and ends with a side-by-side table. Menus and
settings move often in these products; when a label in your version differs from the one here, the
vendor's documentation is the authority.

---

## What every agent surface has in common

Whichever product and surface you use, you will work with the same five controls:

| Control | What it decides |
|---------|-----------------|
| **Session** | The conversation and context the agent carries from turn to turn |
| **Mode** | Whether it plans, asks before acting, or acts on its own (chapter 03) |
| **Model** | Which model, and so which speed, cost, and reasoning depth, does the work |
| **Instructions** | The context files it loads before your first prompt (module 06) |
| **Tools and permissions** | What it may do without asking (chapter 03) |

The surfaces differ mainly in **where the work happens** (your machine or the cloud) and **who is watching**
(you, live, or a reviewer, later).

| Surface | Runs on | You are | Best for |
|---------|---------|---------|----------|
| IDE agent (Visual Studio 2026, VS Code) | Your machine | Watching, with the editor open | Changes you want to see as they happen |
| Terminal agent (Copilot CLI, Claude Code CLI) | Your machine | Watching in a terminal beside any IDE | Multi-file work, scripted runs, Visual Studio users who want Claude Code |
| Cloud agent (Copilot cloud agent, Claude Code GitHub Action) | A GitHub-hosted runner | Reviewing a pull request later | Well-specified issues you can delegate (chapter 07) |

---

## GitHub Copilot in Visual Studio 2026

Agent mode has been generally available since Visual Studio 2022 17.14 and is on by default in Visual
Studio 2026.

1. Open the Copilot Chat window and choose **Agent** in the chat mode dropdown.
2. Select **Tools** to see the tools the agent can use, including MCP tools, and switch them on or off.
3. Describe the task. When the agent wants to run a tool that needs approval, the chat shows a
   **Confirm** option, whose dropdown lets you approve that tool for this session, this solution, or
   always.
4. Edited files are listed under **Total changes**, where you can keep or undo each change, each file, or
   everything at once.

Things to know for C# work in Visual Studio:

- Visual Studio reads `.github/copilot-instructions.md` and `.github/instructions/*.instructions.md`, but
  **not** `AGENTS.md` or `CLAUDE.md`. Put essentials in `copilot-instructions.md`.
- The built-in specialized agents include `@debugger`, `@profiler`, `@test`, `@modernize`, and a **Plan**
  agent. Custom agents live in `.github/agents/<name>.agent.md` (Visual Studio 18.4 or later).
- The chat experience is changing: Visual Studio 18.11 offers a new Copilot chat experience as an opt-in,
  and later versions make it the default. Menus may differ slightly by version.

---

## GitHub Copilot in VS Code

VS Code runs each agent session on a **harness**, the engine that drives the loop. You pick it with the
**Session Target** control:

- **Copilot**: built on the Copilot SDK, the same engine as the Copilot CLI; it can keep running in the
  background, and it is rolling out as the default for new sessions.
- **Local**: the classic in-editor harness. Prompt files (`.github/prompts/*.prompt.md`) work only here.
- **Claude**: built on Anthropic's Claude Agent SDK (introduced as a preview).
- **Codex**, and **Cloud**, which hands the task to the Copilot cloud agent.

In a session, the **permissions** dropdown in the chat input sets how approvals work (Manual, an
experimental Assisted level, or Allow all), and **Autopilot** is a separate agent mode that keeps going
until it decides the task is complete. Chapter 03 covers both. VS Code reads `AGENTS.md`,
`.github/copilot-instructions.md`, `CLAUDE.md`, and `.claude/rules/*.md`, so one set of instruction files
can serve both tools.

---

## The GitHub Copilot CLI

The Copilot CLI (`copilot`) has been generally available since February 2026. It is the same agent in a
terminal, which makes it a natural companion to Visual Studio.

```powershell
winget install GitHub.Copilot      # or: npm install -g @github/copilot  (Node 22+)
cd ~\labs\demo-12-agentic-loop-and-plan-mode
copilot                            # then /login the first time
```

Day-to-day commands inside a session:

| Command | What it does |
|---------|--------------|
| `Shift+Tab` | Cycles the modes: standard, plan, and autopilot |
| `/plan <task>` | Builds an implementation plan before any code changes |
| `/model` | Chooses the model, or **Auto** |
| `/resume` | Opens the session picker to continue an earlier session (`copilot --continue` from the shell) |
| `/compact` | Summarizes the conversation to free context |
| `/context` | Shows what is using the context window |
| `/clear` or `/new` | Starts a fresh conversation |
| `/permissions` | Shows or switches the permission mode |
| `/diff` | Reviews the changes in the working folder |

For one-shot, scripted runs, `copilot -p "<prompt>"` runs a single task, and flags such as
`--allow-tool='shell(dotnet:*)'` and `--deny-tool` decide which tools it may use without asking. Business
and Enterprise organizations must enable the CLI policy before members can use it.

---

## The Copilot cloud agent

The **Copilot cloud agent** works on GitHub instead of your machine. You
assign it an issue, it works on a branch in a GitHub Actions environment, and it opens a pull request for
you to review. Sessions are limited to one repository and 59 minutes, and it works only with repositories
hosted on GitHub. Chapter 07 and [Bonus Demo 04](../../demos/bonus/04-issue-to-pr-automation/) cover it in
full.

---

## Claude Code: the CLI, the VS Code extension, and the desktop app

Claude Code is one agent with several front ends:

- **CLI** (`claude`): install with the native installer, then check it.

  ```powershell
  irm https://claude.ai/install.ps1 | iex     # or: winget install Anthropic.ClaudeCode
  claude --version
  claude doctor
  ```

  On Windows it runs natively. Git for Windows is optional: without Git Bash, Claude Code uses its
  **PowerShell** tool (PowerShell 7 first). The sandbox feature needs macOS, Linux, or WSL2.
- **VS Code extension** (`anthropic.claude-code`): a chat panel with inline diffs. It bundles its own copy
  of the CLI, which doesn't put `claude` on your PATH.
- **Desktop app**: open the **Code** tab.
- **Web** (claude.ai/code) and the **GitHub Action** (chapter 07).

There is **no Claude Code extension for full Visual Studio**. The supported pattern is to run `claude` in
Visual Studio's Developer PowerShell window or in Windows Terminal next to the IDE, against the same
solution folder. Visual Studio picks up the file changes as they happen, and Copilot in Visual Studio 18.5
and later reads the same `.claude/skills/` folder (chapter 05).

---

## Sessions in Claude Code

A **session** is one conversation with its context, history, and file checkpoints. Claude Code saves
sessions locally, so you can leave and come back.

| Command | What it does |
|---------|--------------|
| `claude` | Starts a new session in the current folder |
| `claude --continue` (`-c`) | Continues the most recent session in this folder |
| `claude --resume` (`-r`) or `/resume` | Picks a session by name or from a list |
| `/clear` | Starts a fresh conversation with empty context |
| `/compact [focus]` | Summarizes the conversation so far to free context, optionally with focus instructions |
| `/context` | Shows what is filling the context window |
| `/rewind` (or `Esc` twice) | Restores code and/or conversation to an earlier prompt (chapter 03) |
| `/branch` | Branches the conversation so you can try another direction |

When to use which:

- **New task**: `/clear` (or a new session). Old debugging output is noise for the next task.
- **Same task, conversation getting long**: `/compact`, telling it what to keep, for example
  `/compact keep the approved plan and the failing test names`.
- **Interrupted yesterday**: `claude --continue`, then ask for a status summary before it does anything.

> **GitHub Copilot:** In VS Code each chat session appears in the sessions list and can be reopened; start a
> new one with the **+** button. The Copilot CLI uses `/resume`, `copilot --continue`, `/compact`, and
> `/clear` in the same way.
>
> **Claude Code:** Sessions are per folder. Because course labs live in `~\labs\<lab-name>`, each lab keeps
> its own session history.

---

## Seeing what the agent costs

Agent sessions use far more tokens than chat: every turn re-sends context and tool output. Know where to
look before the bill does it for you.

> **GitHub Copilot:** Usage is metered in **GitHub AI Credits**. Code completions and next edit suggestions
> are included and don't consume credits; chat, agent sessions, the Copilot CLI, and the cloud agent do.
> Check your own usage in your GitHub account's Copilot and billing settings; organization owners see usage
> reports and can set budgets. Allowances vary by plan, so see the
> [Copilot plans page](https://docs.github.com/en/copilot/get-started/plans) for current amounts.
>
> **Claude Code:** `/usage` (aliases `/cost` and `/stats`) shows the session's cost, your plan's usage
> limits, and activity stats. `/context` shows what fills the context window. On a subscription plan,
> usage counts against plan limits; with an API key it is billed per token. See
> [Claude pricing](https://claude.com/pricing) for current plans.

Model choice is the biggest cost lever. A frontier/reasoning-tier model plans better and costs more per
token; a fast, low-cost tier is often enough for mechanical edits, test writing, or summaries. Both tools
let you switch per session (`/model` in Claude Code and the Copilot CLI, the model picker in the IDEs).
Claude Code also has an `opusplan` alias that plans with its most capable tier and executes with a faster
one.

---

## Day-to-day habits that work

These habits matter more than any single feature.

1. **Start from a clean git state.** Commit or stash first, so `git diff` shows exactly what the agent did
   and `git restore` undoes it. The course's `start-lab.ps1` does this for every lab.
2. **One task, one session.** Clear between tasks.
3. **Write the task down.** A `TASK.md` with acceptance criteria beats a long chat message.
4. **Plan first for anything multi-file** (chapter 03), and edit the plan before you approve it.
5. **Keep the terminal output visible.** Watching the build and test output scroll by is how you catch
   thrashing early.
6. **Interrupt freely.** If it heads the wrong way, stop it, correct it, and continue. Redirecting at turn 3
   costs far less than unpicking turn 30.
7. **Ask for a summary at the end**: files changed, tests added, open questions. Then check it against the
   diff.
8. **Review the diff, not the summary.** The summary is the agent's opinion of what it did; the diff is what
   it did.
9. **Never paste secrets into a prompt**, and keep them out of the repo (module 14).

---

## Side by side

| | GitHub Copilot | Claude Code |
|---|---|---|
| In Visual Studio 2026 | Agent mode in Copilot Chat; Tools picker; Confirm dropdown | Run `claude` in Developer PowerShell beside the IDE |
| In VS Code | Agent sessions with Copilot, Local, Claude, Codex, or Cloud harness | `anthropic.claude-code` extension, or the CLI in the terminal |
| Terminal | `copilot` (Copilot CLI) | `claude` |
| Cloud / GitHub | Copilot cloud agent (assign an issue) | Claude Code GitHub Action (`@claude`) |
| Instruction files | `.github/copilot-instructions.md`, `.github/instructions/`; plus `AGENTS.md` in VS Code and CLI | `CLAUDE.md` (with `@AGENTS.md` import), `.claude/rules/` |
| Resume a session | Sessions list (VS Code); `/resume`, `copilot --continue` | `/resume`, `claude --continue` |
| Free context | `/compact`, `/clear` | `/compact`, `/clear` |
| Plan first | Plan agent (VS, VS Code); `Shift+Tab` or `/plan` in the CLI | `Shift+Tab` or `/plan` |
| Usage | AI credits in GitHub billing settings | `/usage` |
| Switch model | Model picker; `/model` in the CLI | `/model`, `--model` |

---

## Summary

- Both tool families ship several **surfaces** (IDE, terminal, cloud) that share the same controls:
  session, mode, model, instructions, and tools and permissions.
- **Copilot agent mode** runs in Visual Studio 2026 and VS Code; VS Code adds a choice of **harness**, and
  Visual Studio reads only the `.github` instruction files.
- The **Copilot CLI** and the **Claude Code CLI** bring the same agents to a terminal, which is how Claude
  Code works alongside full Visual Studio.
- Learn the **session** commands (`/resume`, `/clear`, `/compact`, `/context`) so long sessions stay
  focused and affordable.
- Check **usage** regularly: AI credits for Copilot, `/usage` for Claude Code, and use **model tiers** as the
  main cost lever.
- Good **habits** (clean git state, one task per session, plan first, interrupt early, review the diff)
  matter more than any single feature.
- Next: [Plan Mode and the Supervision Spectrum](03-plan-mode-and-the-supervision-spectrum.md).
