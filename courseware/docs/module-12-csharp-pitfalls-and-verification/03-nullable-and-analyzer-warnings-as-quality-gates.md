# Nullable and Analyzer Warnings as Quality Gates

A warning is a bug report someone else has already written for you. In a team that writes every line
by hand, a handful of warnings in a big build is easy to tolerate. With an assistant generating a few
hundred lines an hour, "a handful" grows quickly, and an agent that sees forty warnings in its build
output learns that warnings don't matter. The fix is to turn the most useful warnings into **gates**:
checks the build enforces, so neither a person nor an agent can finish with them still present.

This chapter covers nullable reference types and why the null-forgiving `!` is a code smell in AI
diffs, how to make warnings fail the build (`TreatWarningsAsErrors`, `WarningsAsErrors`), and the
code-analysis (CA) rules that catch the mistakes assistants make most often in C#. Chapter 04 covers
configuring all of it with EditorConfig and enforcing it with `dotnet format`.

![Generated code flows through five gates in order: compile with nullable enabled and warnings as errors, analyzers at latest-recommended plus opt-in CA rules, dotnet format verify-no-changes, tests including other cultures, and human review; a failure at any gate goes to a reject step where the author or agent fixes the code, then the change starts again](../../diagrams/png/12-quality-gates-pipeline.png)

*Figure: The same gates apply to every change, whoever wrote it. The cheapest run first, and a failed gate's error message becomes the agent's next prompt.*

---

## Nullable Warnings Are Bugs in Waiting

With `<Nullable>enable</Nullable>` (on in every course snapshot), the compiler tracks which references
can be `null` and warns when you might dereference one. The warnings an assistant triggers most
often:

| Warning | Meaning | Typical AI cause |
|---------|---------|------------------|
| `CS8602` | Dereference of a possibly null reference | Using the result of `FirstOrDefaultAsync` or `FindAsync` without a check |
| `CS8600` | Converting null literal or possible null value to non-nullable type | `string name = dict.GetValueOrDefault(key);` |
| `CS8604` | Possible null reference argument | Passing a nullable navigation property to a method |
| `CS8618` | Non-nullable member must contain a non-null value when exiting constructor | Old-style DTOs and options classes without `required` or initializers |
| `CS8625` | Cannot convert null literal to non-nullable reference type | `Color color = null;` copied from pre-C# 8 code |

Each one is the compiler proving that a path to a `NullReferenceException` exists. That's why
"make the warnings go away" is a dangerous instruction to give an assistant: the fastest way is to
stop the compiler from looking.

### The `!` operator is a code smell

The **null-forgiving operator** `!` tells the compiler "trust me, this isn't null". It changes
nothing at runtime. In AI-generated code it's usually not a proof; it's a way to silence `CS8602`.
Demo 21's starting code has two *(from the demo's planted AI change)*:

```csharp
var palette = await db.Palettes.Include(p => p.Colors).AsNoTracking()
    .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

foreach (var color in palette!.Colors.OrderBy(c => c.Id))   // unknown id: NullReferenceException
```

The build is clean, every analyzer is quiet, and `GET /api/palettes/9999/export.css` returns 500
instead of 404. Activity 21 has a nastier version: the supplier's ISBNs arrive without hyphens, the
catalog stores them with hyphens, so *every* lookup returns `null`, and `book!.Price = price` fails on
the first row of a perfectly valid file.

The reviewed version handles the case the compiler warned about:

```csharp
if (palette is null)
{
    return TypedResults.NotFound();
}

foreach (var color in palette.Colors.OrderBy(c => c.Id))
```

### Better ways to tell the compiler the truth

When something really can't be null, say so in a way the compiler can check:

```csharp
// Guard parameters; the compiler knows the value is non-null afterward.
ArgumentNullException.ThrowIfNull(palette);

// Pattern matching gives you a non-null local.
if (await db.Palettes.FindAsync([id], cancellationToken) is not { } palette)
{
    return TypedResults.NotFound();
}

// Required members instead of "= null!" on DTOs and entities.
public sealed class Color
{
    public required string Name { get; set; }
    public required string Hex { get; set; }
}

// Annotate Try-methods so callers get flow analysis, not a !.
public static bool TryToIsbn13(string? value, [NotNullWhen(true)] out string? isbn13) { /* ... */ }
```

