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

   <details>
   <summary>Guided help: step 1</summary>

   Run these from the lab folder, and read both files before you ask the assistant anything:

   ```powershell
   dotnet build
   # Build succeeded. 1 Warning(s): SYSLIB0020 in SupplierPriceEndpoints.cs
   # ('JsonSerializerOptions.IgnoreNullValues' is obsolete)

   dotnet test --solution Bookstore.slnx
   # Test run summary: Passed!  total: 19, failed: 0
   ```

   **Look for:** in `TASK.md`, the import rules (ISBN-10 and ISBN-13 input, `.` as the decimal separator,
   0.01 to 1000.00, all or nothing), the package policy (only `Microsoft.*` and the existing test
   packages are pre-approved), and the four gates. In `SUGGESTION.md`, the two package IDs and the exact
   code each would add. The green tests prove little: none of the 19 calls the import endpoint.

   </details>

2. **See the bug for yourself.** Run the API and post `samples/supplier-prices.csv` to
   `POST /api/books/prices/import` (see *Expected behavior*). Before you open the code, predict why a
   valid file fails.

   <details>
   <summary>Guided help: step 2</summary>

   Two terminals, both in the lab folder. Write your prediction down first, then check it in the code.

   ```powershell
   # Terminal 1
   dotnet run --project src/Bookstore.Api

   # Terminal 2
   $api = 'http://localhost:5080/api/books'
   Invoke-RestMethod "$api/prices/import" -Method Post -ContentType 'text/csv' `
       -InFile samples/supplier-prices.csv -SkipHttpErrorCheck -StatusCodeVariable status
   $status
   # 500
   ```

   Terminal 1 logs a `System.NullReferenceException`. **Now read** `ImportPricesAsync` in
   `src/Bookstore.Api/Endpoints/SupplierPriceEndpoints.cs`, and compare the file's ISBNs
   (`9781234560010`) with how the catalog stores them (`978-1-23456-001-0`). What does
   `FirstOrDefaultAsync` return, and what does the `!` on the next line hide? While you're there, ask
   what `decimal.Parse(parts[1])` would do on a server whose culture is `de-DE`.

   **If you're stuck on the prediction,** ask in a read-only mode (Copilot Ask mode; Claude Code plan
   mode):

   ```text
   Don't change anything. Posting samples/supplier-prices.csv to POST /api/books/prices/import returns
   500 with a NullReferenceException. Walk through ImportPricesAsync for the first data row and tell me
   which line throws and why. Then list every other line that would misbehave on a de-DE server.
   ```

   </details>

3. **Verify both suggested packages.** For each one, answer the five questions in
   `ai-suggestion/VERIFICATION.md` with evidence: the command you ran and what it printed, or the page
   you checked. For any package that exists, compile the suggestion's code against its current version
   in a throwaway project **outside the lab folder** (a file-based app with a `#:package` directive
   works well). Record a verdict for each. Don't add either package to the solution.

   <details>
   <summary>Guided help: step 3</summary>

   An agent mode, so the assistant can run the searches; approve each command yourself, and say no to
   any `dotnet add package` in the lab folder.

   > **GitHub Copilot:** Agent mode; attach `TASK.md` and both `ai-suggestion/` files with `#file`.
   >
   > **Claude Code:** Manual mode (`default`), which asks before each command.

   ```text
   Read TASK.md and ai-suggestion/SUGGESTION.md. Don't change any code and don't add a package to this
   solution. For Isbn13.Net and for CsvHelper, show me every command you run with its raw output:
   1) Does it exist? dotnet package search <id> --exact-match --source https://api.nuget.org/v3/index.json
   2) If it does: owner and total downloads (dotnet package search <id> --take 1), and from its nuget.org
      page the source repository, license, latest stable version, and when that version was published.
   3) Does the suggestion's code compile against that version? Use a file-based app in ~\spikes\csvhelper,
      outside this folder: spike.cs with #:package CsvHelper@<latest version> and the suggestion's code,
      then dotnet build spike.cs and dotnet package list --file spike.cs --vulnerable --include-transitive.
   4) Is it allowed by the package policy in TASK.md, and do we need it at all?
   Then fill in ai-suggestion/VERIFICATION.md with the evidence and a verdict for each.
   ```

   **Check the evidence yourself** (the figures were current at the time of writing; yours will be newer):

   ```powershell
   dotnet package search Isbn13.Net --exact-match --source https://api.nuget.org/v3/index.json
   # No results found.
   dotnet package search CsvHelper --take 1 --source https://api.nuget.org/v3/index.json
   # | Package ID | Latest Version | Owners    | Total Downloads |
   # | CsvHelper  | 34.0.0         | joshclose | 721,960,654     |
   ```

   Open `https://www.nuget.org/packages/CsvHelper` for the repository, license, and version history. The
   spike must fail: against 34.0.0 with `CS1729` (no one-argument `CsvReader` constructor) and two
   `CS1061` (no `Configuration`); against 33.1.0 with `CS1503` and two `CS0200`. Delete `~\spikes` after.

   **If it doesn't:** a summary without output ("CsvHelper is widely used") gets "Paste the exact command
   and what it printed." An offer of a different ISBN package gets "What was `IsbnConverter.ToIsbn13`
   supposed to do, and can `Isbn.cs` do it in a few lines?" A spike that fails with `NU1008` sits inside
   a folder with a `Directory.Packages.props`; move it.

   </details>

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

   <details>
   <summary>Guided help: step 4</summary>

   Get a plan in a read-only mode, check it, then implement in an agent mode.

   > **GitHub Copilot:** Ask mode (or the Plan agent in VS Code) for the plan, then Agent mode; attach
   > `TASK.md`, `SupplierPriceEndpoints.cs`, and `src/Bookstore.Domain/Isbn.cs`.
   >
   > **Claude Code:** plan mode (Shift+Tab until the footer shows it), then accept the plan.

   ```text
   Rebuild POST /api/books/prices/import (src/Bookstore.Api/Endpoints/SupplierPriceEndpoints.cs) to meet
   every rule in TASK.md, with no new packages and no change to existing tests or public types:
   - Parsing and ISBN-10 to ISBN-13 conversion go in Bookstore.Domain (for example a SupplierPriceList
     class and an Isbn.TryToIsbn13 method), so they can be unit tested without the API.
   - Prices parse with CultureInfo.InvariantCulture; every bad row is reported by its line number.
   - Match all rows against the catalog in one query that filters in the database; never load the whole
     Books table. The catalog stores ISBN-13s with hyphens, so compare digits only.
   - Validate the whole file before saving; any bad row means a 400 validation problem that lists every
     bad row, and nothing is saved.
   - TypedResults like the other endpoints, the CancellationToken passed to every async call, and one
     log line as a [LoggerMessage] method, not an interpolated string.
   - No ! to silence a nullable warning.
   Show me the plan first. Then implement it and run dotnet build and dotnet test --solution Bookstore.slnx.
   ```

   **Check the plan and the diff:** no `db.Books.ToListAsync()` followed by matching in memory; one
   `SaveChangesAsync(cancellationToken)`, after validation; the log line is still there. Then repeat the
   request from step 2: it now returns `updated` `4`.

   **If it loads the whole table** "because the catalog is small": "Nothing guarantees that. Filter in SQL
   on the normalized ISBNs with Contains, and show me the generated SQL with ToQueryString()." **If the
   log line is gone:** "Keep the log line, as a [LoggerMessage] method with the count as a placeholder."

   </details>

