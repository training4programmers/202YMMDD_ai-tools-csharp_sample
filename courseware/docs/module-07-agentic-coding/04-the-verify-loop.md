# The Verify Loop: Build, Analyzers, Tests, and Format in the Loop

An agent believes it is done when it runs out of things it can see wrong. The verify loop is how you
control what it can see. When the compiler, the analyzers, the tests, and the formatter all run inside the
agent's loop, every mistake they can detect becomes a precise message the agent acts on before you ever
look at the diff. When they don't run, the same mistakes reach your review, or production.

This chapter shows how to make failure **legible** in a .NET solution: the four gates and how to make them
strict, how to instruct an agent to run them and read their output, and how to stop the most tempting
shortcut an agent has, which is fixing the test instead of the code.

---

## The four gates

Run the gates cheapest first, and stop at the first red one:

1. **Build**: `dotnet build` with warnings treated as errors. Catches anything that doesn't compile,
   including hallucinated APIs and nullable mistakes.
2. **Analyzers**: Roslyn code-quality (CA) and code-style (IDE) rules, run as part of the build. Catches
   code that compiles but is wrong or unidiomatic.
3. **Tests**: `dotnet test --solution`. Catches wrong behavior, as far as the tests describe it.
4. **Format**: `dotnet format --verify-no-changes`. Catches layout and style drift, so reviews stay about
   substance.

![A verify loop: the agent edits code, then a row of gates runs in order (dotnet build for compiler errors, analyzers for CA and IDE warnings treated as errors, dotnet test for failing tests, dotnet format with verify-no-changes). Any failing gate sends its exact output back to the agent; when all are green the work goes to human review. Notes list hooks that make it automatic and the rules the agent is given.](../../diagrams/png/07-verify-loop.png)

*Figure: The verify loop. Every failure is precise feedback for the agent; only all-green work reaches a
human.*

---

## Gate 1: a strict build

By default, warnings don't fail a build, so they scroll past both you and the agent. Make them errors:

```powershell
dotnet build -warnaserror
```

Better, make strictness the default for everyone, human or agent, in `Directory.Build.props`:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <AnalysisLevel>latest-recommended</AnalysisLevel>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
  </PropertyGroup>