In tests, `Assert.NotNull(x)` is annotated the same way, so after it the compiler treats `x` as
non-null; no `!` needed.

There are a few legitimate uses of `!` (EF Core navigation properties you've just loaded, a framework
callback the compiler can't see into). Each deserves a comment explaining why it's safe. When you
review an AI diff, search for every new one and ask whether it's a proof or a hope:

```powershell
git diff -U0 | Select-String -Pattern '^\+.*(\w|\))!(\.|;|,|\))|= null!|default!'
```

### Make nullable warnings errors

You can promote all nullable warnings at once with the special `nullable` value, without promoting
everything else:

```xml
<PropertyGroup>
  <Nullable>enable</Nullable>
  <WarningsAsErrors>$(WarningsAsErrors);nullable</WarningsAsErrors>
</PropertyGroup>
```

This is the single most valuable gate for AI-generated C#: an agent can't finish a task with a
`CS8602` in its build, so it has to decide what happens when the value is null. It still *can* reach
for `!`, which is why the rule in your instruction file matters too:

```markdown
- Never use the null-forgiving operator (!) or `= null!` to silence a nullable warning. Handle the
  null case, or ask me.
```

---

## Making Warnings Fail the Build

| Property | Effect |
|----------|--------|
| `TreatWarningsAsErrors` | Every compiler and analyzer warning becomes an error |
| `WarningsAsErrors` | Only the listed IDs (or `nullable`) become errors |
| `WarningsNotAsErrors` | With `TreatWarningsAsErrors`, these IDs stay warnings |
| `CodeAnalysisTreatWarningsAsErrors` | Set to `false` to keep CA warnings as warnings while everything else is an error |

The course's gated snapshots use the strict version, with one documented exception:

```xml
<!-- Directory.Build.props -->
<PropertyGroup>
  <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>

  <!-- A security advisory published tomorrow shouldn't break every build; triage it instead. -->
  <WarningsNotAsErrors>$(WarningsNotAsErrors);NU1901;NU1902;NU1903;NU1904</WarningsNotAsErrors>
</PropertyGroup>
```

Should the gate run on developer machines or only in CI? Teams differ. With AI agents in the loop,
there's a strong argument for everywhere: the agent runs `dotnet build`, sees errors rather than
warnings it can ignore, and fixes them before handing the diff back. If that's too strict for your
team's inner loop, condition the property on CI (for example `Condition="'$(GITHUB_ACTIONS)' == 'true'"` on
GitHub Actions) and make sure the agent's instructions say to treat warnings as failures anyway.

Turning gates on in an existing codebase is a project of its own: expect a wave of warnings that were
always there. Turn them on in one project at a time, or start with `WarningsAsErrors` for a short list
(`nullable` plus the CA rules below) and widen it.

---

## The CA Rules That Catch AI Mistakes

`AnalysisLevel` decides which code-analysis rules run as warnings (chapter 04 explains the values).
`latest-recommended` is a good default for application code. These are the rules that fire most often
on AI-generated C#, and whether `latest-recommended` includes them, as checked against the .NET 10 SDK:

