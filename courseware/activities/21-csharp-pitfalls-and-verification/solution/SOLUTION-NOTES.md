# Activity 21: Solution Notes

> This is **one acceptable result**, produced with an AI assistant and then reviewed by a person. Your
> prompts, your assistant's output, and your final code will differ. That's expected, as long as you
> meet the activity's Requirements.

## How this result was produced

- **Tool and mode:** Claude Code, one agent session continued across three prompts, edits
  auto-accepted, shell access limited to `dotnet`, `git diff`/`git status`, and read-only commands.
- **Model tier:** frontier.
- **Iterations:** three prompts. Verification took about 3.5 minutes, the implementation about 14
  (much of it spent working around an environment problem, below), and the gates about 3. Then a
  person reviewed the diff and reworked about a third of it by hand.
- The recorded run's `ai-suggestion/VERIFICATION.md` was kept almost as written; the code in this
  folder is the reviewed version, not the agent's.

## Prompts used

```text
Read TASK.md and ai-suggestion/SUGGESTION.md. Before changing any code, verify both suggested packages. For each one: does it exist on nuget.org (dotnet package search with --exact-match)? If it exists, who owns it, how many downloads, what's the latest version and when was it published, and where's the source? Would the code in the suggestion compile against the latest version? Check that in a throwaway project outside this folder, not here. Apply the package policy in TASK.md and fill in ai-suggestion/VERIFICATION.md with what you ran, what it printed, and a verdict for each. Don't add any package to this solution.
```

```text
Now implement the import so it meets every rule in TASK.md with no new packages. Put the parsing and the ISBN-10 conversion in Bookstore.Domain so they can be unit tested, parse prices with the invariant culture, look up all the rows in one query, validate the whole file before saving anything, and use TypedResults like the other endpoints. Add xUnit tests, including one that parses prices under de-DE and an API test that imports samples/supplier-prices.csv. Run dotnet build and dotnet test --solution Bookstore.slnx and fix any failures.
```

```text
Turn on the quality gates from TASK.md in Directory.Build.props and .editorconfig, then fix every warning. Don't suppress a warning unless fixing it would be wrong; if you do, scope the suppression as narrowly as possible and write the justification next to it. Finish with dotnet build (zero warnings), dotnet test --solution Bookstore.slnx, and dotnet format --verify-no-changes.
```

## What review caught

- **Verification was right.** The agent found no `Isbn13.Net` (and called it hallucinated), confirmed
  CsvHelper is real and reputable, and proved with a throwaway project that the suggested CsvHelper
  code fails against 33.1.0 (`CS1503`, two `CS0200`). It also read the package policy correctly.
  Kept, with light edits.
- **"One query" loaded the whole table.** The agent's endpoint ran `db.Books.ToListAsync()` and matched
  ISBNs in memory, with a comment that "the catalog is small." Nothing guarantees that. The reviewed
  version filters in SQL on normalized ISBNs, so only the listed books are loaded:
  `WHERE replace(replace("b"."Isbn", '-', ''), ' ', '') IN (...)` (checked with `ToQueryString()`).
  That expression can't use the ISBN index; if the catalog grows large, store a normalized ISBN column.
- **A refactor nobody asked for.** On the gates prompt, the agent replaced its own justified
  `CA1822` suppression with a "fix": it made `PriceCalculator` a static class, removed its DI
  registration, changed `QuoteEndpoints`, and edited the existing `PriceCalculatorTests`. That's a
  design change to a service the API injects, smuggled into a warnings task, and it changed tests that
  weren't broken. Rejected: `Calculate` stays an instance method with a member-scoped
  `[SuppressMessage]` and a written justification.
- **The log line disappeared.** Instead of converting the interpolated `LogInformation` (`CA2254`,
  `CA1848`, `CA1873`) to a `[LoggerMessage]` method, the rewrite simply dropped it. An import that
  changes prices should leave a trace; restored as `LogPricesUpdated`.
- **Duplicate rows were accepted.** The agent's parser let the same book appear twice (as an ISBN-10 on
  one line and an ISBN-13 on another), and the last price silently won. The reviewed parser reports the
  duplicate by line number.
- **An unexpected file in the diff.** Early on, the agent overwrote `.editorconfig` (it missed the
  dotfile in its first listing), noticed via `git status`, and restored it. It said so, which is good;
  `git diff --stat` before accepting any agent change is how you'd catch it if it hadn't.
- **Writing outside the workspace.** The recording ran in a very deep folder, where the SQLite native
  DLL path exceeded Windows' 260-character limit and the API tests couldn't load it. The agent worked
  around it by building to new folders at the root of `C:`, outside the lab. An allowed `dotnet`
  command can still write anywhere; those folders were deleted afterward. Start labs in `~\labs` (as
  `start-lab.ps1` does) and this doesn't come up.
- **Checked and fine:** invariant-culture price parsing with a `de-DE` test; ISBN-10 conversion with
  `X` and `x`; all-or-nothing validation; `TypedResults`; the forwarded `CancellationToken`;
  `await app.RunAsync()` for `CA1849`; `CA1707` turned off for `tests/**` only, with a comment; no new
  packages; no `!`.

## Edited by hand

- **`SupplierPriceEndpoints`:** the SQL-side ISBN filter, the restored `[LoggerMessage]` log line, and
  one `csv` key for all validation errors (the agent keyed them `line N`; either works, one key is easier
  for clients to read).
- **`SupplierPriceList`:** duplicate detection; a literal "0.01 to 1000.00" in the error message (an
  interpolated `decimal` there would print `0,01` on a German server).
- **`Isbn.TryToIsbn13`:** one method with `[NotNullWhen(true)]` instead of separate `IsValidIsbn10` and
  `ToIsbn13` methods, so callers get flow analysis instead of a convert-without-checking path.
- **`PriceCalculator`:** reverted the static refactor and kept the original tests untouched (see above).
- **Tests:** the API test posts the sample rows inline instead of copying `samples/supplier-prices.csv`
  into the test output folder; the bad-row and unknown-ISBN tests check that an unrelated book's price is
  unchanged.
- **`ai-suggestion/VERIFICATION.md`:** tidied the agent's version and recorded what we built instead.

## Verify

```powershell
dotnet build
dotnet test --solution Bookstore.slnx
dotnet format --verify-no-changes
# Build succeeded: 0 Warning(s), 0 Error(s)
# Test run summary: Passed!  total: 47 (19 existing + 13 ISBN + 12 price list + 3 API)
# dotnet format: no output, exit code 0
```
