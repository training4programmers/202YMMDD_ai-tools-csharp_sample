# Demo 21: C# Pitfalls and Verification (ToolsApp / Colors)

Instructor demo paired with [Activity 21](../../activities/21-csharp-pitfalls-and-verification/). An
assistant's change that builds cleanly turns out to need a package that doesn't exist, uses obsolete
and culture-sensitive APIs, and hides two nulls behind `!`; you verify, rebuild it with the BCL, turn
on the quality gates, and then review what no gate can see.

## Concept

- **A green build proves very little about AI code.** The starting point compiles with a single
  warning, yet it has an obsolete hashing API, three culture bugs, three dropped cancellation tokens,
  interpolated logging, and two `!` operators that turn a 404 into a 500.
- **Verify a dependency before you add it.** The assistant's follow-up suggestion recommends
  `ToolsApp.ColorSpaces.Extensions`, a package named after your own namespace that doesn't exist.
  Hallucinated names get registered by attackers ("slopsquatting"), so "not found" means reject.
- **Ask what the AI meant, then use the BCL.** The invented package was supposed to convert HSL to RGB:
  about thirty lines of math, provable with a round-trip test over every color.
- **Turn warnings into gates.** `TreatWarningsAsErrors`, `AnalysisLevel` `latest-recommended`,
  `EnforceCodeStyleInBuild`, and `dotnet format --verify-no-changes` take the starting point from 1
  warning to 18 errors (19 once you also opt in to `CA1849`), and an agent can fix most of them on its
  own.
- **Gates don't replace review.** The `!` bugs, silently skipped input, and house-style deviations
  pass every tool; a person (or a prompted review pass) has to find them.

![Generated code flows through five gates in order: compile with nullable enabled and warnings as errors, analyzers at latest-recommended plus opt-in CA rules, dotnet format verify-no-changes, tests including other cultures, and human review; a failure at any gate goes to a reject step where the author or agent fixes the code, then the change starts again](../../diagrams/png/12-quality-gates-pipeline.png)

*Figure: Every change clears the same gates; the cheapest run first, and review is for what compiles cleanly and is still wrong.*

## How to run

