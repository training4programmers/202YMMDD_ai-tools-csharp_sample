# EditorConfig, Roslyn Analyzers, and `dotnet format` for Generated Code

Chapter 03 picked the warnings worth gating on. This chapter is about the machinery: where those
settings live, how to scope them, how to make style rules run in the build instead of only in the IDE,
and how `dotnet format` turns "the code should look like this" into a check that passes or fails. It
ends with the part assistants get wrong most often: suppressions. An agent told to "make the build
green" will happily add `#pragma warning disable` or `<NoWarn>`, and a good suppression looks almost
the same as a bad one in a diff.

Everything here is tool-neutral. GitHub Copilot and Claude Code both read build output, and both
respect your `.editorconfig` only to the extent the build enforces it. That's the point: put the rules
where the compiler checks them, and every assistant has to follow them.

---

## EditorConfig: One File, Every Tool

`.editorconfig` is the shared home for formatting rules, code-style preferences, naming rules, and
analyzer severities. Visual Studio 2026, VS Code with C# Dev Kit, Rider, the .NET SDK, and
`dotnet format` all read it. The course snapshots start with one at the solution root:

```ini
root = true

[*.cs]
csharp_style_namespace_declarations = file_scoped:warning
csharp_style_prefer_primary_constructors = true:suggestion
dotnet_style_prefer_collection_expression = when_types_loosely_match:suggestion
```

Three things to know:

- **Files are hierarchical.** Tools read every `.editorconfig` from the source file's folder up to the
  one marked `root = true`, and the closest setting wins. A `tests/.editorconfig` can override the
  root file for test code only.
- **Sections scope settings.** `[*.cs]` applies to all C# files; `[tests/**/*.cs]` in the root file
  applies only under `tests/`; later sections override earlier ones.
- **Severity is per rule.** Code-style options take a `:severity` suffix, and any diagnostic can be
  set with `dotnet_diagnostic.<ID>.severity`:

```ini
[*.cs]
# Opt in to rules that latest-recommended leaves off (chapter 03).
dotnet_diagnostic.CA1849.severity = warning
dotnet_diagnostic.CA2000.severity = warning

# Or a whole category at once.
dotnet_analyzer_diagnostic.category-Globalization.severity = warning
```

The severities are `none`, `silent`, `suggestion`, `warning`, and `error`. Only `warning` and `error`
show up in a command-line build, so only they can act as gates. A `suggestion` is a hint in the IDE
that an agent running `dotnet build` never sees.

That last point matters with AI tools. The baseline `.editorconfig` prefers primary constructors and
collection expressions as *suggestions*. An assistant working in the IDE may pick them up from the
light-bulb hints; an agent in a terminal won't know they exist unless your instruction file says so,
or you raise them to `warning`.

---

## Making Style Rules Run in the Build

Code-style rules (the `IDExxxx` IDs) run in the IDE by default, **not** in `dotnet build`. So the
baseline's `file_scoped:warning` setting is a squiggle in the editor and nothing in a terminal build,
which is exactly where agents and CI live. Turn them on for the build:

```xml
<PropertyGroup>
  <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
</PropertyGroup>
```

Now every code-style rule configured as `warning` or `error` runs in the build. Demo 21's AI-generated
file uses a block-scoped namespace; with the property on, the build reports what the IDE always knew:

```text
src\ToolsApp.Api\Endpoints\PaletteTransferEndpoints.cs(8,11): warning IDE0161: Convert to file-scoped namespace
```

A couple of style rules only run in the IDE for performance reasons, and one popular rule has a
catch: `IDE0005` (remove unnecessary `using` directives) only reports on build when the project also
generates an XML documentation file (`<GenerateDocumentationFile>true</GenerateDocumentationFile>`),
which in turn turns on `CS1591` warnings for public members without XML docs. Decide whether you want
both before you enable it.

---

## Choosing an AnalysisLevel

The code-analysis rules (`CAxxxx`) ship with the .NET SDK. Two MSBuild properties decide which run as
warnings:

| Value | What you get |
|-------|--------------|
| `latest` (the default) | The default set for the latest release: a few rules as warnings, many as IDE suggestions |
| `latest-minimum` | More rules as build warnings |
| `latest-recommended` | More again: the set chapter 03's culture, logging, and cancellation rules come from |
| `latest-all` | Everything (a few legacy rules excepted): very noisy |
| `10` / `10-recommended` | Pin to the .NET 10 rule set, so an SDK upgrade doesn't add new warnings |

`AnalysisLevel` takes the compound `<version>-<mode>` form; the older `AnalysisMode` property sets just
the mode, and `AnalysisLevel` overrides it. You can also set a level per category, which is a good way
to be strict where it matters most:

```xml
<PropertyGroup>
  <AnalysisLevel>latest-recommended</AnalysisLevel>
  <AnalysisLevelSecurity>latest-all</AnalysisLevelSecurity>
  <AnalysisLevelGlobalization>latest-all</AnalysisLevelGlobalization>
</PropertyGroup>
```

