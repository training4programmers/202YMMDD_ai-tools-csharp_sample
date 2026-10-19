# Bonus Demo 09: Performance Review (ToolsApp / Colors)

Instructor demo paired with [Bonus Activity 09](../../../activities/bonus/09-performance-review/). An
"AI-generated" nearest-color finder is correct and slow; you benchmark it, have the assistant rank the
problems and optimize it behind an equivalence test, measure again, and reject a further "optimization"
that the numbers don't support.

## Concept

- **AI code optimizes for reading like the requirement.** The finder copies a list per query, parses
  both hex strings for every comparison, allocates a record per candidate, and uses
  `OrderBy(...).First()` to find a minimum. Every line is reasonable; together they allocate about
  12 MB per batch of 100 lookups.
- **The line that looks worst often isn't.** The recorded agent ranked `ToList()` first and called
  `OrderBy(...).First()` a full sort. Measured, the hex parsing is 62% of the allocations, `ToList()`
  6%, and `OrderBy(...).First()` allocates almost nothing (modern LINQ finds the first element in one
  pass).
- **Measure before you optimize.** BenchmarkDotNet, in Release, with `[MemoryDiagnoser]` and a
  baseline, gives numbers you can compare; a stopwatch in a Debug build doesn't.
- **Keep the original.** The pre-optimization class becomes the benchmark baseline *and* the reference
  for an equivalence test that compares whole results, including tie-breaking.
- **The AI suggests, the benchmark decides.** Assistants propose real wins and empty ones with the same
  confidence; implement ideas as candidates and keep only what beats the noise.

## How to run

From the course repo root, copy the starting point to a lab folder outside the repo (so the repo's own
AGENTS.md/CLAUDE.md don't leak into the session) and open it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 09 -Bonus -Open
cd ~\labs\bonus-demo-09-performance-review
dotnet build
dotnet test --solution ToolsApp.slnx
```

Before class:

- **One AI tool in agent mode** (Copilot agent mode, or a Claude Code session) that can run
  `dotnet run -c Release`. In Claude Code, allow `Bash(dotnet run -c Release *)` and
  `Bash(dotnet test *)`.
- **Warm the benchmark project:** run it once in Release so BenchmarkDotNet is restored and the first
  build doesn't eat demo time.
- **Plug in the laptop and close heavy apps.** Benchmarks on battery or beside a video call are noisy.
- **Keep the lab path short.** BenchmarkDotNet builds a child project under `bin\`; from a very deep
  folder the path can exceed Windows' 260-character limit ("The directory name is invalid").
  `~\labs` is fine.
- Budget about 25 minutes: each `--job short` run takes about a minute; the agent steps take three to
  five minutes each.

If the live run goes sideways, `start-lab.ps1 -Demo 09 -Bonus -Solution` copies the recorded outcome,
including `BENCHMARKS.md` with the full Release results.

## Demo trail

**Code to open (in order):**

1. `src/ToolsApp.Domain/NearestColorFinder.cs`: the "AI-generated" finder. Ask the room to rank the
   costs before you measure: `ToList()` in the loop, `Distance` parsing both strings, a record per
   candidate, `OrderBy(...).First()`, `Math.Pow`. Write the ranking on the board; you'll check it in
   Step 1.
2. `src/ToolsApp.Domain/NamedColors.cs` and `NamedColors.csv`: 1,000 named colors loaded once from an
   embedded resource.
3. `benchmarks/ToolsApp.Benchmarks/NearestColorBenchmarks.cs`: one benchmark, 100 random colors from a
   fixed seed, the finder built outside the measurement, `[MemoryDiagnoser]`, `Baseline = true`.
4. `tests/ToolsApp.Tests/NearestColorFinderTests.cs`: characterization tests, including
   `FindNearest_breaks_ties_by_list_order` (`#000C0C` is exactly as far from Deep Muted Green as from
   Deep Muted Blue). Any optimization has to keep that.

**Step 1: Measure, then ask for the problems.** Prompt (agent mode):

```text
Run the benchmark in benchmarks/ToolsApp.Benchmarks in Release with: dotnet run -c Release --project benchmarks/ToolsApp.Benchmarks -- --filter * --job short. Summarize Mean and Allocated. Then read src/ToolsApp.Domain/NearestColorFinder.cs and list the performance problems you see, most expensive first. Don't change any code yet.
```

The recorded run took about a minute. The agent quoted the filter (`--filter '*'`; in Git Bash a bare
`*` is expanded by the shell before BenchmarkDotNet sees it; in PowerShell either form works), reported
**9.3 ms and 12.24 MB per batch** of 100 lookups, with about 1,531 Gen0 collections per 1,000
operations, and ranked seven problems: the `ToList()` first, the double parsing second,
`OrderBy(...).First()` third ("a full O(M log M) sort"), then the record per candidate, `Math.Pow`,
`Math.Sqrt`, and the `IEnumerable` field.

The review beat: compare its ranking with the room's, then with a measurement of where the allocations
actually come from (measured separately for this demo; one run):

| Source (100 lookups) | Allocated | Share |
|----------------------|----------:|------:|
| Parsing both hex strings per comparison | 7.63 MB | 62% |
| A `ColorMatch` record per candidate | 3.82 MB | 31% |
| `ToList()` per query | 0.77 MB | 6% |
| `OrderBy(...).First()` | about 0 | — |

