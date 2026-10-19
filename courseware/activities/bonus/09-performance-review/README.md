# Bonus Activity 09: Measure Before You Optimize

## Objective

An assistant wrote `KeywordSearch`, the type-ahead search behind a "find a book" box: a book matches
when every word you type is the start of a word in its title or its author's name. It's correct and
readable, and it allocates on every book for every keystroke. Your job is to **measure it first**,
optimize it with your assistant, and **prove** two things with evidence: the new version is faster
(BenchmarkDotNet, Release build, a baseline), and it returns exactly the same books in exactly the same
order (an equivalence test against the original). Any "optimization" that doesn't measurably help gets
rejected, however clever it looks.

## Prerequisites

- .NET 10 SDK, Git, and Visual Studio 2026 or VS Code with C# Dev Kit
- One AI tool in agent mode: GitHub Copilot or Claude Code
- Network access to nuget.org (to restore BenchmarkDotNet)
- Module 12 [chapter 05](../../../docs/module-12-csharp-pitfalls-and-verification/05-performance-review.md),
  and [Bonus Demo 09](../../../demos/bonus/09-performance-review/)
- About 15 minutes of machine time for benchmark runs; plug in your laptop

## Skills Practiced

- Writing a BenchmarkDotNet benchmark with a baseline, `[MemoryDiagnoser]`, and `[Params]`
- Reading Mean, Error, Ratio, and Allocated, and telling a real gain from noise
- Keeping the original implementation as a benchmark baseline and a test oracle
- Writing an equivalence test that compares old and new behavior over many inputs
- Directing an assistant to explain costs before changing code, and to measure after
- Rejecting optimizations that don't pay for themselves

## Instructions