Pinning matters more than it looks. `latest` moves when you install a new SDK, so a teammate's
machine (or the CI image) can suddenly report warnings yours doesn't. With `TreatWarningsAsErrors`
on, that's a broken build nobody changed. Pin a number (`10-recommended`) and raise it on purpose.

### Third-party analyzers

The SDK's rules aren't the only ones worth having. A few widely used analyzer packages:

| Package | What it adds |
|---------|--------------|
| `Microsoft.VisualStudio.Threading.Analyzers` | Async correctness: `async void`, blocking waits, `JoinableTaskFactory` rules |
| `xunit.analyzers` | Comes with xUnit; for example `xUnit1051` when a test doesn't pass `TestContext.Current.CancellationToken` |
| `Meziantou.Analyzer`, `Roslynator.Analyzers` | Broad sets of correctness, performance, and style rules |
| `StyleCop.Analyzers` | Formatting and documentation conventions |
| `SonarAnalyzer.CSharp` | Bug patterns and security hotspots |

Treat them like any other dependency (chapter 01): verify, pin centrally, and with central package
management you can apply an analyzer to every project at once with `GlobalPackageReference` in
`Directory.Packages.props`. Turn on one package at a time, or the first build drowns you.

---

## `dotnet format`: Fix It, or Fail

`dotnet format` ships with the SDK. It reads `.editorconfig` and applies fixes in three passes, which
you can also run separately:

```powershell
dotnet format ToolsApp.slnx              # whitespace, then style, then analyzers
dotnet format whitespace ToolsApp.slnx   # indentation, spacing, line endings
dotnet format style ToolsApp.slnx        # IDE code-style rules
dotnet format analyzers ToolsApp.slnx    # CA and third-party analyzer fixes
```

By default it fixes diagnostics at `warning` severity and above; `--severity info` widens that,
`--diagnostics IDE0161 CA1310` narrows it to specific rules, and `--include` limits it to files or
folders (relative paths).

Add **`--verify-no-changes`** and it changes nothing; it exits non-zero if any file *would* change.
That's the gate. On Demo 21's starting code, with the course's gates turned on (the three properties
from chapter 03 plus the `.editorconfig` above, which opts in to `CA1849`), it fails like this
(trimmed):

```text
src\ToolsApp.Api\Endpoints\PaletteTransferEndpoints.cs(8,11): error IDE0161: Convert to file-scoped namespace
src\ToolsApp.Api\Endpoints\PaletteTransferEndpoints.cs(73,56): error CA1310: The behavior of 'string.StartsWith(string)' could vary ...
src\ToolsApp.Api\Endpoints\PaletteTransferEndpoints.cs(91,35): error CA2254: The logging message template should not vary between calls ...
src\ToolsApp.Api\Program.cs(43,1): error CA1849: 'WebApplication.Run(string?)' synchronously blocks. Await 'WebApplication.RunAsync(string?)' instead.
```

```powershell
$LASTEXITCODE
# 2
```

Notice what's missing. The build reports 19 findings for the same code (the 18 errors Demo 21 shows
with the three properties alone, plus the opted-in `CA1849`); `dotnet format` reported 7. It
reports what it knows how to fix, and many rules (and every `CA1707`-style naming rule) have no
automatic fix. So `dotnet format --verify-no-changes` **complements** the build gate; it doesn't
replace it. Run both.

### As a CI gate

The format check is fast and needs no special tooling. In Azure Pipelines (or any CI), run it after
restore and before the build:

```yaml
- script: dotnet format ToolsApp.slnx --verify-no-changes --verbosity minimal
  displayName: Verify formatting and fixable analyzer rules
- script: dotnet build ToolsApp.slnx --no-restore
  displayName: Build (warnings are errors)
- script: dotnet test --solution ToolsApp.slnx --no-build
  displayName: Test
```

A pull request from a person, an IDE agent, or a cloud agent then gets the same answer. Module 13
builds this into a full Azure Pipelines quality gate.

### As an agent hook

Better still, fix formatting before the agent moves on, so the gate never fails for something a tool
could have fixed. Claude Code's hooks can run a command after every edit:

```json
{
  "hooks": {
    "PostToolUse": [{
      "matcher": "Edit|Write",
      "hooks": [{
        "type": "command",
        "command": "powershell.exe",
        "args": ["-NoProfile", "-ExecutionPolicy", "Bypass", "-File", "${CLAUDE_PROJECT_DIR}/.claude/hooks/dotnet-format.ps1"],
        "timeout": 120
      }]
    }]
  }
}
```

```powershell
# .claude/hooks/dotnet-format.ps1: format the C# file Claude just edited
$in = [Console]::In.ReadToEnd() | ConvertFrom-Json
$file = $in.tool_input.file_path
if (-not $file -or $file -notmatch '\.cs$') { exit 0 }
Set-Location $env:CLAUDE_PROJECT_DIR
$rel = Resolve-Path -LiteralPath $file -Relative
dotnet format ToolsApp.slnx --include $rel --verbosity quiet
if ($LASTEXITCODE -ne 0) { [Console]::Error.WriteLine("dotnet format failed: $rel"); exit 2 }
exit 0
```