5. **Test it.** Add xUnit tests for: ISBN-10 conversion (including a final `x`), price parsing under
   `de-DE`, a rejected row, an ISBN that isn't in the catalog, and an API test that imports this week's
   list and checks the new prices. Existing tests stay as they are.

   <details>
   <summary>Guided help: step 5</summary>

   The same agent session. Name every case, and the culture, so nothing is left to guesswork.

   ```text
   Add xUnit tests in new files under tests/Bookstore.Tests; don't change the existing test files.
   - ISBN-10 conversion: 123456002x (a final lowercase x) becomes 9781234560027; malformed values are
     rejected.
   - Price parsing under de-DE, where "." is a group separator: set CultureInfo.CurrentCulture to de-DE
     in the test, parse a list with the price 19.49, expect 19.49m, and restore the culture afterward.
   - A rejected row (line 3 has the price -1) and an ISBN that isn't in the catalog: each returns 400 and
     leaves every price unchanged.
   - An API test with BookstoreApiFactory that imports the rows of samples/supplier-prices.csv and checks
     the four new prices through GET /api/books.
   Run dotnet test --solution Bookstore.slnx and show me the summary.
   ```

   **Check:** `total:` is 19 plus your new tests, with `failed: 0` (the reference has 47), and
   `git diff --stat -- tests` prints nothing, because only new files were added. To prove the `de-DE`
   test tests something, remove `CultureInfo.InvariantCulture` from the parser for a moment: the test
   must fail. Put it back.

   **If an existing test changed:** "Revert your edits to the existing test files and put the new tests
   in new files."

   </details>