| Rule | Title | In `latest-recommended`? | What the AI wrote |
|------|-------|:---:|-------------------|
| `CA2016` | Forward the CancellationToken parameter to methods that take one | Yes | `await db.SaveChangesAsync();` inside a handler that has a token |
| `CA1305` | Specify IFormatProvider | Yes | `decimal.Parse(text)`, `price.ToString("0.00")` |
| `CA1310` | Specify StringComparison for correctness | Yes | `line.StartsWith("name")` |
| `CA1304`, `CA1311` | Specify CultureInfo / Specify a culture or use an invariant version | Yes | `name.ToLower()`, `isbn.ToUpper()` |
| `CA1862` | Use the StringComparison method overloads for case-insensitive comparisons | Yes | `code.ToUpper() == "ISBN"` |
| `CA1848`, `CA2254`, `CA1873` | Use LoggerMessage delegates / Template should be a static expression / Avoid potentially expensive logging | Yes | `logger.LogInformation($"Imported {count} colors")` |
| `CA1869` | Cache and reuse JsonSerializerOptions instances | Yes | `new JsonSerializerOptions { ... }` on every call |
| `CA1849` | Call async methods when in an async method | **No: opt in** | `db.SaveChanges()`, `reader.ReadToEnd()`, `.Result` inside `async` code |
| `CA2000` | Dispose objects before losing scope | **No: opt in** | `var reader = new StreamReader(stream);` with no `using` |
| `CA2007` | Do not directly await a Task (ConfigureAwait) | **No: a policy decision** | Plain `await` in library code |
| `CA1851` | Possible multiple enumerations of IEnumerable collection | **No: opt in** | `items.Count()` then `foreach (var i in items)` |

That table is the key point of this section: **`latest-recommended` is not "everything that matters."**
Several rules that catch classic AI mistakes are only in `latest-all`, which is too noisy for most
codebases (it also flags `ToLowerInvariant()` with `CA1308`, even where lowercase is the requirement).
Opt in to the specific rules you want, in `.editorconfig`:

```ini
[*.cs]
# Not in latest-recommended, but they catch mistakes assistants make often.
dotnet_diagnostic.CA1849.severity = warning
dotnet_diagnostic.CA2000.severity = warning
```

When Demo 21's solution opted in to `CA1849`, it found a real one in the original *baseline*, not the
AI code: `app.Run()` at the end of a `Program.cs` whose top-level statements already `await`
something. The fix is `await app.RunAsync();`.

### Culture: CA1304, CA1305, CA1310, CA1311

Culture bugs only appear on machines that aren't set to en-US, which is why they survive testing on
developer laptops *(illustrative values; the behavior is real)*:

```csharp
// On a server running de-DE, "." is a group separator:
decimal.Parse("18.99");                                          // 1899
decimal.Parse("18.99", CultureInfo.InvariantCulture);            // 18.99

// In tr-TR, "I" lowercases to a dotless "ı":
"Indigo".ToLower(CultureInfo.GetCultureInfo("tr-TR"));           // "ındigo"
"Indigo".ToLowerInvariant();                                     // "indigo"
```

Decide what each string *is*. Data formats (CSV prices, ISBNs, CSS identifiers, JSON, keys) use the
invariant culture and ordinal comparisons. Text shown to a person can use the current culture. Then
write a test that runs under a hostile culture, so the decision stays made:

```csharp
[Fact]
public void Parse_reads_prices_the_same_way_in_every_culture()
{
    var original = CultureInfo.CurrentCulture;
    CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
    try
    {
        Assert.Equal(19.49m, Assert.Single(SupplierPriceList.Parse("9781234560010,19.49").Prices).Price);
    }
    finally
    {
        CultureInfo.CurrentCulture = original;
    }
}
```

### Async: CA2016, CA1849, and the CA2007 decision

`CA2016` is the cheapest win in the table: the handler has a `CancellationToken`, and the AI forgot to
pass it on, so a cancelled request keeps querying the database. The code fix adds the argument.

`CA1849` catches sync-over-async: inside a `Task`-returning method, it flags any call that has an
`Async` equivalent (`SaveChanges`, `ReadToEnd`, `Run`), and also `Task.Wait()`, `.Result`, and
`GetAwaiter().GetResult()`. It doesn't look inside synchronous methods, and it says nothing about
`async void`; the `Microsoft.VisualStudio.Threading.Analyzers` package covers those (`VSTHRD002`,
`VSTHRD100`), as Module 01 described.

`CA2007` (`ConfigureAwait`) is a **policy decision**, not a bug detector. ASP.NET Core has no
`SynchronizationContext`, so `ConfigureAwait(false)` in application code does nothing useful and adds
noise. A reusable library that might run under a UI or classic ASP.NET context is different. Decide
per project, write the decision down, and tell the assistant, because models sprinkle
`.ConfigureAwait(false)` inconsistently in both kinds of code.

