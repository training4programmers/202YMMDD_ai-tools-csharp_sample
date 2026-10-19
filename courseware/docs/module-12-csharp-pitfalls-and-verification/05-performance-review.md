# Performance Review: Allocations, LINQ, and Async Overhead

Assistants write code that reads well: a chain of LINQ, a `ToList()` to be safe, a string built with
`+`, an `async` keyword on every method. Most of the time that's exactly right, because most code
isn't hot. But the same habits, inside a loop that runs a million times a minute, allocate megabytes
and burn milliseconds. And when you ask an assistant to "make it faster," it answers with the same
confidence whether its idea helps, does nothing, or makes things worse.

This chapter is a review checklist for the performance mistakes AI-generated C# makes most often:
allocation-heavy LINQ, repeated enumeration, needless materialization, string building, boxing, async
overhead, and EF Core query shapes. It ends with how to measure with BenchmarkDotNet, so that the
assistant suggests and the benchmark decides. Bonus Demo 09 and Bonus Activity 09 practice the whole
loop.

---

## Where to Look, and Where Not To

Performance review is about **hot paths**: code that runs per request, per item in a batch, or in an
inner loop. A startup routine that runs once can be as wasteful as it likes. So before you review for
performance, find out where the time goes:

- **Profile first.** Visual Studio 2026's profiler, `dotnet-counters` for a running app's GC and CPU
  numbers, and `dotnet-trace` for a trace you can open later all answer "where does the time go?"
  better than reading code does.
- **Then measure the suspect in isolation** with a benchmark (later in this chapter).
- **Read for the patterns below** in that hot code only.

> **GitHub Copilot:** Visual Studio 2026 includes the **GitHub Copilot Profiler Agent** (`@profiler`,
> or enable it in the Tools picker in agent mode). It analyzes CPU and memory, can write or optimize
> BenchmarkDotNet benchmarks, and validates fixes. In VS Code, ask agent mode to run the benchmark
> project and show you the results table.
>
> **Claude Code:** Ask it to run the benchmark and paste the summary table, not describe it. Allow
> `dotnet run -c Release *` so it can measure without stopping for approval, and tell it never to
> report numbers from a Debug build.

---

## Allocation-Heavy LINQ in Hot Paths

Bonus Demo 09's starting point is an "AI-generated" finder that names a color by finding the nearest
of 1,000 named colors. It's correct, short, and readable *(the demo's planted code)*:

```csharp
public List<ColorMatch> FindNearest(IEnumerable<string> hexes)
{
    var results = new List<ColorMatch>();
    foreach (var hex in hexes)
    {
        var candidates = _colors.ToList();                        // a 1,000-item copy per query
        var best = candidates
            .Select(c => new ColorMatch(c.Name, c.Hex, Distance(hex, c.Hex)))   // 1,000 records per query
            .OrderBy(m => m.Distance)                             // reads like a sort (see below)
            .First();
        results.Add(best);
    }
    return results;
}

private static double Distance(string first, string second)
{
    var a = ColorMath.ParseHex(first);                            // parses both hex strings,
    var b = ColorMath.ParseHex(second);                           // for every single comparison
    return Math.Sqrt(Math.Pow(a.R - b.R, 2) + Math.Pow(a.G - b.G, 2) + Math.Pow(a.B - b.B, 2));
}
```

For 100 lookups, that's 100 list copies, 100,000 `ColorMatch` objects, and 200,000 hex parses (each
of which trims the `#` into a new string), all to find 100 minimums. Which of those matters most? The
recorded agent in Bonus Demo 09 ranked the `ToList()` first and called `OrderBy(...).First()` a full
sort. Measuring the allocations of each piece separately says otherwise:

| Source (100 lookups) | Allocated | Share |
|----------------------|----------:|------:|
| Parsing both hex strings per comparison | 7.63 MB | 62% |
| A `ColorMatch` record per candidate | 3.82 MB | 31% |
| `ToList()` per query | 0.77 MB | 6% |
| `OrderBy(...).First()` | about 0 | — |
| **Total** | **12.24 MB** | |

