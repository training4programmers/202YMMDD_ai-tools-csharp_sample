# Bonus Activity 09: Solution Notes

> This is **one acceptable result**, produced with an AI assistant and then reviewed by a person. Your
> prompts, your assistant's output, and your final code will differ. That's expected, as long as you
> meet the activity's Requirements.

## How this result was produced

- **Tool and mode:** Claude Code, one agent session continued across three prompts, edits
  auto-accepted, shell access limited to `dotnet`, `git diff`/`git status`, and read-only commands.
- **Model tier:** frontier.
- **Iterations:** three prompts of about 2, 3, and 4 minutes, each including a `--job short` benchmark
  run. Then a person reviewed the diff, reworked parts of it by hand, and ran the full-length benchmark
  recorded in [`BENCHMARKS.md`](BENCHMARKS.md).
- The code in this folder is the reviewed version. It keeps the recording's approach (index the
  catalog once, scan with plain loops, park the faster index as a candidate) and differs where review
  said so (below).

## Prompts used

```text
Add a BenchmarkDotNet project at benchmarks/Bookstore.Benchmarks (add it to Bookstore.slnx, and put the package version in Directory.Packages.props) that measures KeywordSearch.Search over a generated 5,000-book catalog for three typical queries, with [MemoryDiagnoser]. Run it in Release with --job short and show me the results. Don't change KeywordSearch yet.
```

```text
Now optimize KeywordSearch without changing what Search returns for any query: the same books in the same order. First keep a copy of the current implementation in the benchmarks project as the baseline, and add an xUnit test that compares baseline and optimized over a generated catalog and many queries. Then optimize, run dotnet test --solution Bookstore.slnx, and rerun the benchmark comparing baseline and optimized in Release with --job short.
```

```text
Can it go faster still? Propose one more optimization, implement it as a separate candidate class in the benchmarks project (not in KeywordSearch), and benchmark it against the optimized version in Release with --job short. Tell me whether it's worth keeping, based on the numbers.
```

## What review caught

- **A stale package version, from memory.** The agent pinned `BenchmarkDotNet` 0.15.2 in
  `Directory.Packages.props`. The latest stable version is 0.15.8
  (`dotnet package search BenchmarkDotNet --exact-match`). It's Module 12's chapter 01 lesson in
  miniature: a real package, a plausible version, never checked. Fixed.
- **A quiet change to the sort.** The agent pre-sorted the catalog by title when the search was
  built, so results came out already in order. That moves `OrderBy(b => b.Title)` (which compares with
  the *current culture*) from search time to construction time: the same object built under one culture
  and searched under another, as happens with per-request localization, could order results
  differently from the original. The reviewed version builds the word index once but still sorts the
  (few) matches at search time, with the same comparer as before.
- **Snapshot semantics, flagged by the agent itself.** The optimized search indexes the catalog once,
  so changes to the source collection after construction aren't seen; the original re-read it on every
  call. The agent raised this unprompted, which is exactly what you want. It matches the class's
  "catalog snapshot" contract, so it's kept and documented in the XML remarks.
- **The test project referenced the benchmark executable.** That works, but drags BenchmarkDotNet into
  the test build. The reviewed version links just the reference implementation and the sample catalog
  into the test project.
- **The candidate was right, and so was the verdict.** The agent's inverted-index candidate was 4 to 7
  times faster than the optimized scan in its short run, and it still recommended *not* adopting it:
  about 35 microseconds per search over 5,000 books is already noise next to a web request, and the
  index is far more code to keep correct. Agreed. The reviewed copy (without the construction-time
  sort) stays in the benchmark project, covered by the equivalence test, with the decision recorded in
  `BENCHMARKS.md`.
- **Short-job numbers.** All three of the agent's tables came from `--job short` (three iterations,
  wide error bars), which it said itself. `BENCHMARKS.md` records a default-job run.
- **Checked and fine:** the baseline copy was verbatim; the equivalence test compared whole result
  lists, not counts; ordinal `StartsWith` and `ToLowerInvariant` semantics were preserved; the public API
  and the existing tests were unchanged.

## Edited by hand

- `KeywordSearch`: the reviewed version described above (index once in the constructor, sort matches at
  search time, `ArgumentNullException` guards, `sealed`).
- `benchmarks/Bookstore.Benchmarks`: `SampleCatalog` (5,000 books, fixed seed), three queries as
  `[Params]`, the original under `Candidates/OriginalKeywordSearch.cs`, and the adapted
  `Candidates/IndexedKeywordSearch.cs`.
- `tests/Bookstore.Tests/KeywordSearchEquivalenceTests.cs`: compares the original with both the
  optimized search and the candidate over every word, one- and two-letter prefix, and word pair in a
  2,000-book catalog, plus edge cases and equal titles.
- `.gitignore`: `BenchmarkDotNet.Artifacts/`, so benchmark output stays out of commits.
- `BENCHMARKS.md`: the full Release run and the decisions.

## Verify

```powershell
dotnet build
dotnet test --solution Bookstore.slnx
# Build succeeded: 0 Warning(s), 0 Error(s)
# Test run summary: Passed!  total: 33 (19 baseline + 11 KeywordSearchTests + 3 equivalence)

dotnet run -c Release --project benchmarks/Bookstore.Benchmarks -- --filter '*'
```
