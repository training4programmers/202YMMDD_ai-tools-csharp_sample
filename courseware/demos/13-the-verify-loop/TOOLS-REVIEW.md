# Demo 13: Tools Review

This demo introduces the tool features that make a verify loop run by itself: **pre-approving the gate
commands**, **protecting files** from agent edits, a **PreToolUse hook** that blocks an action, and a
**PostToolUse hook** that formats code after every edit. The ideas are tool-neutral; the file names and
settings below are GitHub Copilot's and Claude Code's at the time of writing. Hooks in particular are
moving fast, so treat the vendors' documentation as the authority.

---

## Pre-approving the verify loop

An agent that has to ask before every `dotnet test` either stalls or trains you to click "allow" without
reading. Pre-approve the exact gate commands, and nothing broader.

| | GitHub Copilot | Claude Code |
|---|---|---|
| Where | VS Code setting `chat.tools.terminal.autoApprove` (workspace or user settings) | `permissions.allow` in `.claude/settings.json` |
| Example | `{ "/^dotnet (build\|test\|format)\\b/": true }` | `"Bash(dotnet test *)"`, `"PowerShell(dotnet test *)"` |
| In the CLI | `copilot --allow-tool='shell(dotnet:*)'` | `claude --allowedTools "Bash(dotnet test *)"` |
| Watch out | `shell(dotnet:*)` also allows `dotnet nuget push`; prefer exact subcommands | `Bash(...)` rules don't cover the `PowerShell` tool on Windows; add both |

---

## Protecting files from edits

"Don't edit the tests" in a prompt is a request. These settings turn it into a control, with different
strengths.

| | GitHub Copilot | Claude Code |
|---|---|---|
| Ask before editing | `"chat.tools.edits.autoApprove": { "**/*": true, "**/tests/**": false }` in VS Code | `"permissions": { "ask": ["Edit(/tests/**)"] }` |
| Never edit | (use a hook) | `"permissions": { "deny": ["Edit(/tests/**)"] }` |
| Watch out | Approval is still a click away | A deny rule also blocks **new** test files; neither setting stops a shell command from writing |

In Claude Code project settings, `/tests/**` is anchored at the project folder, and one `Edit(...)` rule
covers every file-writing tool.

In VS Code, the **last matching pattern decides**, so every `false` entry goes after `"**/*": true`. A
blanket `true` also approves edits to the guard itself, so the solution's `.vscode/settings.json` adds
`"**/scripts/**"`, `"**/.github/hooks/**"`, and `"**/.claude/**"` as `false` too.

---

## A PreToolUse hook that blocks

A `PreToolUse` hook runs before the tool and can stop it. The demo's guard blocks edits to test files that
already exist in git and allows new ones, which no simple path rule can express.

```json
{
  "hooks": {
    "PreToolUse": [
      {
        "matcher": "Edit|Write",
        "hooks": [
          {
            "type": "command",
            "command": "pwsh",
            "args": ["-NoProfile", "-File", "${CLAUDE_PROJECT_DIR}/scripts/protect-tests.ps1"]
          }
        ]
      }
    ]
  }
}
```

> **GitHub Copilot:** The Copilot CLI and cloud agent support a `preToolUse` hook in `.github/hooks/*.json`;
> it denies a call by printing `{"permissionDecision": "deny", "permissionDecisionReason": "..."}`. Add
> `"matcher": "edit|create"` to limit it to file edits. Their payload uses `toolName` and `toolArgs`, so a
> guard script must read that shape. VS Code's agent hooks are in preview.
>
> **Claude Code:** The hook receives JSON on stdin (`tool_input.file_path` for `Edit` and `Write`). Exit code
> 2 blocks the tool, and the stderr text is shown to Claude as the reason, so write it as an instruction
> ("fix the code, not the test").

---

## A PostToolUse hook that formats

A `PostToolUse` hook runs after the tool succeeds. Running `dotnet format` there means formatting never
needs a prompt, and `dotnet format --verify-no-changes` stays green.

| | GitHub Copilot | Claude Code |
|---|---|---|
| File | `.github/hooks/dotnet-format.json` | `.claude/settings.json` |
| Event | `postToolUse` with `"matcher": "edit\|create"` (a regex tested against the whole tool name) | `PostToolUse` with `"matcher": "Edit\|Write"` |
| Command | `"powershell": "pwsh -NoProfile -File scripts/format-changed.ps1"` (plus `bash` for the Linux cloud agent) | `"command": "pwsh"` with `args` (exec form) |
| Watch out | Tool names are lowercase (`edit`, `create`); leave out `matcher` and the hook fires after every tool | Full `dotnet format` takes seconds; format only the changed files |

Both files call the same script, `scripts/format-changed.ps1`, which ignores the payload and asks git which
`.cs` files changed since its last run. That makes one script work under both tools, and it still returns
in well under a second when nothing changed.

---

## Seeing and trusting hooks

Hooks run with your permissions on every matching event, so know which ones are active and review changes
to them like code.

> **GitHub Copilot:** Hook files are part of the repository; review `.github/hooks/` changes in pull
> requests. In VS Code, workspace hook files are subject to Workspace Trust. Hook support in Visual Studio
> isn't documented at the time of writing.
>
> **Claude Code:** `/hooks` lists every configured hook and its source file. When hook settings change
> during a session, review them in `/hooks` (or restart the session) so the change takes effect.

---

## Summary

- **Pre-approve exactly the gate commands** (`dotnet build`, `test`, `format`), never a whole tool family.
- **Protect test files** with approval patterns, a deny rule, or, most precisely, a **PreToolUse hook**.
- A **PostToolUse hook** that runs `dotnet format` on changed files keeps the format gate green without
  prompts.
- Claude Code hooks live in **`.claude/settings.json`**; Copilot CLI and cloud agent hooks live in
  **`.github/hooks/*.json`**; one script can serve both.
- **Review hooks like code** and check what is active with `/hooks`.