From the course repo root, copy the starting point to a lab folder outside the repo (so the repo's own
AGENTS.md/CLAUDE.md don't leak into the session) and open it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 21 -Open
cd ~\labs\demo-21-csharp-pitfalls-and-verification
dotnet build
dotnet test --solution ToolsApp.slnx
```

The build succeeds with one warning (`SYSLIB0021` for `SHA256Managed`), and all 18 tests pass. That
contrast is the opening point.

Before class:

- **One AI tool in agent mode, on a frontier tier:** GitHub Copilot agent mode in Visual Studio 2026 or
  VS Code, or a Claude Code session. The agent needs to run `dotnet` commands; in Claude Code, allow
  `dotnet build`, `dotnet test`, `dotnet format`, and `dotnet package search` so the loop doesn't stop
  for every command.
- **Network access to nuget.org** for `dotnet package search`.
- **Optional:** the Microsoft Learn MCP server (`https://learn.microsoft.com/api/mcp`) connected, to
  show docs grounding if the HSL step goes off track ([Tools Review](TOOLS-REVIEW.md)).
- **Have open:** `ai-suggestion/SUGGESTION.md` and `https://www.nuget.org/packages/ToolsApp.ColorSpaces.Extensions`
  (it returns 404).
- Budget about 20 minutes, with the *Short on time* list at the top of the Demo trail: Step 1 (5),
  Step 2 (8), Step 3 (4), Step 4 (3). The full trail takes about 35: Step 1 (5), Step 2 (10), Step 3
  (10), Step 4 (10). Steps 2–4 are agent runs of two to four minutes each when recorded; talk through
  the code while they run.

If the live run goes sideways, `start-lab.ps1 -Demo 21 -Solution` copies the recorded outcome.

## Demo trail

> **Short on time (about 20 minutes).** Run Steps 1 and 2 live. In Step 3, run the one-line build that
> shows the 18 errors, then show the gated result from the `-Solution` copy instead of the agent run. In
> Step 4, skip the agent's review pass and walk the human-pass bullets in the solution, starting with
> the two `palette!` in your lab copy that no gate flagged.

**Code to open (in order):**

1. `src/ToolsApp.Api/Endpoints/PaletteTransferEndpoints.cs`: the "AI-generated" change (header
   comment). Point at `SHA256Managed`, `name.ToLower()`, `line.StartsWith("name")`,
   `db.SaveChangesAsync()` with no token, the interpolated `LogInformation`, and the two `palette!`.
   Ask the room which of these the build will flag. (Only the first.)
2. `ai-suggestion/SUGGESTION.md`: the assistant's follow-up. Read the confident claim aloud: "the
   companion package for ToolsApp's color types … the standard way to convert between HSL and RGB."
3. `src/ToolsApp.Domain/ColorMath.cs`: `ToHsl` exists; there's no way back. That's what the invented
   package was for.
4. `Directory.Build.props` and `.editorconfig`: no gates yet. `.editorconfig` already says
   `file_scoped:warning`, but nothing enforces it in the build.

**Step 1: Verify the package before you add it.** Prompt (agent mode, Copilot or Claude Code):

```text
Read ai-suggestion/SUGGESTION.md. Before we apply it, verify the package it recommends: does ToolsApp.ColorSpaces.Extensions exist on nuget.org? Use dotnet package search with --exact-match, and tell me exactly what you ran and what it printed. Don't add any packages or change any code yet.
```

In the recorded run, the agent ran
`dotnet package search ToolsApp.ColorSpaces.Extensions --exact-match --source https://api.nuget.org/v3/index.json`,
reported `No results found.`, called it a likely hallucination, and named slopsquatting as the risk.
Point out three things:

- **Insist on the command and its output**, not a summary. Run it yourself to show the room.
- **Why "not found" isn't "wait for it".** Anyone can register that ID tomorrow; nuget.org prefix
  reservation protects `Microsoft.*`, not `ToolsApp.*`.
- **The alternative needs verifying too.** The recorded agent suggested "something like
  `SixLabors.ImageSharp`" as a real library to consider. It is real, but it's a large image-processing
  library with its own license terms, for thirty lines of math. Show what a real, focused package
  looks like for contrast:

```powershell
dotnet package search Colourful --take 1 --source https://api.nuget.org/v3/index.json
# | Colourful | 3.2.0 | tompazourek | 301,318 |    (downloads at the time of writing)
```

Then ask: do we need a package at all?

**Step 2: Ask what the AI meant, then use the BCL.** Prompt (agent mode):

```text
What were HslColor.Parse(...).ToRgb() and ToHexString() supposed to do? Implement the same behavior without a package: add ColorMath.FromHsl(Hsl) and ColorMath.TryParseHsl(string?, out Hsl) for CSS hsl() values, parsing numbers with the invariant culture, and let the palette importer accept hex or hsl() values. Add xUnit tests, including FromHsl(ToHsl(rgb)) == rgb across many colors and a parse test under de-DE. Run dotnet build and dotnet test --solution ToolsApp.slnx.
```

The recorded run (about 3.5 minutes) explained the three calls, added both methods, and wrote 17 tests
(35 total, all passing). Review beats:

- **It caught a bug the suggestion would have shipped:** `line.Split(',')` splits
  `hsl(38, 76%, 81%)` into four fields. It now splits at the first comma only. Credit it.
- **It made a product decision silently:** malformed lines are now *skipped with a log warning*. A
  partial import that returns 200 is a data-integrity bug; note it for Step 4.
- **It added scope:** the parser also accepts an alpha value and ignores it, which quietly changes the
  color. Ask whether anyone asked for that.
- **Check the suggestion's own example.** It claims `hsl(38, 76%, 81%)` is `#F4D8A8`. Run the new code:
  it's `#F3D8AA` (and `#F4D8A8` is about `hsl(37.9, 77.6%, 80.8%)`). The model invented the output too.
  The round-trip test is the proof that matters: in the recorded outcome it holds for a 140,608-color
  sample, and a one-off check confirmed all 16,777,216.

```powershell
dotnet build      # still 1 warning: SYSLIB0021
dotnet test --solution ToolsApp.slnx
```

**Step 3: Turn on the gates.** First show what the gates will find, without committing to them:

```powershell
dotnet build -p:TreatWarningsAsErrors=true -p:AnalysisLevel=latest-recommended -p:EnforceCodeStyleInBuild=true
# Build FAILED.  18 Error(s)   (on the original code; SYSLIB0021, CA1304/CA1311, CA1305, CA1310,
#                               CA2016 x3, CA1848, CA2254, CA1873, CA1850, CA1872, CA1866, IDE0161)
```

Then let the agent do the work. Prompt (agent mode):

```text
Turn on our quality gates in Directory.Build.props: TreatWarningsAsErrors, AnalysisLevel latest-recommended, and EnforceCodeStyleInBuild. Then make dotnet build pass with zero warnings by fixing the code, not by suppressing. If a rule genuinely doesn't fit, stop and explain it to me before you suppress anything. Finish with dotnet test --solution ToolsApp.slnx and dotnet format --verify-no-changes.
```

The recorded run fixed every finding in code (`SHA256.HashData` + `Convert.ToHexString`, invariant
casing, ordinal comparisons, forwarded tokens, a `[LoggerMessage]` method, a file-scoped namespace),
and then **stopped**: a second wave of `CA1707` errors appeared in the test project once the API
compiled, and it asked whether to rename the tests or suppress the rule. That's the behavior you
asked for. Decide out loud, then answer:

```text
Agreed: turn off CA1707 for the tests folder only, with the justification as a comment next to the setting. Keep everything else enforced. Then finish: dotnet build, dotnet test --solution ToolsApp.slnx, and dotnet format --verify-no-changes.
```

It added a `tests/.editorconfig` that turns the rule off, with the justification as comments (the
recorded outcome in `solution/` uses a `[tests/**/*.cs]` section in the root file instead; both are
fine). Show the result:

```powershell
dotnet build                          # 0 Warning(s), 0 Error(s)
dotnet format --verify-no-changes     # no output; exit code 0
git diff --stat
```

Mention the gap the agent didn't cover: `latest-recommended` doesn't include `CA1849` or `CA2000`. The
recorded outcome opts in to both in `.editorconfig`, and `CA1849` immediately finds `app.Run()` after an
`await` in the *baseline* `Program.cs`: on the original code, that opt-in makes the 18 errors 19.

**Step 4: Review what no gate can see.** Prompt (agent mode):

```text
The gates are green, so now review PaletteTransferEndpoints.cs for bugs that compiled cleanly and that no analyzer flagged: null-forgiving operators, missing input validation, and any request that returns 500 when it should return 404 or 400. For each one, write a failing test first, then fix it. Run dotnet test --solution ToolsApp.slnx at the end.
```

The recorded run (about 3 minutes) found four: both `palette!` (500 instead of 404 for an unknown
palette), the silent skip from Step 2 (now a 400 that lists every bad line, with nothing saved), and
empty color names. Five new tests, 40 total. Then do the human pass and show what the recorded outcome
in `solution/` changed beyond the agent:

- **House style.** The agent kept `Task<IResult>` and `Results.*`; the codebase uses
  `Results<Ok<T>, NotFound, ValidationProblem>` and `TypedResults`, which OpenAPI and the compiler
  can check. The outcome also returns an `ImportResultDto` record instead of an anonymous object.
- **ETag details.** `IfNoneMatch == etag` only matches a single, exact header value; the outcome uses
  the typed `EntityTagHeaderValue` comparison. And `AppendLine` writes `\r\n` on Windows and `\n` on
  Linux, so the same palette had different ETags per server OS; the outcome writes `\n`.
- **Header detection.** "Skip any line that starts with `name`" also skips a color named "namesake";
  the outcome only treats line 1 as a header.
- **Testability.** The outcome moves CSS rendering and CSV parsing into `ToolsApp.Domain`
  (`PaletteCss`, `PaletteCsv`), so the tr-TR slug test and the de-DE parse test run without a server.
- **Provenance.** Delete the "Generated by an AI assistant" header once the code is reviewed, and
  record the package verdict in `ai-suggestion/VERIFICATION.md`.

```powershell
dotnet test --solution ToolsApp.slnx   # recorded outcome: 49 tests
```

**Key teaching beat:** the starting code passed the default build with one warning. The gates turned
that into 18 specific, fixable errors, and an agent cleared all of them in minutes, because each
error message is a precise prompt. But the two worst bugs in the file, the `!` operators that turn a
404 into a 500, passed every gate, and so did a silent partial import the agent introduced itself.
Verification in C# is layered: the package registry, the compiler, the analyzers, `dotnet format`,
the tests, and then a person asking "what happens when this is null?"

## Reference

- Concepts: [Module 12: C#-Specific Pitfalls and Verification](../../docs/module-12-csharp-pitfalls-and-verification/),
  especially [Hallucinated NuGet Packages and API Versions](../../docs/module-12-csharp-pitfalls-and-verification/01-hallucinated-nuget-packages-and-api-versions.md),
  [Stale Framework Knowledge](../../docs/module-12-csharp-pitfalls-and-verification/02-stale-framework-knowledge.md),
  [Nullable and Analyzer Warnings as Quality Gates](../../docs/module-12-csharp-pitfalls-and-verification/03-nullable-and-analyzer-warnings-as-quality-gates.md), and
  [EditorConfig, Analyzers, and dotnet format](../../docs/module-12-csharp-pitfalls-and-verification/04-editorconfig-analyzers-and-dotnet-format.md)
- Tools: [Tools Review](TOOLS-REVIEW.md)
- Practice: [Activity 21](../../activities/21-csharp-pitfalls-and-verification/)
