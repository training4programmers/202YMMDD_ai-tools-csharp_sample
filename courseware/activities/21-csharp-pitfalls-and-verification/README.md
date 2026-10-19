# Activity 21: Verify, Then Gate

## Objective

An assistant added a supplier price import to the Bookstore API and suggested two follow-ups: a
package to convert ISBN-10s, and a CSV library. The build is green, with a single warning. Your job is
to treat all of it as unreviewed AI output: **verify** each suggested package with evidence, rebuild the
import so it meets the spec in `TASK.md` without new packages, **turn on the quality gates**
(warnings as errors, a stronger analyzer level, code style in the build, `dotnet format`), and finish
with zero warnings and green tests. Along the way you'll meet a hallucinated package, a real package
with the wrong API version, an obsolete API, culture bugs that only appear on a German server, and a
`!` that makes every valid file fail.

![Flow from "AI suggests dotnet add package X" through four checks: does it exist (search nuget.org with exact match), can you trust it (owner, downloads, repository, recent releases), does the API exist (compile a spike against that version), and is it allowed (approved list, license, source mapping), ending in adding it with a centrally pinned version and a lock file; any failed check leads to reject and report, with notes on slopsquatting and asking whether a package is needed at all](../../diagrams/png/12-verify-a-package.png)

*Figure: The four checks you'll record in `VERIFICATION.md`. Any failed check means reject and report.*

## Prerequisites

- .NET 10 SDK, Git, and Visual Studio 2026 or VS Code with C# Dev Kit
- One AI tool in agent mode: GitHub Copilot (Visual Studio 2026 or VS Code) or Claude Code
- Network access to nuget.org (for `dotnet package search` and a compile spike)
- Module 12 chapters 01–04, and [Demo 21](../../demos/21-csharp-pitfalls-and-verification/)
- Helpful: Activity 13 (the verify loop) and Activity 10 (context files)

## Skills Practiced

- Verifying an AI-suggested NuGet package: existence, publisher signals, API version, policy
- Compiling a throwaway spike to test a suggestion's code against the real package version
- Replacing a suggested dependency with BCL code and tests
- Finding culture bugs and proving the fix with a test under another culture
- Turning on `TreatWarningsAsErrors`, `AnalysisLevel`, `EnforceCodeStyleInBuild`, and opt-in CA rules
- Directing an agent to fix warnings rather than suppress them, and scoping the suppressions you keep
- Reviewing an agent's diff for scope creep, silent behavior changes, and new suppressions

## Instructions

Start the lab from the course repo root. This copies `begin/` to a folder outside the repo, makes it a
git repository, and commits a baseline, so `git diff` always shows exactly what you and your assistant
changed:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 21
cd ~\labs\activity-21-csharp-pitfalls-and-verification
```

Open the folder (or `Bookstore.slnx`) in Visual Studio 2026 or VS Code. To start over, re-run the
command with `-Force`. The reference in `solution/` is **one acceptable result**; yours will differ.

> **In class / take-home.** In class (about 25 minutes): steps 1 to 3, then start step 4. Take-home: the
> rest of step 4, steps 5 to 7 (the tests, the gates, and the final review), and the stretch goal.

1. **Read the brief.** Read `TASK.md` (the feature spec, the team's package policy, and the definition
   of done) and `ai-suggestion/SUGGESTION.md`. Then build and test the starting point, and note the
   one warning the build reports.
2. **See the bug for yourself.** Run the API and post `samples/supplier-prices.csv` to
   `POST /api/books/prices/import` (see *Expected behavior*). Before you open the code, predict why a
   valid file fails.
3. **Verify both suggested packages.** For each one, answer the five questions in
   `ai-suggestion/VERIFICATION.md` with evidence: the command you ran and what it printed, or the page
   you checked. For any package that exists, compile the suggestion's code against its current version
   in a throwaway project **outside the lab folder** (a file-based app with a `#:package` directive
   works well). Record a verdict for each. Don't add either package to the solution.
4. **Rebuild the import to the spec.** Meet every rule in `TASK.md` with no new packages. Constraints:
   - parsing and ISBN-10 conversion live in `Bookstore.Domain`, so they can be unit tested;
   - prices parse the same way in every culture;
   - all rows are matched against the catalog in one database query, filtered in SQL (don't load
     the whole `Books` table);
   - nothing is saved unless every row is valid, and a bad file gets a `400` that lists every bad row
     by line number;
   - the endpoint uses `TypedResults`, forwards the `CancellationToken`, and keeps a log line
     (structured, not interpolated);
   - no `!` to silence a nullable warning.
5. **Test it.** Add xUnit tests for: ISBN-10 conversion (including a final `x`), price parsing under
   `de-DE`, a rejected row, an ISBN that isn't in the catalog, and an API test that imports this week's
   list and checks the new prices. Existing tests stay as they are.
6. **Turn on the gates.** Add the gates from `TASK.md` to `Directory.Build.props` and `.editorconfig`,
   then get the build to zero warnings. Direct your assistant to fix code rather than suppress, and to
   stop and explain when a rule doesn't fit. For each suppression you keep, choose the narrowest scope
   and write the justification next to it.