Start the lab from the course repo root. This copies `begin/` to a folder outside the repo, makes it a
git repository, and commits a baseline, so `git diff` always shows exactly what you and your assistant
changed:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 09 -Bonus
cd ~\labs\bonus-activity-09-performance-review
```

Open the folder (or `Bookstore.slnx`) in Visual Studio 2026 or VS Code. To start over, re-run the
command with `-Force`. The reference in `solution/` is **one acceptable result**; yours will differ.

1. **Read the code.** Open `src/Bookstore.Domain/KeywordSearch.cs` and `tests/Bookstore.Tests/KeywordSearchTests.cs`.
   Write down, before measuring, which lines you think cost the most. Build and run the tests.

   <details>
   <summary>Guided help: step 1</summary>

   No assistant yet: your own guess is what you'll check the benchmark against.

   ```powershell
   dotnet build
   # Build succeeded. 0 Warning(s)

   dotnet test --solution Bookstore.slnx
   # Test run summary: Passed!  total: 30 (11 of them in KeywordSearchTests)
   ```

   **Look for:** for each line in `Search`, how often it runs (once per search, once per book, or once
   per book per keyword) and whether it allocates. Candidates: `_books.ToList()`, the string
   concatenation and `ToLowerInvariant()`, `Split(' ').ToList()`, the `words.Any(...)` lambda, the
   loop that keeps counting after a keyword misses, and the final `OrderBy`/`ThenBy`/`ToList`. Rank
   your top three and write them down.

   </details>

2. **Create a benchmark project.** Add `benchmarks/Bookstore.Benchmarks` to `Bookstore.slnx`:
   a console project that references `Bookstore.Domain`, with the BenchmarkDotNet version pinned in
   `Directory.Packages.props` (verify the package first, as in Module 12 chapter 01). Benchmark
   `KeywordSearch.Search` over a generated catalog of about 5,000 books (a fixed random seed, so every
   run measures the same work), for at least three realistic queries. Build the search object outside
   the measurement.

   <details>
   <summary>Guided help: step 2</summary>

   Check the package yourself first, so the version in your prompt comes from the feed, not from a
   model's memory:

   ```powershell
   dotnet package search BenchmarkDotNet --exact-match --source https://api.nuget.org/v3/index.json
   # one row per version; the last row is the latest stable (0.15.8 at the time of writing)
   ```

   Then use an agent mode (Copilot Agent mode, or a Claude Code session):

   ```text
   Add a console project at benchmarks/Bookstore.Benchmarks that references src/Bookstore.Domain, and
   add it to Bookstore.slnx. Put BenchmarkDotNet <version from the search> in Directory.Packages.props;
   the .csproj gets a PackageReference with no version. Add a SampleCatalog.Generate(count, seed) that
   builds about 5,000 books with authors from fixed word lists and new Random(seed), and a benchmark
   class with [MemoryDiagnoser] and three queries as [Params]: a common word, a two-word prefix, and an
   author's full name, each of which matches some generated books. Create the KeywordSearch in a field,
   outside the measured method. Program.cs uses BenchmarkSwitcher so --filter works. Don't change
   KeywordSearch or any test. Run dotnet build and dotnet test --solution Bookstore.slnx.
   ```

   **Check:** `dotnet build` has no warnings; `dotnet test` still reports 30 (the benchmark project
   isn't a test project); the `.csproj` has `<PackageReference Include="BenchmarkDotNet" />` with no
   `Version`. **If the version differs from your search** (the recorded run pinned 0.15.2 from memory),
   correct it in `Directory.Packages.props`.

   </details>

3. **Measure the baseline in Release** and save the summary table. Compare it with your guesses from
   step 1.

   <details>
   <summary>Guided help: step 3</summary>

   Run it yourself in a terminal, from the solution folder, with other apps closed and the laptop
   plugged in. A default run takes a few minutes; a Debug build stops with an error instead of numbers.

   ```powershell
   dotnet run -c Release --project benchmarks/Bookstore.Benchmarks -- --filter '*'
   # a summary table with Mean, Error, StdDev, Gen0, and Allocated: one row per query

   Copy-Item BenchmarkDotNet.Artifacts\results\*-report-github.md before.md
   ```

   The `-report-github.md` file holds the environment (BenchmarkDotNet version, OS, CPU, .NET SDK and
   runtime) and the table, ready for `BENCHMARKS.md`. The next run overwrites it, so keep the copy.
   (`BenchmarkDotNet.Artifacts/` is already in `.gitignore`.)

   **Compare with your guesses:** Allocated is per search. Megabytes per search over 5,000 books means
   allocations per book dominate. Which of your top three explains that?

   </details>

4. **Keep the original.** Copy the current `KeywordSearch` into the benchmark project (renamed) so it
   stays the benchmark baseline, and add an xUnit **equivalence test** that compares the original and
   your new version over a generated catalog and many queries: whole words, one- and two-letter
   prefixes, mixed case, extra spaces, multiple words, and queries that match nothing.

   <details>
   <summary>Guided help: step 4</summary>

   Do this before any optimization: today both classes are identical, so the test must pass now.

   ```text
   Copy src/Bookstore.Domain/KeywordSearch.cs verbatim to
   benchmarks/Bookstore.Benchmarks/Candidates/OriginalKeywordSearch.cs, renaming only the class and its
   namespace (Bookstore.Benchmarks.Candidates). Benchmark it in a new [Benchmark(Baseline = true)]
   method next to the KeywordSearch one. Add tests/Bookstore.Tests/KeywordSearchEquivalenceTests.cs.
   Link the original and SampleCatalog.cs into the test project with <Compile Include="..."
   Link="..." /> instead of referencing the benchmark project. Generate a catalog with a different
   seed, and assert that the original and KeywordSearch return the same book ids in the same order for
   every word in the catalog, its one-letter prefix, its two-letter prefix upper-cased, pairs of words,
   "", "   ", a word that matches nothing, a repeated word, and a query with extra spaces. Compare
   whole id lists, not counts. Run dotnet test --solution Bookstore.slnx.
   ```

   **Check:** the test passes, and the copy is verbatim apart from the names.

   ```powershell
   git diff --no-index src/Bookstore.Domain/KeywordSearch.cs `
       benchmarks/Bookstore.Benchmarks/Candidates/OriginalKeywordSearch.cs
   # only comments, the namespace (plus a using Bookstore.Domain;), and the class and constructor names
   ```

   **If it doesn't:** if the assistant "tidied" the copy, ask it to restore it exactly; the baseline is
   evidence. Also ask for a case with two books with the same title, so ties (title, then id) are tested.

   </details>

