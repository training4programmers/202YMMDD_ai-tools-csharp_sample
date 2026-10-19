# Demo 13: The Verify Loop (ToolsApp / Colors)

Instructor demo paired with [Activity 13](../../activities/13-the-verify-loop/). The agent turns on strict
build settings in a half-finished solution and drives build, analyzers, tests, and format to green by
reading the toolchain's output, without being allowed to touch the existing tests.

## Concept

- The **verify loop** puts four gates inside the agent's loop: `dotnet build`, the Roslyn **analyzers**,
  `dotnet test`, and `dotnet format --verify-no-changes`.
- `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended`, and `EnforceCodeStyleInBuild` turn
  AI-typical C# mistakes (missing cancellation tokens, culture-sensitive strings, nullable slips) into
  build failures with rule IDs the agent can act on.
- Test failures name the inline data and the **expected versus actual** values, which is usually enough to
  find the **root cause**.
- The most tempting shortcut is **fixing the test instead of the code**; rules, a `PreToolUse` hook, and the
  final `git diff` stop it.
- A **`PostToolUse` hook** runs `dotnet format` after every edit, so formatting never needs a prompt.

![The verify loop: the agent edits code, then dotnet build, analyzers, dotnet test, and dotnet format run in order; any failure sends its output back to the agent, and all-green work goes to human review.](../../diagrams/png/07-verify-loop.png)

*Figure: The gates the agent runs in this demo, cheapest first.*

## How to run

