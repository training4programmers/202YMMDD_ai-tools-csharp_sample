# Benchmarks: NearestColorFinder

Recorded 2026-10-16 from a Release build, BenchmarkDotNet's default job. Numbers depend on the machine,
its power settings, and what else is running; compare ratios, not absolute times, with your own runs.

```powershell
dotnet run -c Release --project benchmarks/ToolsApp.Benchmarks -- --filter '*'
```

```text
BenchmarkDotNet v0.15.8, Windows 11 (10.0.26300.9550)
AMD Ryzen 9 7940HS w/ Radeon 780M Graphics 4.00GHz, 1 CPU, 16 logical and 8 physical cores
.NET SDK 10.0.401
  [Host]     : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
  DefaultJob : .NET 10.0.12 (10.0.12, 10.0.1226.42308), X64 RyuJIT x86-64-v4
```

Each operation names **100 random colors** (fixed seed) against the **1,000 built-in named colors**.

| Method          | Mean        | Error      | StdDev     | Ratio | Gen0      | Allocated   | Alloc Ratio |
|---------------- |------------:|-----------:|-----------:|------:|----------:|------------:|------------:|
| Original        | 8,590.84 us | 192.898 us | 568.763 us | 1.004 | 1531.2500 | 12531.86 KB |       1.000 |
| Optimized       |   109.93 us |   2.154 us |   2.116 us | 0.013 |    0.9766 |     8.65 KB |       0.001 |
| UnsafeCandidate |   123.45 us |   1.840 us |   1.631 us | 0.014 |    1.0986 |     9.95 KB |       0.001 |
| SimdCandidate   |    23.38 us |   0.466 us |   0.864 us | 0.003 |    1.1902 |     9.95 KB |       0.001 |

BenchmarkDotNet flagged the `Original` distribution as bimodal (heavy GC makes some iterations much
slower than others); its mean is still roughly 78 times the optimized one, far outside any error bar.

## Decisions

| Version | Verdict | Why |
|---------|---------|-----|
| `Optimized` (now `NearestColorFinder`) | **Shipped** | About 78x faster and 1,400x less allocation per batch (8.6 ms → 0.11 ms, 12.2 MB → 8.7 KB), with identical results (`NearestColorFinderEquivalenceTests`). |
| `UnsafeCandidate` (`Unsafe.Add`, `AggressiveInlining`) | **Rejected** | *Slower* than the plain loop: 123 µs against 110 µs, and the gap is larger than both error bars. Skipping bounds checks sounded like a win; measured, it bought risk and nothing else. |
| `SimdCandidate` (`Vector<int>` over three channel arrays) | **Measured, not shipped** | About 4.7x faster than `Optimized` (23 µs against 110 µs per 100 lookups), with identical results. But that's 1.1 µs against 0.23 µs *per lookup*, invisible inside a web request, and the code's tie handling depends on the equivalence test to stay correct. Kept in `benchmarks/.../Candidates/` as the documented option if nearest-color ever runs per pixel or against a much larger palette. |

## Where the original's allocations came from

Measured separately (one run, `GC.GetAllocatedBytesForCurrentThread`), to check the assistant's
ranking of the problems against reality:

| Source (100 lookups) | Allocated | Share |
|----------------------|----------:|------:|
| Parsing both hex strings per comparison | 7.63 MB | 62% |
| A `ColorMatch` record per candidate | 3.82 MB | 31% |
| `ToList()` per query | 0.77 MB | 6% |
| `OrderBy(...).First()` | about 0 | — |

`OrderBy(...).First()` reads like a sort, but LINQ answers it with a single pass. The assistant ranked
the `ToList()` first and called `OrderBy(...).First()` a full sort; both were wrong, and only a
measurement could tell.

## Reproduce

1. Run the command above from the solution folder, in a short path (`~\labs` is fine; very deep folders
   can exceed Windows' path limit when BenchmarkDotNet builds its child project).
2. For a quick, rougher look, add `--job short`.
3. Results land in `BenchmarkDotNet.Artifacts/results/` (git-ignored).