7. **Review the whole diff before you call it done.** Run `git diff --stat` and look for files you
   didn't expect to change, changed tests, new suppressions, new packages, and any new `!`. Then run the
   build, the tests, and `dotnet format --verify-no-changes` yourself; don't rely on the agent's summary.

<details>
<summary>Prompting hints</summary>

- Ask for the exact command and its raw output when verifying a package, not a summary of it. Name the
  source (`https://api.nuget.org/v3/index.json`) and `--exact-match` so a near miss can't look like a
  hit.
- When a package exists, ask for the publisher signals the policy cares about (owner, downloads,
  repository, latest version and date, license), and for a compile check of the suggestion's own code.
- Point the assistant at `TASK.md` for the rules instead of retyping them, and state the constraints
  from step 4 that aren't in it (where the code lives, one SQL-filtered query, keep a log line).
- For culture, say which culture the test should run under and why ("." is a group separator in
  `de-DE`).
- For the gates, tell it the order of preference: fix the code, then a narrowly scoped suppression with
  a written reason, and stop and ask before suppressing anything.
- Say explicitly that existing tests and public types stay as they are unless you approve a change. An
  agent asked to "fix every warning" may decide to refactor a service it was only supposed to annotate.
</details>

## Requirements

- `ai-suggestion/VERIFICATION.md` has evidence and a verdict for both suggestions, including the
  compile result of the CsvHelper code against its current version.
- No new NuGet packages; `Directory.Packages.props` is unchanged.
- `POST /api/books/prices/import` meets every rule in `TASK.md`: ISBN-10 and ISBN-13 input, invariant
  prices, catalog matching, the 0.01–1000.00 range, all-or-nothing, and a `400` listing every bad row.
- Matching uses one query that filters in the database.
- The gates from `TASK.md` are on, including `CA1849` and `CA2000`; `dotnet build` reports
  **0 warnings**; `dotnet format --verify-no-changes` exits 0.
- Every suppression is scoped as narrowly as possible and has a written justification; no `<NoWarn>`.
- New tests cover the cases in step 5; no existing test was weakened, deleted, or rewritten.
- No `!` used to silence a nullable warning, and no interpolated log messages.

## Expected behavior

```powershell
dotnet build
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)

dotnet test --solution Bookstore.slnx
# Test run summary: Passed!
#   total: 19 plus your new tests, failed: 0

dotnet format --verify-no-changes
# (no output; $LASTEXITCODE is 0)

dotnet run --project src/Bookstore.Api
```

With the API running, from a second PowerShell 7 terminal:

```powershell
$api = 'http://localhost:5080/api/books'

Invoke-RestMethod "$api/prices/import" -Method Post -ContentType 'text/csv' -InFile samples/supplier-prices.csv
# updated
# -------
#       4
#  (on the starting code this request fails with a 500)

$books = Invoke-RestMethod $api
$books | Where-Object title -in 'The Quiet Compiler', 'Rivers of Glass' | Select-Object title, price
# title              price
# -----              -----
# Rivers of Glass    25.99
# The Quiet Compiler 19.49

$bad = "isbn,price`n9781234560034,35.00`n9781234560041,-1"
$result = Invoke-RestMethod "$api/prices/import" -Method Post -ContentType 'text/csv' -Body $bad -SkipHttpErrorCheck -StatusCodeVariable status
$status
# 400
$result.errors.csv
# Line 3: '-1' is not a price from 0.01 to 1000.00.
```

The exact error text is yours to choose; the status code, the line number, and "nothing saved" are the
requirement. (The API keeps its SQLite database between runs; delete `bookstore.db` in
`src/Bookstore.Api` to start from the seeded prices again.)

## Stretch goal

Make the supply chain itself a gate. Turn on `RestorePackagesWithLockFile`, commit the generated
`packages.lock.json` files, and restore with `--locked-mode`. Then add rules to `AGENTS.md` (or
`CLAUDE.md` / `.github/copilot-instructions.md`) that require the assistant to verify any package with
`dotnet package search` before proposing it, and back them with your tool's approval settings for
`dotnet add package`. Ask your assistant to add a CSV library and see whether it follows the rules.
(Leave `solution/` as is; this is extra practice.)

When you're done, email your instructor.

## Reference

- Concepts: [Module 12: C#-Specific Pitfalls and Verification](../../docs/module-12-csharp-pitfalls-and-verification/),
  especially [Hallucinated NuGet Packages and API Versions](../../docs/module-12-csharp-pitfalls-and-verification/01-hallucinated-nuget-packages-and-api-versions.md),
  [Nullable and Analyzer Warnings as Quality Gates](../../docs/module-12-csharp-pitfalls-and-verification/03-nullable-and-analyzer-warnings-as-quality-gates.md), and
  [EditorConfig, Analyzers, and dotnet format](../../docs/module-12-csharp-pitfalls-and-verification/04-editorconfig-analyzers-and-dotnet-format.md)
- Worked example: [matching demo](../../demos/21-csharp-pitfalls-and-verification/)
