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
2. **Create a benchmark project.** Add `benchmarks/Bookstore.Benchmarks` to `Bookstore.slnx`:
   a console project that references `Bookstore.Domain`, with the BenchmarkDotNet version pinned in
   `Directory.Packages.props` (verify the package first, as in Module 12 chapter 01). Benchmark
   `KeywordSearch.Search` over a generated catalog of about 5,000 books (a fixed random seed, so every
   run measures the same work), for at least three realistic queries. Build the search object outside
   the measurement.
3. **Measure the baseline in Release** and save the summary table. Compare it with your guesses from
   step 1.
4. **Keep the original.** Copy the current `KeywordSearch` into the benchmark project (renamed) so it
   stays the benchmark baseline, and add an xUnit **equivalence test** that compares the original and
   your new version over a generated catalog and many queries: whole words, one- and two-letter
   prefixes, mixed case, extra spaces, multiple words, and queries that match nothing.
5. **Ask for the problems, ranked, before any changes.** Then optimize with your assistant. Constraints:
   the public API stays the same (`new KeywordSearch(books)`, `Search(query)` returning `List<Book>`),
   results and their order stay identical, and the existing tests don't change.
6. **Measure again** with the same benchmark, now comparing original and optimized. Record both tables
   in `BENCHMARKS.md` at the solution root, with your machine, the .NET version, and the job you used.
7. **Try one more idea, and judge it.** Ask the assistant for a further optimization, implement it as a
   separate candidate (not in `KeywordSearch`), measure it, and record whether you kept it and why.

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

When you're done, email your instructor.

## Reference

- Concepts: [Module 12: C#-Specific Pitfalls and Verification](../../../docs/module-12-csharp-pitfalls-and-verification/),
  especially [Performance Review](../../../docs/module-12-csharp-pitfalls-and-verification/05-performance-review.md)
- Worked example: [matching demo](../../../demos/bonus/09-performance-review/)