6. **Turn on the gates.** Add the gates from `TASK.md` to `Directory.Build.props` and `.editorconfig`,
   then get the build to zero warnings. Direct your assistant to fix code rather than suppress, and to
   stop and explain when a rule doesn't fit. For each suppression you keep, choose the narrowest scope
   and write the justification next to it.

   <details>
   <summary>Guided help: step 6</summary>

   Agent mode. State the order of preference, and make the assistant stop before any suppression.

   ```text
   Turn on the quality gates from TASK.md: TreatWarningsAsErrors, AnalysisLevel latest-recommended, and
   EnforceCodeStyleInBuild in Directory.Build.props, and CA1849 and CA2000 at warning severity in
   .editorconfig. Then get dotnet build to zero warnings by changing code. Before you suppress anything,
   stop and tell me the rule, the line, and why a code fix would be wrong; I'll decide. A suppression I
   approve gets the narrowest scope (a [SuppressMessage] on one member with a Justification, or one
   .editorconfig section with a comment), never <NoWarn>. Don't change public types, DI registrations,
   or existing tests to make a warning go away. Finish with dotnet build, dotnet test --solution
   Bookstore.slnx, and dotnet format --verify-no-changes.
   ```

   **Decisions you'll probably face** (the reference made these):
   - `CA1822` wants `PriceCalculator.Calculate` to be static. It's a service the API injects, so it stays
     an instance method, with a member-scoped `[SuppressMessage]` and a written justification.
   - `CA1707` flags the underscores in test names: turn it off for `[tests/**/*.cs]` only, with a comment.
   - `CA1849` flags `app.Run()` in `Program.cs`: the fix is `await app.RunAsync()`.

   **If it goes wrong:** an agent that makes `PriceCalculator` static, drops its DI registration, or edits
   `PriceCalculatorTests` has refactored instead of fixing a warning. Reject it: "Revert PriceCalculator,
   QuoteEndpoints, and the tests. Suppress CA1822 on Calculate only, with a justification."

   </details>

7. **Review the whole diff before you call it done.** Run `git diff --stat` and look for files you
   didn't expect to change, changed tests, new suppressions, new packages, and any new `!`. Then run the
   build, the tests, and `dotnet format --verify-no-changes` yourself; don't rely on the agent's summary.

   <details>
   <summary>Guided help: step 7</summary>

   Do this one yourself. Stage everything first, so new files show up in the diff too:

   ```powershell
   git add -A
   git diff --staged --stat                 # every file that changed; read each one
   git diff --staged --stat -- Directory.Packages.props '*.csproj'
   # (no output: no new packages)
   git diff --staged -U0 | Select-String '^\+.*(SuppressMessage|#pragma warning|NoWarn|severity = none)'
   # one line per suppression you approved, each with its justification next to it
   git diff --staged -U0 -- '*.cs' | Select-String '^\+.*[\w\)\]]!(?!=)'
   # (no output: no new null-forgiving !)

   dotnet build                             # 0 Warning(s), 0 Error(s)
   dotnet test --solution Bookstore.slnx    # failed: 0
   dotnet format --verify-no-changes        # no output
   $LASTEXITCODE                            # 0
   ```

   **Look for:** a test file you didn't expect to change, a service that became `static`, a log line that
   disappeared, and a duplicate row (the same book as an ISBN-10 and an ISBN-13) that silently wins.
   Anything you can't explain goes back to the assistant: "Why did this file change? Revert it unless
   the task needed it."

   </details>

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

## Reference

- Concepts: [Module 12: C#-Specific Pitfalls and Verification](../../docs/module-12-csharp-pitfalls-and-verification/),
  especially [Hallucinated NuGet Packages and API Versions](../../docs/module-12-csharp-pitfalls-and-verification/01-hallucinated-nuget-packages-and-api-versions.md),
  [Nullable and Analyzer Warnings as Quality Gates](../../docs/module-12-csharp-pitfalls-and-verification/03-nullable-and-analyzer-warnings-as-quality-gates.md), and
  [EditorConfig, Analyzers, and dotnet format](../../docs/module-12-csharp-pitfalls-and-verification/04-editorconfig-analyzers-and-dotnet-format.md)
- Worked example: [matching demo](../../demos/21-csharp-pitfalls-and-verification/)