### Logging: CA1848, CA2254, CA1873

Assistants almost always write interpolated log messages:

```csharp
logger.LogInformation($"Imported {imported} colors into palette {id}.");
// CA2254: The logging message template should not vary between calls
// CA1848: For improved performance, use the LoggerMessage delegates
// CA1873: Evaluation of this argument may be expensive and unnecessary if logging is disabled
```

The interpolation builds a new string on every call, even when Information logging is off, and it
destroys structured logging: `Count` and `PaletteId` are no longer searchable fields. The
source-generated version fixes all three:

```csharp
public static partial class PaletteTransferEndpoints
{
    [LoggerMessage(Level = LogLevel.Information, Message = "Imported {Count} colors into palette {PaletteId}")]
    private static partial void LogImported(ILogger logger, int count, int paletteId);
}

// in the handler
LogImported(logger, result.Colors.Count, id);
```

### Disposal: CA2000

`CA2000` flags disposables that are never disposed, such as a `StreamReader`, an `HttpRequestMessage`,
or a `StringContent` in a test. It's noisier than the others (it can't see ownership transfers), so
some teams enable it for `src/` only. Either way, the fix is almost always `using var`.

---

## Feeding the Gates Back to the Assistant

Gates pay off most when the assistant is the one hitting them. A prompt like this one, from Demo 21,
turns the whole table above into the agent's to-do list:

```text
Turn on our quality gates in Directory.Build.props: TreatWarningsAsErrors, AnalysisLevel
latest-recommended, and EnforceCodeStyleInBuild. Then make dotnet build pass with zero warnings by
fixing the code, not by suppressing. If a rule genuinely doesn't fit, stop and explain it to me
before you suppress anything.
```

In the recorded run, the agent fixed every finding in the AI-generated file in code (obsolete hashing,
culture, cancellation tokens, logging, the namespace style), then stopped at `CA1707` on the tests'
underscore names and asked how to proceed. That's the behavior you want: fixes for real problems, and
a question for a convention clash. Chapter 04 covers what "suppress it properly" looks like.

> **GitHub Copilot:** Agent mode in Visual Studio 2026 and VS Code reads build output when it runs
> `dotnet build` in the terminal, and in VS Code `#read/problems` gives it the Problems panel. Ask
> it to build before it reports back. Copilot code review on pull requests is a later, separate check;
> it comments, and doesn't block the merge.
>
> **Claude Code:** The agent reads `dotnet build` output from its own terminal tool. Put the gate
> rules ("build must have zero warnings; never suppress without asking") in `CLAUDE.md`, and consider
> a `Stop` hook (Module 07) that refuses to end a turn while the build fails.

---

## Summary

- **Nullable warnings are proofs** that a `NullReferenceException` path exists; treat them as bugs,
  and promote them with `<WarningsAsErrors>nullable</WarningsAsErrors>`.
- The **null-forgiving `!`** silences the compiler without changing runtime behavior. In AI diffs it's
  usually a hope, not a proof; prefer **null checks, pattern matching, `required` members,
  `ThrowIfNull`, and `[NotNullWhen]`**, and review every new `!`.
- **`TreatWarningsAsErrors`** makes warnings a gate the agent must clear; use **`WarningsNotAsErrors`**
  for deliberate exceptions such as NuGet audit warnings.
- **`latest-recommended`** catches culture (`CA1304`, `CA1305`, `CA1310`, `CA1311`), cancellation
  (`CA2016`), logging (`CA1848`, `CA2254`, `CA1873`), and JSON options (`CA1869`) mistakes, but **not**
  `CA1849`, `CA2000`, `CA2007`, or `CA1851`: **opt in** to the ones you want.
- **`CA2007` is a policy**: decide it per project (usually off for ASP.NET Core apps, on for
  libraries) and tell the assistant.
- Prompt the agent to **fix, not suppress**, and to **stop and ask** when a rule doesn't fit.
