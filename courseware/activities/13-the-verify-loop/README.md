# Activity 13: Drive the Agent to Green

## Objective

A teammate left the Bookstore solution halfway through a migration: coupons moved to a lookup table, ISBN
handling was "simplified", and an AI-drafted lookup endpoint was never reviewed. Several tests fail, and the
build is lenient. Your job is to set up a verify loop (strict build settings, the four gates, rules the
agent must follow, and at least one guard) and then drive your agent until build, analyzers, tests, and
format are all green, **without the existing tests changing** and with a root cause for every fix.

![The verify loop: the agent edits code, then dotnet build, analyzers, dotnet test, and dotnet format run in order; any failure sends its output back to the agent, and all-green work goes to human review.](../../diagrams/png/07-verify-loop.png)

*Figure: The gates you'll put in your agent's loop.*

## Prerequisites

- .NET 10 SDK, Git, and PowerShell 7
- GitHub Copilot (agent mode or the Copilot CLI) or Claude Code
- Activity 12, and the chapter [The Verify Loop](../../docs/module-07-agentic-coding/04-the-verify-loop.md)

## Skills Practiced

- Turning on `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended`, and `EnforceCodeStyleInBuild`
- Instructing an agent to run build, test, and format gates and to read their output
- Grouping failures by root cause instead of fixing symptoms one at a time
- Guarding tests against "fixing the test instead of the code" with rules, settings, or a hook
- Scoping an analyzer exception narrowly, with a justification
- Verifying the agent's claims with exit codes and `git diff`

## Instructions

