# Benchmarks: KeywordSearch

Recorded 2026-10-16 from a Release build, BenchmarkDotNet's default job. Numbers depend on the machine,
its power settings, and what else is running; compare ratios, not absolute times, with your own runs.

```powershell
dotnet run -c Release --project benchmarks/Bookstore.Benchmarks -- --filter '*'
```

```text
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9550)
AMD Ryzen 9 7940HS w/ Radeon 780M Graphics 4.00GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
```

Each operation is **one search** over a **5,000-book** catalog (`SampleCatalog`, seed 9). The three
queries match 756 books (`the`), 27 books (`quiet comp`), and 19 books (`maya okafor`).

| Method           | Query       | Mean       | Error      | StdDev     | Ratio | Gen0     | Allocated  | Alloc Ratio |
|----------------- |------------ |-----------:|-----------:|-----------:|------:|---------:|-----------:|------------:|
| Original         | maya okafor | 984.315 us | 19.3748 us | 34.4387 us | 1.001 | 435.5469 | 3558.26 KB |       1.000 |
| Optimized        | maya okafor |  37.268 us |  0.7438 us |  1.3220 us | 0.038 |   0.1831 |    1.83 KB |       0.001 |
| IndexedCandidate | maya okafor |   6.071 us |  0.1208 us |  0.3287 us | 0.006 |   0.9308 |    7.63 KB |       0.002 |
| Original         | quiet comp  | 995.140 us | 19.8578 us | 55.0260 us |  1.00 | 435.5469 |  3558.5 KB |       1.000 |
| Optimized        | quiet comp  |  37.045 us |  0.7367 us |  1.3095 us |  0.04 |   0.2441 |    2.07 KB |       0.001 |
| IndexedCandidate | quiet comp  |  10.505 us |  0.2281 us |  0.6618 us |  0.01 |   1.7853 |   14.66 KB |       0.004 |
| Original         | the         | 921.169 us | 17.9361 us | 25.1439 us |  1.00 | 387.6953 | 3167.13 KB |        1.00 |
| Optimized        | the         | 225.414 us |  4.4783 us |  8.4113 us |  0.24 |   4.8828 |   40.39 KB |        0.01 |
| IndexedCandidate | the         | 192.775 us |  3.8342 us |  9.4054 us |  0.21 |   8.0566 |   67.39 KB |        0.02 |

BenchmarkDotNet flagged several distributions as bimodal or multimodal (GC pauses in `Original`, and
the candidate's small means); the conclusions below hold with plenty of margin.

## Decisions

| Version | Verdict | Why |
|---------|---------|-----|
| `Optimized` (now `KeywordSearch`) | **Shipped** | 26x faster for selective queries (about 985 µs → 37 µs) and 4x for `the`, with allocations down from about 3.5 MB to 2–40 KB per search. Same books, same order (`KeywordSearchEquivalenceTests`). |
| `IndexedCandidate` (an inverted word index) | **Measured, not adopted** | Another 6x for selective queries (37 µs → 6 µs), but no meaningful gain for `the` (225 µs → 193 µs) and more allocation per search. At 37 µs a search is already invisible inside a web request, and the index is several times more code to keep correct. Kept in `benchmarks/.../Candidates/` in case the catalog grows by an order of magnitude. |

## What's left in the optimized version

For `the`, nearly half of the remaining time is sorting 756 matches by title with the default
(culture-aware) string comparer, the same comparer the original used. (A quick stopwatch check put the
sort at about 126 µs of a 279 µs search; with `StringComparer.Ordinal` the same sort took about 53 µs.) The recorded assistant avoided
that cost by pre-sorting the catalog when the search was built; review rejected that because it fixes
the culture at construction time instead of search time. If the team decides that titles should sort
the same way for every user (by a fixed culture, or ordinally), pre-sorting once becomes a valid and
much cheaper option. That's a product decision about ordering, not a performance tweak, so it isn't
made here.

## Reproduce

1. Run the command above from the solution folder, in a short path (`~\labs` is fine).
2. For a quick, rougher look, add `--job short`.
3. Results land in `BenchmarkDotNet.Artifacts/results/` (git-ignored).