`OrderBy(...).First()` doesn't sort at all in modern .NET: LINQ recognizes the pattern and finds the
first element in a single pass. It isn't free (it calls the key selector and a comparer for every item,
so in a quick timing it was about twice as slow as `MinBy` and five times slower than a plain loop), but
it isn't the problem the code appears to have. Intuition, yours or the model's, is a poor profiler.

The reviewed version does the expensive work once, when the finder is built, and finds each minimum
with a plain loop:

```csharp
public NearestColorFinder(IEnumerable<NamedColor> colors)
{
    _colors = [.. colors];
    _rgb = Array.ConvertAll(_colors, c => ColorMath.ParseHex(c.Hex));   // parse once
}

public ColorMatch FindNearest(string hex)
{
    // (the empty-list check is omitted here)
    var target = ColorMath.ParseHex(hex);
    var bestIndex = 0;
    var bestDistance = int.MaxValue;

    for (var i = 0; i < _rgb.Length; i++)
    {
        // Compare squared distances in integers; the square root only matters for the result.
        var dr = _rgb[i].R - target.R;
        var dg = _rgb[i].G - target.G;
        var db = _rgb[i].B - target.B;
        var distance = (dr * dr) + (dg * dg) + (db * db);

        // Strictly less than: on a tie the earlier color stays, as the stable sort it replaces did.
        if (distance < bestDistance)
        {
            bestDistance = distance;
            bestIndex = i;
        }
    }

    var best = _colors[bestIndex];
    return new ColorMatch(best.Name, best.Hex, Math.Sqrt(bestDistance));
}
```

Measured with BenchmarkDotNet on a Release build *(one machine, one run; see Bonus Demo 09's
`BENCHMARKS.md` for the full table)*:

| Method (100 lookups) | Mean | Error | Ratio | Gen0 | Allocated |
|----------------------|-----:|------:|------:|-----:|----------:|
| Original | 8,590.84 µs | 192.898 µs | 1.00 | 1531.25 | 12,531.86 KB |
| Optimized | 109.93 µs | 2.154 µs | 0.013 | 0.98 | 8.65 KB |

About 78 times faster and 1,400 times less allocation, with identical results, proved by a test that
compares both versions on 3,007 inputs.

LINQ isn't the problem in general. `MinBy` would have avoided the 100,000 records with one line. The
problem is repeated, allocating work inside the hot loop, which is exactly what a model produces when it
optimizes for "reads like the requirement."

---

## Repeated Enumeration

A LINQ query over `IEnumerable<T>` is **deferred**: it runs every time you enumerate it. An assistant
that writes the query once and uses it three times runs it three times *(illustrative)*:

```csharp
var dark = colors.Where(c => ColorMath.ToHsl(c.ToRgb()).Lightness < 0.3);
if (!dark.Any())                                        // enumerates (and computes HSL)
{
    return "No dark colors";
}
return $"{dark.Count()} dark colors: {string.Join(", ", dark.Select(c => c.Name))}";   // twice more
```

Over an in-memory list that's wasted CPU. Over an EF Core `IQueryable` it's three database round trips,
and over a stream or a generator it may not even give the same answer each time. Materialize once, on
purpose:

```csharp
List<string> dark = [.. colors.Where(c => ColorMath.ToHsl(c.ToRgb()).Lightness < 0.3).Select(c => c.Name)];
return dark.Count == 0 ? "No dark colors" : $"{dark.Count} dark colors: {string.Join(", ", dark)}";
```

`CA1851` ("Possible multiple enumerations of IEnumerable collection") flags this pattern, but it isn't
in `latest-recommended`; opt in to it (chapter 03) if your codebase passes `IEnumerable<T>` around a lot.

---

## `ToList()` Everywhere

The opposite habit is just as common: `ToList()` "to be safe" on every query.

```csharp
var count = db.Colors.ToList().Count;                   // loads every row to count them
var first = colors.ToList().First();                    // copies the list to read one item
foreach (var c in palette.Colors.ToList()) { /* … */ }  // copies a list to loop over it
```