Start the lab from the course repo root. This copies `begin/` to a folder outside the repo, makes it a
git repository, and commits a baseline, so `git diff` always shows exactly what you and your assistant
changed:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 13
cd ~\labs\activity-13-the-verify-loop
```

Open the folder (or `Bookstore.slnx`) in Visual Studio 2026 or VS Code. To start over, re-run the
command with `-Force`. The reference in `solution/` is **one acceptable result**; yours will differ.

> **In class / take-home.** In class (about 30 minutes): steps 1 to 6. If the agent is still iterating
> when time is up, let it finish and do step 6's verification afterward. Take-home: step 7 and the
> stretch goal.

1. **Measure the starting point.** Run the three gates yourself and note the results: the build, the test
   summary (how many failures?), and the exit code of the format check. Read `TASK.md`.

   <details>
   <summary>Guided help: step 1</summary>

   Run the gates yourself before the agent does, so you can check its claims later:

   ```powershell
   dotnet build
   # Build succeeded. 0 Warning(s), 0 Error(s): lenient, because TreatWarningsAsErrors is off
   dotnet test --solution Bookstore.slnx
   # total: 26, failed: 8 (in PriceCalculatorTests, IsbnTests, and BookLookupApiTests)
   dotnet format Bookstore.slnx --verify-no-changes ; $LASTEXITCODE
   # whitespace errors and IDE0161 in BookLookupEndpoints.cs, then 2: non-zero, files would change
   ```

   **Look for:** the data in each failing test's name and output. Coupon codes in other cases or with
   spaces, exactly 10 copies, and ISBNs with hyphens or spaces in the middle all fail. Note the groups
   you see; step 7 asks how many causes they come down to.

   </details>

2. **Guard the tests before the agent starts.** Put at least one control in place that goes beyond a
   sentence in the prompt: an approval pattern for `tests/**`, a deny rule, or a `PreToolUse` hook. Decide
   whether your guard should allow **new** test files, and check that it does what you expect.

   <details>
   <summary>Guided help: step 2</summary>

   Pick the guard your tool supports, put it in place before the first prompt, and test it.

   > **GitHub Copilot:** In VS Code, create `.vscode/settings.json` (below) so edits to tests, and to
   > the guard files themselves, wait for your approval. The last matching pattern wins, so the `false`
   > entries come after `"**/*": true`. New test files ask too; you can approve those.
   >
   > In the Copilot CLI, start the session with a deny rule for each test file already in git (the
   > PowerShell below). `write(<name>)` matches a file of that name in any folder, a deny rule beats every
   > allow rule, and new test files still ask first. Like every file-tool rule, it doesn't cover shell
   > commands; those still ask.
   >
   > In Visual Studio 2026 there's no path-based edit guard at the time of writing. The agent applies its
   > edits as it goes, so review each file in **Total changes** in the chat window and **Undo** any change
   > to an existing test; **Keep** the rest.
   >
   > **Claude Code:** A deny rule (`"deny": ["Edit(/tests/**)"]`) is simplest, but it also blocks new
   > test files. The precise guard is a `PreToolUse` hook that blocks only test files already in git:
   > copy the script under "3. Make it a precise hook" in
   > [The Verify Loop](../../docs/module-07-agentic-coding/04-the-verify-loop.md) to
   > `scripts/protect-tests.ps1`, and register it in `.claude/settings.json` (below).

   ```jsonc
   {
     "chat.tools.edits.autoApprove": {
       "**/*": true,
       "**/.vscode/*.json": false,
       "**/tests/**": false,
       "**/scripts/**": false,
       "**/.github/hooks/**": false,
       "**/.claude/**": false
     }
   }
   ```

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
               "args": ["-NoProfile", "-File", "${CLAUDE_PROJECT_DIR}/scripts/protect-tests.ps1"],
               "timeout": 30
             }
           ]
         }
       ]
     }
   }
   ```

   For the Copilot CLI, from the lab folder:

   ```powershell
   $guard = git ls-files tests | ForEach-Object { '--deny-tool'; "write($(Split-Path $_ -Leaf))" }
   $guard -join ' '
   # --deny-tool write(BookApiTests.cs) --deny-tool write(BookLookupApiTests.cs) ... (one per file)
   copilot @guard
   ```

   **Test it** in a new session (in Claude Code, `/hooks` should list your `PreToolUse` hook):

   ```text
   Add a blank line at the end of tests/Bookstore.Tests/IsbnTests.cs. Then create
   tests/Bookstore.Tests/GuardCheck.cs containing an empty internal class named GuardCheck.
   ```

   **Check:** the hook blocks the edit to `IsbnTests.cs` with its message (in VS Code, the edit waits
   for you: reject it; the Copilot CLI reports it was denied by `write(IsbnTests.cs)`; in Visual Studio,
   undo it), and the new file is allowed (Claude Code's `Edit(/tests/**)` deny rule blocks both; that's
   its trade-off).
   Then delete the new file, if it was created, with
   `Remove-Item tests\Bookstore.Tests\GuardCheck.cs`; `git status --short` should list only your guard
   files.

   </details>

3. **Instruct the agent.** Give it the goal, the three gates in order, and the rules from `TASK.md`: no test
   edits, no solution-wide rule switch-offs, a root cause for every fix, and a final rerun of all gates.
   Choose a supervision level and pre-approve only the gate commands.

   <details>
   <summary>Guided help: step 3</summary>

   Pre-approve the three gate commands and nothing else, so every other command still asks:

   > **GitHub Copilot:** In VS Code, add the terminal rule below to `.vscode/settings.json` and use
   > Agent mode. In the Copilot CLI, don't answer **Yes, and approve** for a gate: outside `git` and `gh`
   > it approves the whole command name, so approving `dotnet build` approves every `dotnet` command
   > (`dotnet new`, `dotnet add`, ...). Restart the CLI with one allow rule per gate instead (below);
   > `shell(dotnet build)` allows `dotnet build` with any options, but not other `dotnet` commands. In
   > Visual Studio, approve each gate command when it asks.
   >
   > **Claude Code:** Merge the allow list below into `.claude/settings.json`, beside the `hooks` key
   > from step 2, and work in accept-edits mode (Shift+Tab until the footer shows it).

   ```jsonc
   "chat.tools.terminal.autoApprove": {
     "/^dotnet (build|test|format)\\b/": true
   }
   ```

   ```json
   {
     "permissions": {
       "allow": [
         "Bash(dotnet build *)", "Bash(dotnet test *)", "Bash(dotnet format *)",
         "PowerShell(dotnet build *)", "PowerShell(dotnet test *)", "PowerShell(dotnet format *)"
       ]
     }
   }
   ```

   ```powershell
   # Copilot CLI: the step 2 guard plus the three gates (rerun the $guard line first in a new terminal)
   copilot @guard --allow-tool 'shell(dotnet build)' --allow-tool 'shell(dotnet test)' `
       --allow-tool 'shell(dotnet format)'
   ```

   ```text
   Read TASK.md. In Directory.Build.props, turn on TreatWarningsAsErrors, AnalysisLevel
   latest-recommended, and EnforceCodeStyleInBuild. Then make these three gates pass, in this order,
   stopping at the first red one and fixing it before you run the next:

     dotnet build
     dotnet test --solution Bookstore.slnx
     dotnet format Bookstore.slnx --verify-no-changes

   Rules: never edit, skip, or delete a test, and never change an expected value. No solution-wide
   analyzer switch-offs: scope any exception as narrowly as you can and justify it in a comment.
   PriceCalculator stays an injectable instance class with the same Calculate signature. Before you
   fix failing tests, group them by root cause and explain each group in one sentence. New errors after
   a fix may only mean another project now builds. Finish by rerunning all three gates and pasting
   their summary lines.
   ```

   **Check:** the agent's first move is the `Directory.Build.props` change and a build, not a test edit.

   </details>

4. **Let it iterate, and watch.** Expect errors to arrive in waves as each project starts to build. Note
   where the agent hesitates or tries a shortcut, and what your guard did about it.

   <details>
   <summary>Guided help: step 4</summary>

   Each project is analyzed only once the projects it depends on build, so the errors come in this
   order in this starter:

   ```text
   dotnet build   -> Bookstore.Domain: CA1310 x2 (Isbn.cs), CA1822 and CA1854 (PriceCalculator.cs)
   dotnet build   -> Bookstore.Api: CA2016 x2 (QuoteEndpoints.cs, BookLookupEndpoints.cs), IDE0161
   dotnet build   -> Bookstore.Tests: CA1707 x15 (underscores in test method names)
   dotnet test    -> 8 failed
   dotnet format  -> whitespace in BookLookupEndpoints.cs
   ```

   **Watch for shortcuts:** renaming test methods to clear CA1707 (your guard should stop it), making
   `Calculate` static, a `<NoWarn>` in `Directory.Build.props`, `[Fact(Skip = ...)]`, or a changed
   expected value. Note each attempt and what the guard did. **If it renames tests or switches a rule
   off everywhere:** redirect it.

   ```text
   Don't touch the test files. Scope the exception instead: turn CA1707 off for tests/**.cs only in
   .editorconfig, with a comment explaining why test names may contain underscores.
   ```

   </details>

5. **Make the judgment calls yourself.** At least two findings don't have a mechanical fix: one rule fires
   on code where the rule doesn't fit, and one suggestion would change `PriceCalculator`'s public shape,
   which `TASK.md` forbids. Decide what the right, narrowly scoped answer is, and make sure the agent's
   change matches.

   <details>
   <summary>Guided help: step 5</summary>

   The two findings are CA1707 on the test names and CA1822 on `PriceCalculator.Calculate`. For test
   code, scope the rule in `.editorconfig` (a new section at the end of the file):

   ```ini
   [tests/**.cs]
   # xUnit test names read as sentences (Method_does_this_when_that). CA1707 protects public API names,
   # and test methods aren't API, so the rule is off for test code only.
   dotnet_diagnostic.CA1707.severity = none
   ```

   For `Calculate`, "make it static" breaks every caller that uses the injected instance. Ask for a
   suppression on that one method:

   ```text
   CA1822 on PriceCalculator.Calculate: don't make it static. TASK.md keeps its public shape, and it's
   injected as a service. Suppress CA1822 on that one method with a SuppressMessage attribute and a
   Justification.
   ```

   ```csharp
   [SuppressMessage(
       "Performance",
       "CA1822:Mark members as static",
       Justification = "PriceCalculator is registered and injected as a service; callers depend on the instance API.")]
   ```

   **Check:** neither exception reaches beyond the code it's about, and each says why.

   </details>

6. **Verify the claim.** When the agent says it's done, rerun all three gates yourself and check that
   `git diff --stat -- tests/` shows no changes to existing test files.

   <details>
   <summary>Guided help: step 6</summary>

   Check exit codes and the diff, not the agent's summary:

   ```powershell
   dotnet build                                       # Build succeeded. 0 Warning(s), 0 Error(s)
   dotnet test --solution Bookstore.slnx              # total: 26, failed: 0
   dotnet format Bookstore.slnx --verify-no-changes ; $LASTEXITCODE
   # 0
   git diff --stat -- tests/                          # (no output): existing tests unchanged
   git diff -- Directory.Build.props .editorconfig    # the three settings, and only scoped exceptions
   Select-String -Path src\Bookstore.Domain\PriceCalculator.cs -Pattern 'public .*Calculate\('
   # public Quote Calculate(IReadOnlyList<QuoteLine> lines, string? couponCode = null)
   ```

   **Check:** every number matches, and `Calculate` has no `static`. **If anything is red or a test
   file changed:** paste the output back to the agent and ask for the root cause before the fix.

   </details>

7. **Count root causes.** Write down how many tests failed at the start and how many distinct root causes
   explained them.

   <details>
   <summary>Guided help: step 7</summary>

   Ask the agent to map the failures to its fixes, then check its answer against your step 1 notes:

   ```text
   List the tests that failed in my first dotnet test run, grouped by the root cause you fixed. For
   each group: the one-sentence cause, the tests it explains, and the file and line of the fix.
   ```

   **Check:** the reference found **8 failures from 3 root causes**: the coupon table's case-sensitive,
   untrimmed lookup (3 tests); the bulk discount using `>` instead of `>=` at 10 copies (1 test); and
   `Isbn.Normalize` trimming hyphens and spaces only at the ends (4 tests, in `IsbnTests` and
   `BookLookupApiTests`). If your count differs, find the test you can't explain yet.

   </details>

<details>
<summary>Prompting hints</summary>

- Include the three exact gate commands, in order, and ask the agent to stop at the first red one.
- State the rules as rules: no editing, skipping, or deleting tests; no changed expected values; no
  solution-wide `NoWarn` or analyzer switch-offs; one-sentence root cause per fix.
- Ask it to group failing tests by root cause before changing code, and to explain each group.
- Tell it that new errors appearing after a fix may mean more projects now build, not a regression.
- Ask for the final summary lines of all three gates, not a description of them.

</details>

## Requirements

- `Directory.Build.props` turns on `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended`, and
  `EnforceCodeStyleInBuild`.
- `dotnet build` reports no warnings or errors, `dotnet test --solution Bookstore.slnx` passes, and
  `dotnet format Bookstore.slnx --verify-no-changes` exits with code 0.
- Existing test files are byte-for-byte unchanged; new tests, if any, are in new files.
- No analyzer rule is switched off for the whole solution; any exception is scoped and justified in a
  comment.
- `PriceCalculator` is still an injectable instance class with the same `Calculate` signature.
- You can name the root cause behind each group of failures, and at least one guard was in place before the
  agent started.

## Expected behavior

```powershell
# Before
dotnet build                                       # Build succeeded (lenient settings)
dotnet test --solution Bookstore.slnx              # total: 26, failed: 8
dotnet format Bookstore.slnx --verify-no-changes   # non-zero exit code

# After
dotnet build                                       # Build succeeded. 0 Warning(s) 0 Error(s)
dotnet test --solution Bookstore.slnx              # total: 26, failed: 0
dotnet format Bookstore.slnx --verify-no-changes   # exit code 0
git diff --stat -- tests/                          # nothing, or only new files
```

With the API running, both ISBN forms find the same book:

```http
### Hyphenated ISBN: 200, "The Quiet Compiler"
GET http://localhost:5080/api/books/by-isbn/978-1-23456-001-0

### Digits only: 200, the same book
GET http://localhost:5080/api/books/by-isbn/9781234560010
```

## Stretch goal

Add a `Stop` hook (Claude Code) that runs `dotnet test --solution Bookstore.slnx` when the agent tries to
finish and makes it continue if any test fails. Make sure it can't trap the agent in an endless loop, and
test it by asking the agent to stop with a failing test in place. (Leave `solution/` as is; this is extra
practice.)

## Reference

- Concepts: [Module 07: Agentic Coding](../../docs/module-07-agentic-coding/), especially
  [The Verify Loop](../../docs/module-07-agentic-coding/04-the-verify-loop.md) and
  [Subagents, Hooks, and Skills](../../docs/module-07-agentic-coding/05-subagents-hooks-and-skills.md)
- Worked example: [matching demo](../../demos/13-the-verify-loop/)