This example is assembled from Claude Code's documented hook pieces; test it in your own repository
before you rely on it. `--include` needs a relative path, and a full `dotnet format` on every edit is
slow in a large solution, so consider `"async": true` or `dotnet format whitespace --folder --include
$rel` for the per-edit hook, and leave the full check to CI. Module 07 covers hooks in depth.

> **GitHub Copilot:** Put "run `dotnet format` and `dotnet build` before you finish, and fix what
> they report" in `.github/copilot-instructions.md`. Agent mode in Visual Studio 2026 and VS Code
> runs the commands in a terminal (VS Code asks before running commands that aren't auto-approved),
> and the Copilot cloud agent runs them in its own environment. The CI gate catches anything that
> slips through.
>
> **Claude Code:** Use the `PostToolUse` hook above to format after each edit, and put the build and
> format commands in `CLAUDE.md` so the agent runs the full check before it reports back. Allow them
> in `.claude/settings.json` (`"allow": ["Bash(dotnet format *)", "Bash(dotnet build *)"]`, plus the
> same rules as `PowerShell(...)` on Windows, where Claude Code may run commands through its PowerShell
> tool) so the loop doesn't stop for permission each time.

---

## Suppressions Done Right

Sometimes a rule really doesn't fit. Demo 21's recorded agent hit one: `CA1707` ("Identifiers should
not contain underscores") fired on every test method, because the team names tests as sentences
(`Import_with_a_bad_line_returns_400`). Renaming forty tests to satisfy a rule meant for public API
names would make the suite worse. That's a legitimate suppression. What makes it *done right* is
scope and a written reason.

From best to worst:

**1. Fix the code.** Most warnings point at a real problem; chapter 03's examples all had code fixes.

**2. Scope the severity in `.editorconfig`, with the reason next to it.** Narrowest folder, one rule:

```ini
[tests/**/*.cs]
# Test names are sentences (Method_condition_result) by team convention. CA1707 protects public API
# names that other code calls; nothing calls a test method by name.
dotnet_diagnostic.CA1707.severity = none
```

**3. Suppress one member with an attribute and a `Justification`.** Activity 21's solution keeps
`PriceCalculator.Calculate` as an instance method even though `CA1822` says it could be static:

```csharp
[SuppressMessage("Performance", "CA1822:Mark members as static",
    Justification = "PriceCalculator is a DI service injected into the quote endpoint. Keeping Calculate " +
        "an instance method lets it take dependencies (tax tables, coupon rules) without changing callers.")]
public Quote Calculate(IReadOnlyList<QuoteLine> lines, string? couponCode = null)
```

**4. `#pragma warning disable` around the fewest lines possible, with a comment, and `restore`
right after.** For the occasional unavoidable call into an obsolete API you can't replace yet.

**5. `GlobalSuppressions.cs` with a `Target`.** The IDE's "Suppress in suppression file" action
generates these; keep the `Scope` and `Target` narrow and replace the placeholder justification it
writes with a real one.

**6. `<NoWarn>` in a project file.** Turns the rule off for the whole project, invisibly. A last
resort, and a red flag in any AI diff.

When you review an assistant's change, look for new suppressions before you look at anything else.
They're how a model makes a failing gate pass without fixing anything:

```powershell
git diff -U0 | Select-String -Pattern '^\+.*(#pragma warning disable|SuppressMessage|NoWarn|\.severity = (none|silent|suggestion))'
```

And say so in the instructions, so the agent asks instead of guessing:

```markdown
- Never add #pragma warning disable, [SuppressMessage], <NoWarn>, or a lower .editorconfig severity
  without asking first. If a rule doesn't fit, explain why and propose the narrowest scope.
```

---

## Summary

- **`.editorconfig`** is the one place every tool reads for formatting, code style, naming, and
  analyzer severity; it's **hierarchical**, **sectioned by path**, and only `warning` and `error`
  severities act as gates in a command-line build.
- **`EnforceCodeStyleInBuild`** makes `IDExxxx` style rules run in `dotnet build`, where agents and CI
  see them; `IDE0005` additionally needs `GenerateDocumentationFile`.
- **`AnalysisLevel`** picks the CA rule set: `latest-recommended` suits application code, per-category
  levels let you be stricter where it matters, and a **pinned number** keeps SDK upgrades from adding
  surprise errors.
- **`dotnet format`** fixes whitespace, style, and analyzer issues; **`--verify-no-changes`** makes it a
  gate (non-zero exit), but it only reports what it can fix, so run it **alongside** the build, in CI
  and as an **agent hook**.
- **Suppressions** need a **reason** and the **narrowest scope**: fix the code first, then a scoped
  `.editorconfig` severity, then `[SuppressMessage]` with a `Justification`; `<NoWarn>` is a last
  resort. Review every new suppression in an AI diff, and tell the assistant to **ask first**.