The agent's top item is the third-largest cost, and its third item is wrong: modern LINQ answers
`OrderBy(...).First()` with a single pass, not a sort. It still costs a delegate and comparer call per
item, but it isn't the problem it looks like. Rankings from reading code, the model's or ours, are
hypotheses; the benchmark is the evidence.

**Step 2: Optimize behind an equivalence test.** Prompt (agent mode):

```text
Optimize NearestColorFinder without changing its results: the same match, the same distance, the same tie-breaking (the earlier color in the list wins), and the same exceptions for bad input. Before you change it, copy the current class to benchmarks/ToolsApp.Benchmarks/Candidates/OriginalNearestColorFinder.cs (renamed OriginalNearestColorFinder) so the benchmark can compare original and optimized, and add a test that compares the two finders on every named color plus 2,000 random colors. Run dotnet test --solution ToolsApp.slnx, then the benchmark in Release, and show me the before and after numbers.
```

The recorded run (about 4 minutes) copied the original, wrote a parity test over 3,000 queries
comparing whole results, replaced the LINQ with a min-scan over pre-parsed colors using integer squared
distances, and reported **8,547 µs → 127.6 µs and 12.5 MB → 8.68 KB** per batch (`--job short`): about
67 times faster, with identical results. Point out what it got right (strict `<` to keep the tie rule,
the reported distance still computed exactly), then the review notes:

- **Short job, wide error bars.** Three iterations are fine for direction, not for the numbers you
  record. The recorded outcome's `BENCHMARKS.md` has a default-job run.
- **The test project referenced the benchmark executable.** It works, but pulls BenchmarkDotNet into
  the test build. The outcome links just `OriginalNearestColorFinder.cs` into the test project.
- **Exact exception timing.** The agent kept the palette parsing lazy (`??=`) so a malformed color in
  the list still fails on the first lookup, as before. The outcome parses in the constructor instead
  (fail fast) and documents that deliberate difference with a test. Either is defensible; the point is
  that it's a decision, written down.

```powershell
dotnet test --solution ToolsApp.slnx
git diff --stat
```

**Step 3: Ask for more, and let the numbers answer.** Prompt (agent mode):

```text
Can it go faster still? Propose one more optimization, implement it as a separate candidate class in the benchmarks project (not in NearestColorFinder), and benchmark it against the optimized version. Tell me whether it's worth keeping, based on the numbers.
```

The recorded run (about 6 minutes) proposed SIMD: the palette as three parallel `int` arrays, compared
`Vector<int>.Count` candidates at a time, with an index-aware reduction to keep the tie rule. It
measured **122.5 µs → 27.9 µs** (a default-job run; BenchmarkDotNet flagged the distribution as
bimodal), about 4.4 times faster, with identical results, and then recommended **not** promoting it:
per lookup that's 1.2 µs against 0.28 µs, far below anything a request would notice, for code whose
tie handling depends entirely on the parity test.

That's the right call, and the most useful moment in the demo: **measurable is not the same as worth
it.** Two review notes on the candidate itself: it built its five arrays lazily into separate fields,
so two threads making the first call at once could see a half-built finder, and it used `!` on those
fields. The recorded outcome keeps an adapted copy (built in the constructor) in
`benchmarks/ToolsApp.Benchmarks/Candidates/`, next to a second candidate an assistant commonly suggests
(skipping bounds checks with `Unsafe.Add` plus `AggressiveInlining`). Open `BENCHMARKS.md` and show
both verdicts side by side:

| Method (recorded outcome, default job) | Mean | Error | Allocated | Verdict |
|----------------------------------------|-----:|------:|----------:|---------|
| Original | 8,590.84 µs | 192.898 µs | 12,531.86 KB | Baseline |
| Optimized | 109.93 µs | 2.154 µs | 8.65 KB | **Shipped** (about 78x faster) |
| UnsafeCandidate | 123.45 µs | 1.840 µs | 9.95 KB | **Rejected:** slower than the plain loop |
| SimdCandidate | 23.38 µs | 0.466 µs | 9.95 KB | **Not shipped:** faster, not worth it yet |

One candidate sounds expert and makes things worse; the other really is faster and still isn't worth
its complexity at 0.23 µs per lookup. Both verdicts come from the table, not from the assistant's tone.

```powershell
dotnet test --solution ToolsApp.slnx   # recorded outcome: 32 tests, including the equivalence suite
```

**Key teaching beat:** the assistant was useful at every step: it found the real costs, wrote the
equivalence test before touching the code, implemented a correct SIMD version, and talked itself out
of shipping it. It was also wrong or overconfident at several: the ranking, the "sort" that isn't one,
and a lazy initializer that isn't thread-safe. What decided each question was a measurement or a test,
never the model's confidence. Keep the original, prove equivalence, measure in Release, and accept only
what beats the noise and pays for its complexity.

## Reference

- Concepts: [Module 12: C#-Specific Pitfalls and Verification](../../../docs/module-12-csharp-pitfalls-and-verification/),
  especially [Performance Review](../../../docs/module-12-csharp-pitfalls-and-verification/05-performance-review.md)
- Tools: [Tools Review](TOOLS-REVIEW.md)
- Practice: [Bonus Activity 09](../../../activities/bonus/09-performance-review/)
