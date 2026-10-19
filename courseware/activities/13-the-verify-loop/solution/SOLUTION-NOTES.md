# Activity 13: Solution Notes

> This is **one acceptable result**, produced with an AI assistant and then reviewed by a person. Your
> prompts, your assistant's output, and your final code will differ. That's expected, as long as you
> meet the activity's Requirements.

## How this result was produced

- Claude Code CLI, frontier/reasoning-tier model, accept-edits mode, with `dotnet build`, `dotnet test`,
  and `dotnet format` pre-approved in `.claude/settings.json`.
- Guard in place before the first prompt: the `PreToolUse` hook `scripts/protect-tests.ps1`, which blocks
  edits to test files that exist in git and allows new ones. The `PostToolUse` format hook was added after
  the first green build.
- About 30 agent turns; the hook blocked one attempted test edit.

## Prompts used

```text
Read TASK.md. In Directory.Build.props turn on TreatWarningsAsErrors, AnalysisLevel latest-recommended,
and EnforceCodeStyleInBuild. Then make these gates pass, in order, stopping at the first red one:

  dotnet build
  dotnet test --solution Bookstore.slnx
  dotnet format Bookstore.slnx --verify-no-changes

Rules: never edit, skip, or delete a test, or change an expected value. No solution-wide analyzer
switch-offs; scope any exception and justify it in a comment. PriceCalculator must stay an injectable
instance class. Before changing code for the failing tests, group them by root cause and explain each
group. New errors after a fix may just mean another project now builds.
Finish by rerunning all three gates and pasting their summary lines.
```

```text
CA1822 on PriceCalculator.Calculate: don't make it static. TASK.md says the public shape stays the same,
and it's injected as a service. Suppress CA1822 on that one method with a SuppressMessage attribute and a
Justification.
```

## What review caught

- **8 failing tests, 3 root causes.** The agent's first grouping was right:
  - **Coupon lookup** (3 failures): the new `Dictionary` used the default, case-sensitive comparer and the
    code was never trimmed, so `"readmore"`, `" STUDENT10 "`, and `"student10\t"` were "unknown". Fix:
    `StringComparer.OrdinalIgnoreCase` and `Trim()`, plus `TryGetValue` (which also cleared CA1854).
  - **Bulk discount boundary** (1 failure): `Quantity > BulkQuantity` excluded exactly 10 copies; the rule
    is "10 or more". Fix: `>=`.
  - **ISBN normalization** (4 failures, two in `IsbnTests` and two in `BookLookupApiTests`): `Trim('-', ' ')`
    only removes hyphens and spaces at the **ends** of the string. Fix: restore `Replace` for both
    characters.
- **An attempted test edit.** On the CA1707 errors (underscores in test names), the agent first tried to
  rename methods in `PriceCalculatorTests.cs`. The hook blocked it; the agent then scoped CA1707 off for
  `tests/**.cs` in `.editorconfig` with a comment, which is the right fix.
- **CA1822 on `PriceCalculator.Calculate`.** The agent proposed making `Calculate` static. That would have
  broken the injected-service API (and the tests that call it on an instance). Redirected to a scoped
  `[SuppressMessage]` with a justification (second prompt).
- **Ordinal comparisons.** CA1310 flagged `StartsWith("978")`; the agent added `StringComparison.Ordinal`,
  which is correct for digits.
- **Two missing cancellation tokens** (CA2016) in `QuoteEndpoints` and the new lookup endpoint.

## Edited by hand

- Nothing. The `.vscode/settings.json` entries that make Copilot ask before editing `tests/**`, and before
  editing the guard and hook files (`scripts/`, `.github/hooks/`, `.claude/`), were added for teammates who
  use VS Code. They come after `"**/*": true`, because the last matching pattern decides.

## Verify

```powershell
dotnet build
dotnet test --solution Bookstore.slnx
dotnet format Bookstore.slnx --verify-no-changes
```
