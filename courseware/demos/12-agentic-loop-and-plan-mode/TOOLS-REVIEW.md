# Demo 12: Tools Review

This demo introduces the controls you use to run an agent through a whole feature: **plan mode**,
**approving and editing a plan**, choosing a **permission level** for the implementation, approving
**terminal commands**, rolling back with **checkpoints**, and managing the **session**. The concepts are
tool-neutral; the names below are the ones GitHub Copilot and Claude Code use at the time of writing.
Menus and settings move between versions, so the vendors' documentation is the authority.

---

## Plan mode

In plan mode the agent reads files and may run read-only commands, but it doesn't edit your code until you
approve a plan. Use it for any change that touches more than one or two files.

| | GitHub Copilot | Claude Code |
|---|---|---|
| Where | Plan agent in VS Code and Visual Studio 2026; plan mode in the Copilot CLI | CLI, VS Code extension, desktop |
| How | Pick **Plan** in the agent picker; in the CLI press `Shift+Tab` or type `/plan <task>`; `/plan` also works in VS Code's Copilot harness | `Shift+Tab` until `plan mode on`, `/plan <task>`, or `claude --permission-mode plan` |
| Make it the default | (per session) | `"permissions": { "defaultMode": "plan" }` in `.claude/settings.json` |
| Watch out | The Plan agent proposes; switch to **Agent** to implement | Plan mode can still run read-only commands to explore |

---

## Editing and approving a plan

A plan is a draft. Change it before approving: it costs a sentence now instead of a review later.

> **GitHub Copilot:** Reply in the chat with the changes ("use an explicit lookup instead of
> `Enum.TryParse`") and ask for the revised plan; when it's right, switch to **Agent** (or use the plan's
> hand-off to implementation, where offered) and ask it to implement the plan.
>
> **Claude Code:** When the plan is ready, press `Ctrl+G` to open it in your editor and edit it directly,
> or choose **No, keep planning** and type the changes. Then approve with **Yes, and use auto mode** (shown
> as **Yes, auto-accept edits** where auto mode isn't available) or **Yes, manually approve edits**.
> Approving switches the session to that permission mode.

---

## Edits accepted, commands approved

The level used in this demo: file edits land without prompts (you review them in the diff), but every
terminal command waits for you.

| | GitHub Copilot | Claude Code |
|---|---|---|
| Set it | VS Code: **Manual permissions** in the permissions dropdown; edits are applied and you **Keep** or **Undo** them. Visual Studio: the **Confirm** dropdown per tool | **accept edits on** in the status bar (`Shift+Tab` from Manual), or `--permission-mode acceptEdits` |
| Approving commands | Allow once, or for the session or workspace; `chat.tools.terminal.autoApprove` rules in VS Code | Approve once, or choose **Yes, and don't ask again** for that command in this project |
| Pre-approve the verify loop | `"chat.tools.terminal.autoApprove": { "/^dotnet (build\|test)\\b/": true }` | `"permissions": { "allow": ["Bash(dotnet test *)", "PowerShell(dotnet test *)"] }` |
| Watch out | Approving a broad pattern approves everything it matches | On Windows without Git Bash, commands run through the `PowerShell` tool; `Bash(...)` rules don't cover it |

---

## Checkpoints and rollback

Tool checkpoints undo the agent's file edits quickly within a session. Git is still the checkpoint that
always works.

> **GitHub Copilot:** In VS Code, hover over an earlier request and choose **Restore Checkpoint**; **Redo**
> brings the changes back. In Visual Studio, use **Undo** in the **Total changes** list for a change, a
> file, or everything.
>
> **Claude Code:** `/rewind` (or `Esc` twice with an empty prompt) lists your prompts; choose **Restore code
> and conversation**, **Restore conversation**, or **Restore code**. Edits made by shell commands aren't
> tracked, so `git restore .` remains the fallback.

```powershell
git diff --stat           # what the agent changed since the lab's baseline commit
git restore . ; git clean -fd    # back to the baseline (check git clean -n first)
```

---

## Sessions and usage

One task per session keeps the context focused and the cost down.

| | GitHub Copilot | Claude Code |
|---|---|---|
| New task | New chat session (**+**); `/clear` or `/new` in the CLI | `/clear` |
| Long conversation | `/compact` in the CLI | `/compact keep the approved plan` |
| Continue later | Sessions list in VS Code; `/resume` in the CLI | `claude --continue` or `/resume` |
| See usage | AI credits in your GitHub Copilot and billing settings | `/usage` (aliases `/cost` and `/stats`) |

---

## Summary

- **Plan mode** keeps the agent read-only until you approve; both tools offer it in the IDE and the CLI.
- **Edit the plan** before approving: `Ctrl+G` or "keep planning" in Claude Code, a chat reply in Copilot.
- **Accept edits, approve commands** is a good default for implementation: you review edits in the diff
  and decide on each command.
- **Checkpoints** (`/rewind`, Restore Checkpoint, Undo) give quick rollback; **git** covers what they
  can't.
- **Clear between tasks**, compact long sessions, and check **usage** as you go.