5. **Ask for the problems, ranked, before any changes.** Then optimize with your assistant. Constraints:
   the public API stays the same (`new KeywordSearch(books)`, `Search(query)` returning `List<Book>`),
   results and their order stay identical, and the existing tests don't change.

   <details>
   <summary>Guided help: step 5</summary>

   Ask read-only first (Copilot Ask mode; Claude Code plan mode, Shift+Tab until the footer shows it),
   and paste your `before.md` table:

   ```text
   Read src/Bookstore.Domain/KeywordSearch.cs and the benchmark table below. Don't change any code. Rank
   the performance problems in Search, most expensive first. For each: the line, whether it runs per
   search, per book, or per book per keyword, what it allocates, and whether it shows in Mean,
   Allocated, or both.
   ```

   Compare its ranking with yours, then switch to an agent mode:

   ```text
   Optimize KeywordSearch, biggest problem first. Keep the public API:
   new KeywordSearch(IEnumerable<Book>) and Search(string) returning List<Book>. Every query must
   return the same books in the same order (title with today's comparer, then id), with the same case
   handling and treatment of extra spaces. Don't change KeywordSearchTests or
   KeywordSearchEquivalenceTests. Doing per-book work once in the constructor is fine; sorting the
   catalog in the constructor isn't, because the title comparer uses the culture current at search
   time. Run dotnet build and dotnet test --solution Bookstore.slnx.
   ```

   **Check:** all tests pass; `git diff --stat -- tests/Bookstore.Tests/KeywordSearchTests.cs` prints
   nothing; `OrderBy(b => b.Title).ThenBy(b => b.Id)` still runs in `Search`, over the matches only. If
   the constructor now copies the books, later changes to the catalog aren't seen: make sure the XML
   docs say so.

   **If the equivalence test fails:** paste its output and ask for the first query that differs, and
   why, before any fix.

   </details>

6. **Measure again** with the same benchmark, now comparing original and optimized. Record both tables
   in `BENCHMARKS.md` at the solution root, with your machine, the .NET version, and the job you used.

   <details>
   <summary>Guided help: step 6</summary>

   Same command, same conditions, default job (not `--job short`) for the numbers you record:

   ```powershell
   dotnet run -c Release --project benchmarks/Bookstore.Benchmarks -- --filter '*'
   # | Method    | Query | Mean | Error | ... | Ratio | ... | Allocated | Alloc Ratio |
   # | Original  | ...   |      |       |     |  1.00 |     |           |        1.00 |
   # | Optimized | ...   |      |       |     |  0.xx |     |           |        0.xx |
   ```

   What `BENCHMARKS.md` needs, in order:

   ```text
   A title; the date, "Release build", the job (default), and the exact command you ran
   The environment block from the report: BenchmarkDotNet version, OS, CPU, .NET SDK and runtime
   Before: the table from before.md
   After: this run's table, with Original as the baseline and Optimized
   Decisions: a table with Version, Verdict, and Why (step 7 adds the candidate's row)
   ```

   **Check:** for each query, Optimized's Ratio and Alloc Ratio are well below 1.00, and the gap between
   the two Means is much larger than their Error values. (The reference was about 26 times faster for
   the selective queries and 4 times for `the`; your machine will differ, so compare ratios.)

   </details>