</Project>
```

The compiler output is already ideal for an agent: file, line, column, ID, and message.

```text
src/ToolsApp.Api/Endpoints/ColorSearchEndpoints.cs(22,55): error CS8602: Dereference of a possibly null reference.
```

With nullable warnings as errors, "the AI forgot the null check" stops being a code-review comment and
becomes a build failure the agent fixes on its own.

---

## Gate 2: analyzers as a quality gate

The .NET SDK ships the Roslyn analyzers. Three MSBuild properties control how loudly they speak:

| Property | What it does |
|----------|--------------|
| `AnalysisLevel` | Which rule set and version: `latest` (the default), or compound values such as `latest-recommended` and `latest-all` |
| `EnforceCodeStyleInBuild` | Runs the IDE (code-style) rules from `.editorconfig` during the build; off by default |
| `TreatWarningsAsErrors` | Fails the build on any warning, including analyzer warnings |

Turning on `latest-recommended` in a typical AI-assisted codebase surfaces exactly the C# mistakes models
make. These came from the Demo 13 starter, code that compiled cleanly under the default settings:

```text
ColorSearchEndpoints.cs(20,30): warning CA2016: Forward the 'cancellationToken' parameter to the 'ToListAsync' method ...
ColorSearchEndpoints.cs(22,29): warning CA1862: Prefer the string comparison method overload of 'string.Contains(string)' that takes a 'StringComparison' ...
ColorSearchEndpoints.cs(22,36): warning CA1311: Specify a culture or use an invariant version ...
ColorMath.cs(94,9):             warning CA1305: The behavior of 'string.Format(string, object, object, object)' could vary based on the current user's locale settings ...
ColorSearchEndpoints.cs(7,11):  warning IDE0161: Convert to file-scoped namespace
```

Each one is a real bug or a team rule: a cancellation token that never reaches the database, a
case-insensitive match that misbehaves for Turkish users (`"I".ToLower()` isn't `"i"` there), CSS output
whose format depends on the server's culture (harmless with whole numbers, broken the day someone adds a
decimal place and a German server writes `32,5%`), and a style rule from `.editorconfig`.

### When a rule doesn't fit

Sometimes a rule is wrong for part of the code. The same Demo 13 build also reported this, 13 times:

```text
ColorMathTests.cs(11,17): warning CA1707: Remove the underscores from member name ToolsApp.Tests.ColorMathTests.TryParseHex_reads_long_and_short_forms(...)
```

CA1707 protects public API names, but the team's test names use underscores on purpose. Watch what an agent
does here, because there are three possible responses:

1. **Rename every test method.** That edits the tests, which the task forbids, and churns the history.
2. **Switch the rule off for the whole solution.** That also silences it for real public APIs.
3. **Scope the exception to test code, with the reason.** This is the right answer:

```ini
[tests/**.cs]
# xUnit test names read as sentences (Method_does_this_when_that). CA1707 protects public API names,
# and test methods aren't API, so the rule is off for test code only.
dotnet_diagnostic.CA1707.severity = none
```

For a single member, use an attribute with a justification instead of a `#pragma`:

```csharp
[SuppressMessage(
    "Performance",
    "CA1822:Mark members as static",
    Justification = "PriceCalculator is registered and injected as a service; callers depend on the instance API.")]
public Quote Calculate(IReadOnlyList<QuoteLine> lines, string? couponCode = null)
```

Make "no solution-wide switch-offs; scope and justify exceptions" an explicit rule for the agent.

---

## Gate 3: tests the agent can read

On .NET 10 with xUnit v3 and Microsoft.Testing.Platform (opted in through `global.json`), run the tests
with:

```powershell
dotnet test --solution ToolsApp.slnx
```

A failure names the test, the inline data that failed, and the values:

```text
failed ToolsApp.Tests.ColorMathTests.TryParseHex_reads_long_and_short_forms(hex: "#F0A", r: 255, g: 0, b: 170) (2ms)
  Assert.Equal() Failure: Values differ
  Expected: Rgb { R = 255, G = 0, B = 170 }
  Actual:   Rgb { R = 240, G = 175, B = 10 }
```

From that alone, an agent (or you) can see that `#F0A` was read as `F0AF0A` instead of `FF00AA`: the
shorthand expansion repeats the string instead of doubling each digit.

Two practical tips:

- While iterating on one area, run a subset. xUnit v3 accepts filters directly on .NET 10, for example
  `dotnet test --solution ToolsApp.slnx --filter-class "ToolsApp.Tests.ColorMathTests"`. Always finish with
  the full suite.
- Count **root causes**, not failures. Activity 13's starter has eight failing test cases from just three
  bugs. An agent that fixes the eight cases one at a time is guessing; one that groups them by cause is
  debugging.

---

## Gate 4: formatting

[`dotnet format`](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format) applies
`.editorconfig` whitespace, style, and analyzer fixes. The `--verify-no-changes` switch makes it a gate: it
changes nothing and exits non-zero if any file would change.

```powershell
dotnet format ToolsApp.slnx --verify-no-changes   # exit code 0 = clean
dotnet format ToolsApp.slnx                       # apply the fixes
dotnet format whitespace --folder --include src/ToolsApp.Api/Endpoints/ColorSearchEndpoints.cs
```

The subcommands `whitespace`, `style`, and `analyzers` run one part at a time; `--include` takes paths
relative to the current folder. Formatting is the gate an agent should never have to think about: a hook
can run it after every edit (chapter 05).

---

## Instructing the agent

Agents run the commands you tell them to run. Put the gates in the prompt, or better, in the instruction
file so they apply to every task:

```text
Turn on TreatWarningsAsErrors, AnalysisLevel latest-recommended, and EnforceCodeStyleInBuild in
Directory.Build.props. Then make all three gates pass, in this order:

  dotnet build
  dotnet test --solution ToolsApp.slnx
  dotnet format ToolsApp.slnx --verify-no-changes

Rules:
- Fix the code, not the tests. Never edit, skip, or delete a test or change an expected value.
  If you think a test is wrong, stop and tell me why.
- No solution-wide analyzer switch-offs. Scope any exception narrowly and justify it in a comment.
- Group failures by root cause. For each fix, give the root cause in one sentence.
- Before you say you're done, run all three gates again and show me the summary lines.
```

The last rule matters. Agents sometimes report success from memory of an earlier run; asking for the final
summary lines makes the claim checkable.

### What good iteration looks like

An **illustrative** extract from an agent working through the Demo 13 starter:

```text
> dotnet build                      -> 1 error: CA1305 in ToolsApp.Domain/ColorMath.cs(94,9)
  ToCssHsl formats with the current culture. Use string.Create(CultureInfo.InvariantCulture, ...).
> dotnet build                      -> 11 errors in ToolsApp.Api
  CS8602: q is a nullable query parameter with no check; TASK.md wants a 400 when it's missing.
  CA2016: the token isn't passed to ToListAsync.  CA1304/CA1311/CA1862: ToLower().Contains(...);
  use Contains(text, StringComparison.OrdinalIgnoreCase).  IDE0161: file-scoped namespace.
> dotnet build                      -> 13 errors in ToolsApp.Tests, all CA1707 on test method names
  Tests are off-limits and the names are intentional: scoping CA1707 off for tests/**.cs in
  .editorconfig, with a comment.
> dotnet test --solution ToolsApp.slnx -> 3 failed
  1. #F0A parsed as F0AF0A: the expansion loop writes positions i and i+3 instead of 2i and 2i+1.
  2. Rate(4.5) is AALarge: thresholds compared with > instead of >= (a 4.5 ratio meets AA).
  3. ToCssHsl gives 32% for 32.5%: (int) truncates; round with MidpointRounding.AwayFromZero.
```

That is the behavior you want: one cause per fix, explained, and no test touched. Notice also that the
errors arrived in **waves**. A project that fails to build hides the errors of every project that depends
on it, so new errors after a fix usually mean progress, not regression. Warn the agent (and yourself)
about that, or it may start undoing good fixes.

---

## Guarding against "fixing the test instead of the code"

When a test fails, the shortest path to green is often to change the test. Models take that path more often
than you'd like, in several forms:

- changing an expected value to match the actual one;
- adding `[Fact(Skip = "...")]`, deleting the test, or commenting out an `[InlineData]` row;
- special-casing the input in production code (`if (hex == "#F0A") return new Rgb(255, 0, 170);`);
- catching and swallowing the exception the test expects;
- `#pragma warning disable` or a solution-wide `NoWarn` to silence an analyzer.

Layer your defenses, from cheapest to strongest.

### 1. Say it, in the prompt and in AGENTS.md

"Don't change or delete existing tests to make them pass. If a test looks wrong, stop and ask." It works
most of the time, which isn't the same as always.

### 2. Make it a permission

A deny rule is a hard stop for Claude Code's file tools:

```json
{ "permissions": { "deny": ["Edit(/tests/**)"] } }
```

It is blunt: it also blocks the agent from **adding** tests. In VS Code, `"chat.tools.edits.autoApprove":
{ "**/tests/**": false }` makes Copilot ask before it edits a test file, which keeps you in the loop
without blocking new tests. The last matching pattern decides, so if you also auto-approve everything
with `"**/*": true`, put the `false` entries after it, and add the guard and hook files themselves
(`**/scripts/**`, `**/.github/hooks/**`, `**/.claude/**`) so the agent can't edit its own guard without
asking.

### 3. Make it a precise hook

A **PreToolUse hook** (chapter 05) runs before every edit and can apply any rule you can script. This one,
from the Demo 13 solution, blocks edits to test files that already exist in git and allows new ones:

```powershell
$payload = [Console]::In.ReadToEnd() | ConvertFrom-Json
$path = $payload.tool_input.file_path
if (-not $path) { exit 0 }

$root = Split-Path -Parent $PSScriptRoot
$relative = [System.IO.Path]::GetRelativePath($root, $path).Replace('\', '/')
if (-not $relative.StartsWith('tests/', [StringComparison]::OrdinalIgnoreCase)) { exit 0 }

git -C $root cat-file -e "HEAD:$relative" 2>$null      # exit code 0: the file is in the last commit
if ($LASTEXITCODE -eq 0) {
    [Console]::Error.WriteLine("Blocked: $relative is an existing test. Fix the code, not the test.")
    exit 2                                             # 2 blocks the edit; stderr goes to the agent
}
exit 0
```

The agent sees the message and adjusts, which is better than a silent refusal.

### 4. Check the diff, and let CI decide

None of the above stops a shell command from rewriting a file, so the last line of defense is review:

```powershell
git diff --stat -- tests/        # should list only new test files, if anything
```

And the pull request's CI build runs the same gates on a clean machine, where nothing on your laptop can
change the result.

---

## Summary

- The **verify loop** runs four gates inside the agent's loop: **build**, **analyzers**, **tests**, and
  **format**, cheapest first, stopping at the first red one.
- `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended`, and `EnforceCodeStyleInBuild` in
  `Directory.Build.props` turn AI-typical mistakes (missing cancellation tokens, culture bugs, nullable
  slips) into **build failures** the agent fixes itself.
- When a rule doesn't fit, **scope the exception** in `.editorconfig` or with `[SuppressMessage]` and a
  justification; never switch it off solution-wide.
- `dotnet test --solution` output names the test, the data, and **expected versus actual**; group failures
  by **root cause**.
- `dotnet format --verify-no-changes` makes formatting a **gate** with an exit code.
- Put the gates and the rules in the **prompt** and the **instruction file**, and require the final summary
  lines before "done".
- Guard against **fixing the test instead of the code** in layers: explicit rules, deny rules, a
  **PreToolUse hook**, and a final `git diff` plus **CI**.
- Practice it in [Demo 13](../../demos/13-the-verify-loop/) and
  [Activity 13](../../activities/13-the-verify-loop/).