`ToList()` is right when you need a snapshot (you're about to modify the source), when you'll enumerate
more than once (above), or to end a database query deliberately. Anywhere else it's an allocation and a
copy for nothing: `await db.Colors.CountAsync(ct)`, `colors.First()`, and a plain `foreach` do the same
jobs. Inside a loop, as in the finder above, it multiplies.

---

## Strings

Strings are immutable, so `+=` in a loop copies everything built so far on every iteration:

```csharp
var css = ":root {\n";
foreach (var color in palette.Colors)
{
    css += "  --" + color.Name.ToLowerInvariant() + ": " + color.Hex + ";\n";   // O(n²) copying
}
```

Use a `StringBuilder`, and its interpolated-string overload, which formats straight into the buffer
(and takes an `IFormatProvider`, which keeps `CA1305` happy):

```csharp
var css = new StringBuilder(":root {\n");
foreach (var color in palette.Colors)
{
    css.Append(CultureInfo.InvariantCulture, $"  --{color.Name.ToLowerInvariant()}: {color.Hex};\n");
}
return css.Append("}\n").ToString();
```

Other string tools assistants underuse, and over-suggest:

- **Interpolated strings are already efficient.** Since C# 10, `$"#{rgb.R:X2}{rgb.G:X2}{rgb.B:X2}"`
  formats into a pooled buffer and allocates only the result. When an assistant proposes rewriting
  one with `string.Create` and a span callback "for performance," ask for a benchmark first; for a short
  string the gain is usually negligible and the code is much harder to read.
- **Spans avoid substrings.** `value.AsSpan().Trim()`, `int.TryParse(span, …)`, and slicing parse
  without allocating intermediate strings; worth it in a parser on a hot path.
- **Purpose-built APIs beat hand-rolled ones.** `Convert.ToHexString(bytes)` instead of
  `BitConverter.ToString(bytes).Replace("-", "")` (`CA1872`), `string.Join` instead of a loop with a
  trailing-separator fix-up.

---

## Boxing

**Boxing** wraps a value type (`int`, `decimal`, a `struct`) in a heap object so it can be treated as
`object`. Each box is an allocation. It hides in a few places:

```csharp
string.Format(CultureInfo.InvariantCulture, "{0},{1},{2}", rgb.R, rgb.G, rgb.B);   // three boxes (object args)
string.Create(CultureInfo.InvariantCulture, $"{rgb.R},{rgb.G},{rgb.B}");           // no boxes
```

Other common sources in AI code: non-generic collections from old samples (`ArrayList`, `Hashtable`),
`List<object>` used as a grab bag, a `struct` passed through a non-generic interface such as
`IComparable`, and `Dictionary<TKey, TValue>` with a struct key that doesn't implement `IEquatable<T>`.
The domain's `Rgb` is a `readonly record struct`, which implements `IEquatable<Rgb>`, so it's a
boxing-free dictionary key.

Boxing matters in hot loops and allocation-sensitive code. Elsewhere, readable code wins.

---

## Async Overhead

Async is how ASP.NET Core scales, and assistants are generally right to use it. The mistakes are at
the edges.

**Sync over async** is the expensive one: `.Result`, `.Wait()`, or `GetAwaiter().GetResult()` on a
task blocks a thread-pool thread until the work finishes. Under load that starves the pool, and request
latency climbs for everyone. `CA1849` flags it inside `Task`-returning methods (chapter 03). `Task.Run`
around synchronous work in a request handler is the mirror image: it moves the work to another
thread-pool thread and makes the request wait anyway.

**Needless `async`/`await`** costs a little: a method that only returns another method's task can
return it directly and skip the state machine:

```csharp
// Fine to elide: nothing happens after the call.
public Task<Color?> FindAsync(int id, CancellationToken cancellationToken) =>
    db.Colors.AsNoTracking().FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

// Not safe to elide: without await, the using would dispose the reader before the read finishes.
public static async Task<string> ReadAllAsync(string path, CancellationToken cancellationToken)
{
    using var reader = new StreamReader(path);
    return await reader.ReadToEndAsync(cancellationToken);
}
```

The saving is small, and eliding changes how exceptions surface. Keep `async` unless the method is a
trivial pass-through, and never elide inside `using` or `try`.

**`ValueTask<T>`** avoids allocating a `Task<T>` when a method usually completes synchronously, such as
a cache hit:

```csharp
public ValueTask<string?> GetNameAsync(string hex, CancellationToken cancellationToken) =>
    _names.TryGetValue(hex, out var name)
        ? ValueTask.FromResult(name)                                 // hit: no allocation
        : new ValueTask<string?>(LoadAsync(hex, cancellationToken));  // miss: wraps the real task
```

It comes with rules: await it exactly once, don't call `.Result` before it completes, and don't store
it. Assistants that "optimize" by changing every `Task` to `ValueTask` usually break the rules and win
nothing measurable. Use it where a benchmark says the allocation matters.

---

## EF Core: N+1 Queries and Client Evaluation

The most expensive performance bugs in AI-generated data code aren't CPU; they're round trips.

**N+1** is one query for the list, then one more per item *(illustrative)*:

```csharp
foreach (var palette in await db.Palettes.ToListAsync(cancellationToken))
{
    var count = await db.Colors.CountAsync(c => c.PaletteId == palette.Id, cancellationToken);   // per palette
    result.Add(new PaletteDto(palette.Id, palette.Name, count));
}
```

Three seeded palettes means four queries; three hundred means 301. A projection does it in one, and
it's what the ToolsApp baseline already does:

```csharp
return await db.Palettes
    .OrderBy(p => p.Name)
    .Select(p => new PaletteDto(p.Id, p.Name, p.Colors.Count))
    .ToListAsync(cancellationToken);
```

Activity 21 shows both halves of the trap. The original AI code ran one query per CSV row; the agent's
"one query" fix loaded the entire `Books` table to match in memory. The reviewed version filters in SQL
and loads only the listed books.

**Client evaluation** is the other half. EF Core throws when a `Where` can't be translated to SQL, and
the error message itself suggests `AsEnumerable()`. An assistant asked to "fix the error" often
follows that advice, which loads the whole table and filters in memory (Module 01 shows an example).
Decide deliberately: materialize a small, bounded table on purpose, or store a value SQL can filter on.

To see what EF Core actually sends:

```csharp
var query = db.Palettes.Select(p => new PaletteDto(p.Id, p.Name, p.Colors.Count));
Console.WriteLine(query.ToQueryString());   // the SQL, without running it
```

Logging the `Microsoft.EntityFrameworkCore.Database.Command` category at `Information` shows every
command and its duration while you exercise an endpoint. Count the commands; N+1 is obvious in the log.

---

## Measuring with BenchmarkDotNet

**BenchmarkDotNet** runs a method many times, after warm-up, in a separate optimized process, and
reports statistics instead of a single stopwatch reading. Put benchmarks in their own console project,
so they never run as tests:

```xml
<!-- benchmarks/ToolsApp.Benchmarks/ToolsApp.Benchmarks.csproj -->
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BenchmarkDotNet" />   <!-- 0.15.8, pinned in Directory.Packages.props -->
    <ProjectReference Include="..\..\src\ToolsApp.Domain\ToolsApp.Domain.csproj" />
  </ItemGroup>
</Project>
```

```csharp
// Program.cs
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);

[MemoryDiagnoser]                         // adds Gen0 and Allocated columns
public class NearestColorBenchmarks
{
    private readonly OriginalNearestColorFinder _original = new(NamedColors.All);
    private readonly NearestColorFinder _optimized = new(NamedColors.All);
    private string[] _queries = [];

    [GlobalSetup]
    public void Setup() { /* 100 random colors from a fixed seed */ }

    [Benchmark(Baseline = true)]
    public List<ColorMatch> Original() => _original.FindNearest(_queries);

    [Benchmark]
    public List<ColorMatch> Optimized() => _optimized.FindNearest(_queries);
}
```

Run it **in Release**, from the solution folder. Quote the filter so no shell expands `*`:

```powershell
dotnet run -c Release --project benchmarks/ToolsApp.Benchmarks -- --filter '*'
dotnet run -c Release --project benchmarks/ToolsApp.Benchmarks -- --filter '*' --job short   # quicker, rougher
```

A Debug build doesn't produce numbers; BenchmarkDotNet refuses:

```text
Benchmark was built without optimization enabled (most probably a DEBUG configuration). Please, build it in RELEASE.
```

How to read the table:

| Column | Meaning |
|--------|---------|
| `Mean` | Average time per operation (one call of the benchmark method) |
| `Error`, `StdDev` | How noisy the measurement was; a "gain" smaller than the error is no gain |
| `Ratio` | Mean relative to the `Baseline = true` method |
| `Gen0`, `Gen1` | Garbage collections per 1,000 operations |
| `Allocated`, `Alloc Ratio` | Bytes allocated per operation, absolute and relative to the baseline |

Two practical notes. Keep the measured work realistic (a fixed-seed set of inputs, the finder built
outside the measurement, as the app would hold it). And on Windows, keep the repository at a short path:
BenchmarkDotNet builds a child project under `bin\`, and from a very deep folder that can exceed the
260-character path limit. `~\labs` is fine.

---

## The AI Suggests, the Benchmark Decides

Put it together and the loop looks like this:

1. **Measure the baseline**, in Release, and save the table.
2. **Ask the assistant for the problems, ranked**, before any code changes. A good answer names
   specific lines and explains the cost, as the recorded run in Bonus Demo 09 did.
3. **Keep the original** as the benchmark baseline and as a reference, and **add an equivalence
   test** that compares old and new on many inputs, including ties and bad input. "Faster" means
   nothing if the answers changed.
4. **Optimize**, run the tests, and **measure again** with the same benchmark.
5. **Accept or reject on the numbers.** Assistants also propose "further optimizations" (unsafe code,
   forced inlining, parallelism) with the same confidence as the real ones. Implement them as
   candidates, measure them, and keep only what clearly beats the error bars, and is worth the code it
   costs.

Bonus Demo 09 shows both kinds of verdict. Asked "can it go faster still?", an assistant proposed a
SIMD version, and a second common suggestion (skip bounds checks with `Unsafe.Add`, force inlining) was
measured alongside it:

| Candidate (100 lookups) | Mean | Error | Verdict |
|-------------------------|-----:|------:|---------|
| Optimized (shipped) | 109.93 µs | 2.154 µs | — |
| `Unsafe.Add` + `AggressiveInlining` | 123.45 µs | 1.840 µs | **Rejected:** slower, beyond the error bars |
| SIMD with `Vector<int>` | 23.38 µs | 0.466 µs | **Not shipped:** 4.7x faster, but 0.23 µs per lookup was already invisible, and the code is much harder to keep correct |

The unsafe version is the classic AI "optimization": it sounds expert and makes things worse. The SIMD
version is the subtler lesson: **measurable isn't the same as worth it.** The recorded assistant
reached that conclusion itself, after measuring; without the numbers, either candidate could have been
merged on confidence alone.

---

## Summary

- Review for performance **only on hot paths**, and **profile first**; most code isn't worth
  optimizing.
- The usual AI patterns: **allocating work in loops** (re-parsing, copies, per-item records),
  **repeated enumeration** of deferred queries (`CA1851`), **`ToList()` everywhere**, **string
  concatenation in loops**, and **boxing** through `object` parameters and non-generic APIs. **Measure**
  which one matters; the line that looks worst often isn't.
- **Async**: never block on tasks (`CA1849`); elide `async` only for trivial pass-throughs; use
  **`ValueTask`** only where a benchmark shows the allocation matters, and follow its rules.
- **EF Core**: watch for **N+1** queries and "fixes" that **load whole tables**; project with `Select`,
  filter in SQL, and check the SQL with **`ToQueryString()`** or command logging.
- **BenchmarkDotNet** in its own project, **Release only**, with **`[MemoryDiagnoser]`** and a
  **baseline**, gives numbers you can trust; **equivalence tests** prove behavior didn't change.
- **The AI suggests; the benchmark decides.** Reject any "optimization" that doesn't measurably help.