7. **Try one more idea, and judge it.** Ask the assistant for a further optimization, implement it as a
   separate candidate (not in `KeywordSearch`), measure it, and record whether you kept it and why.

   <details>
   <summary>Guided help: step 7</summary>

   Ask for the trade-off before the code, so you can judge more than the speed:

   ```text
   Suggest one further optimization for KeywordSearch.Search and explain what it costs in code and
   memory before writing anything. Then implement it as a new class in
   benchmarks/Bookstore.Benchmarks/Candidates/ (KeywordSearch stays as it is), add it as a third
   [Benchmark] method, and add it to KeywordSearchEquivalenceTests so it must return the same books in
   the same order as the original. Run dotnet test --solution Bookstore.slnx and the benchmark in
   Release with --job short, and tell me from the numbers whether it's worth the extra code.
   ```

   Re-run the default job before you record anything; `--job short` has wide error bars.

   **Judge it on:** the gain over Optimized (not over Original), per query; the absolute time per search
   next to the cost of a web request; allocations; and how much code it adds. "Measured, not adopted"
   is a fine verdict: write the numbers and the reason in the Decisions table.

   </details>

<details>
<summary>Prompting hints</summary>

- Ask the assistant to explain where the time and allocations go, line by line, before it writes
  anything. Compare its ranking with the benchmark.
- Name the constraints that make results comparable: same inputs, a fixed seed, the search object built
  outside the measured method, Release, `[MemoryDiagnoser]`, one method marked as the baseline.
- When you ask for the optimization, include what "unchanged" means: the same books, the same order
  (title, then id), the same treatment of case and extra spaces.
- Ask for the equivalence test **before** the optimization, and ask it to compare whole result lists, not
  just counts.
- While iterating, `--job short` gives quick, rough numbers; ask for the raw table, and use a default
  run for the numbers you record.
- If a run fails with a "directory name is invalid" or path-too-long error, your lab folder is too deep;
  `~\labs` is fine.
</details>

## Requirements

- `benchmarks/Bookstore.Benchmarks` is in `Bookstore.slnx`, references BenchmarkDotNet without a version
  in its `.csproj`, and isn't run by `dotnet test`.
- The benchmark has a baseline method, `[MemoryDiagnoser]`, a fixed-seed catalog, and at least three
  queries.
- The original implementation is kept as the baseline, and an equivalence test proves the optimized
  version returns the same books in the same order for many queries.
- `KeywordSearch`'s public API is unchanged, and every existing test still passes unchanged.
- `BENCHMARKS.md` records before and after tables from Release runs, with the machine, .NET version,
  and job, and a decision on the extra candidate from step 7.
- The optimized version is measurably faster and allocates less, by more than the `Error` column.

## Expected behavior

```powershell
dotnet build
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)

dotnet test --solution Bookstore.slnx
# Test run summary: Passed!
#   total: 30 plus your new tests, failed: 0

dotnet run -c Release --project benchmarks/Bookstore.Benchmarks -- --filter '*'
# | Method    | Query       | Mean | Error | Ratio | Allocated | Alloc Ratio |
# | Original  | the         | ...  | ...   |  1.00 | ...       |        1.00 |
# | Optimized | the         | ...  | ...   |  0.xx | ...       |        0.xx |
# ... one pair of rows per query; your numbers depend on your machine
```

A Debug build doesn't produce numbers; BenchmarkDotNet stops with "Benchmark was built without
optimization enabled (most probably a DEBUG configuration). Please, build it in RELEASE."

## Stretch goal

Find out what the remaining time is spent on. Add a second benchmark that measures only building the
search object, and compare it with a single search: at what number of searches does building an index
up front pay for itself? Then try `--job short` and the default job on the same code and compare the
`Error` columns. (Leave `solution/` as is; this is extra practice.)

## Reference

- Concepts: [Module 12: C#-Specific Pitfalls and Verification](../../../docs/module-12-csharp-pitfalls-and-verification/),
  especially [Performance Review](../../../docs/module-12-csharp-pitfalls-and-verification/05-performance-review.md)
- Worked example: [matching demo](../../../demos/bonus/09-performance-review/)