From the course repo root, copy the starting point to a lab folder outside the repo (so the repo's own
AGENTS.md/CLAUDE.md don't leak into the session) and open it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 13 -Open
cd ~\labs\demo-13-the-verify-loop
dotnet build
dotnet test --solution ToolsApp.slnx
```

The build succeeds with one nullable warning, and **three tests fail on purpose**. Also copy the recorded
outcome now, because steps 2 and 5 take the hook scripts from it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 13 -Solution     # -> ~\labs\demo-13-the-verify-loop-solution
```

Claude Code is the best fit, because steps 2 and 5 use its hooks; with GitHub Copilot, use an agent
session in VS Code and show the Copilot equivalents as you go. Any capable model works; the run takes 10 to 15 minutes. If the live run goes sideways, the
solution copy above is the recorded outcome; `FIXES.md` there is the agent's root-cause summary.

## Demo trail

> **Short on time (about 20 minutes).** Run Steps 1 to 4 live, narrating the waves and the CA1707
> decision while the agent works. Skip Step 5's live format edit: show `scripts/format-changed.ps1` and
> the settings files in the solution copy you already made. Run Step 6 in whichever copy is finished
> (the lab, or the solution copy if the run is still going); its `FIXES.md` is the recorded root-cause
> summary.

**Code to open (in order):**

1. `TASK.md`: the three strict settings, the three gates, and the rules, especially "fix the code, not the
   tests" and "no solution-wide switch-offs".
2. `Directory.Build.props`: `TreatWarningsAsErrors` is `false` "while the change is in progress".
3. `src/ToolsApp.Api/Endpoints/ColorSearchEndpoints.cs`: marked as unreviewed AI output. Let the audience
   spot problems, but don't fix anything yet.
4. `src/ToolsApp.Domain/ColorMath.cs`: the "tidied-up" `TryParseHex`, `ToCssHsl`, and `Rate`.
5. `tests/ToolsApp.Tests/ColorMathTests.cs`: the tests that will fail, and why they're right.

**Step 1: Show the red starting state.**

```powershell
dotnet build                                      # Build succeeded, 1 warning: CS8602
dotnet test --solution ToolsApp.slnx              # total: 25, failed: 3
dotnet format ToolsApp.slnx --verify-no-changes   # exit code 2: whitespace and IDE0161
```

Point out that a lenient build let the nullable warning scroll past, and that each failing test already
says expected versus actual.

**Step 2: Guard the tests before prompting.** Rules in a prompt are requests; a hook is enforcement. Copy
the guard script from the solution copy and register it as a `PreToolUse` hook:

```powershell
$sol = "$HOME\labs\demo-13-the-verify-loop-solution"
New-Item -ItemType Directory -Force scripts, .claude | Out-Null
Copy-Item "$sol\scripts\protect-tests.ps1" scripts\
@'
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
'@ | Set-Content .claude\settings.json
```

Open `scripts/protect-tests.ps1`: it reads the tool call's JSON from stdin, and if the target is a test file
already in git it writes a reason to stderr and exits with code 2, which blocks the edit. New test files
are allowed. Start a new Claude Code session and run `/hooks` to show it is loaded. For Copilot in VS Code,
show the `"**/tests/**": false` entry in the solution's `.vscode/settings.json`
(`chat.tools.edits.autoApprove`), which makes test edits ask for approval instead. The entries after it
do the same for `scripts/`, `.github/hooks/`, and `.claude/`, so `"**/*": true` can't approve an edit to
the guard itself; the last matching pattern decides.

**Step 3: Instruct the agent.** Use accept-edits mode (or VS Code with Manual permissions) and approve each
command. Prompt (accept-edits mode):

```text
Read TASK.md. Turn on warnings-as-errors and latest-recommended analysis, and enforce code style in the
build, in Directory.Build.props. Then make build, test, and format green:

  dotnet build
  dotnet test --solution ToolsApp.slnx
  dotnet format ToolsApp.slnx --verify-no-changes

Never edit tests to make them pass. Don't switch analyzer rules off for the whole solution; scope any
exception and justify it in a comment. Group failures by root cause and give each fix a one-sentence
root cause. Before you say you're done, rerun all three gates and show me their summary lines.
```

**Step 4: Watch it read the output and iterate.** Narrate what appears; runs vary, but expect:

- The first strict build reports a single **CA1305** in `ToolsApp.Domain`. Fixing it uncovers 11 errors in
  `ToolsApp.Api` (CS8602, CA2016, CA1304, CA1311, CA1862, IDE0161), then 13 **CA1707** errors on test
  method names. Errors arrive in waves because a failed project hides its dependents' errors.
- **The CA1707 decision**: renaming the tests breaks the rules, a global `NoWarn` breaks the rules, a
  `[tests/**.cs]` section in `.editorconfig` with a comment is the right fix. If the agent tries to edit a
  test file, the hook blocks it and the agent reads the reason.
- Three test failures, three root causes: the shorthand expansion writes positions `i` and `i + 3`
  instead of `2i` and `2i + 1`; `Rate` uses `>` where the thresholds are minimums; `ToCssHsl` truncates
  32.5% to 32 instead of rounding.

**Step 5: Add the format hook.** Copy the format script and the solution's settings file, which adds a
`PostToolUse` hook on `Edit|Write` (and allow rules for the three `dotnet` commands) next to the guard:

```powershell
Copy-Item "$sol\scripts\format-changed.ps1" scripts\
Copy-Item "$sol\.claude\settings.json" .claude\ -Force
Copy-Item "$sol\.github\hooks" .github\ -Recurse -Force      # .github/hooks/dotnet-format.json for Copilot
```

Run `/hooks` to review and load the change (if the new hook isn't listed, restart with `claude --continue`).
Walk through `scripts/format-changed.ps1`: it asks git which `.cs` files changed since its last run and
formats only those. Then show it working. Prompt (accept-edits mode):

```text
In ColorSearchEndpoints.cs, change the "q is required." message to "q is required and can't be blank."
```

The hook runs after the edit; `git diff` shows correctly formatted code even if the agent's edit wasn't.
Show the Copilot version, `.github/hooks/dotnet-format.json`, which calls the same script.

**Step 6: Review.**

```powershell
dotnet build                                      # 0 Warning(s), 0 Error(s)
dotnet test --solution ToolsApp.slnx              # total: 28, failed: 0
dotnet format ToolsApp.slnx --verify-no-changes   # exit code 0
git diff --stat -- tests/                         # only the new test file, if any
git diff -- .editorconfig Directory.Build.props
```

**Key teaching beat:** The agent didn't need you to explain any of the bugs. It needed strict gates that
make every problem visible, rules that close the shortcuts, and a hook that enforces the rule it is most
tempted to break. With those in place, "done" stops being the agent's opinion and becomes three exit
codes.

## Reference

- Concepts: [Module 07: Agentic Coding](../../docs/module-07-agentic-coding/):
  [The Verify Loop](../../docs/module-07-agentic-coding/04-the-verify-loop.md),
  [Subagents, Hooks, and Skills](../../docs/module-07-agentic-coding/05-subagents-hooks-and-skills.md)
- Tools: [Tools Review](TOOLS-REVIEW.md)
- Practice: [Activity 13](../../activities/13-the-verify-loop/)
