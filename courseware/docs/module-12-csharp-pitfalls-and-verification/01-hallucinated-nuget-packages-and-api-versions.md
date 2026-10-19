# Hallucinated NuGet Packages and API Versions

Sooner or later an assistant will tell you, with complete confidence, to run
`dotnet add package` for something that doesn't exist, or to call a method that a real package
removed three major versions ago. The first mistake usually fails loudly at restore time. The second
fails at compile time. Neither is the dangerous case. The dangerous case is a package name that didn't
exist yesterday and does exist today, because someone noticed that models keep inventing it.

This chapter explains why models invent packages and members, what "slopsquatting" is, and a
four-step workflow for verifying any dependency an assistant suggests before it reaches your
`Directory.Packages.props`. It ends with the rules to put in your instruction files so the assistant
does the first round of checking for you. Demo 21 and Activity 21 practice all of it.

---

## Why Models Invent Packages and Members

A model doesn't look packages up. It predicts the next token (Module 01), and package IDs and member
names are just text with very regular patterns. NuGet IDs are dotted, compositional names, so a model
that has seen `Microsoft.Extensions.Http`, `Microsoft.Extensions.Caching.Memory`, and
`System.Net.Http.Json` can produce `Microsoft.Extensions.Http.Json` with no sense that it never
existed:

```text
error NU1101: Unable to find package Microsoft.Extensions.Http.Json. No packages exist with this id
in source(s): nuget.org
```

A few patterns make an invented name more likely:

- **Your own namespace.** The model sees `ToolsApp.Domain` and `ToolsApp.Api` in context, so
  `ToolsApp.ColorSpaces.Extensions` looks like the obvious companion package. A suggested package
  whose name starts with your company or project name is either internal (and should come from your
  private feed) or invented.
- **Concepts from other ecosystems.** Python has `colorsys`, JavaScript has a dozen color libraries.
  Ask for HSL conversion in C# and the model may "translate" one of those into a NuGet package that
  was never written.
- **Plausible suffixes.** `.Extensions`, `.Core`, `.Abstractions`, `.Net`, and `.Validators` attach
  to almost anything.
- **A confident explanation.** The invented package usually arrives with a description ("the
  standard way to convert between HSL and RGB in .NET") and example code, which makes it feel
  checked when it isn't.

Members get invented, or resurface from long-gone versions, the same way. The model knows
`GetFromJsonAsync` and `PostAsJsonAsync`, so `GetJsonAsync` (real once, in Blazor's experimental and
preview packages, and gone before Blazor WebAssembly shipped) resurfaces and feels right. It knows
`List<T>.RemoveAll`, so `RemoveWhere` (which belongs to `HashSet<T>`) feels right on a list too:

```csharp
palette.Colors.RemoveWhere(c => c.Hex == "#000000");
// error CS1061: 'List<Color>' does not contain a definition for 'RemoveWhere' ...
```

The compiler catches invented members immediately, and so does an agent that runs `dotnet build`. The
harder cases are members that exist, but in a different version than the one you're using.

---

## Version Mismatches: Real Package, Wrong API

A model's training data spans many years of a package's history. Stack Overflow answers, blog posts,
and repositories written against version 12 outnumber the ones written against version 33, so the
model happily blends them. The package is real; the code is for a version you don't have.

CsvHelper is a good example. It's a widely used, well-maintained CSV library, and older major versions
had a constructor that took only a `TextReader`, plus settable configuration properties. An assistant
writing from that era produces *(illustrative)*:

```csharp
using var csv = new CsvReader(reader);
csv.Configuration.HasHeaderRecord = true;
csv.Configuration.Delimiter = ",";
```

Against the current release (33.x at the time of writing), that code doesn't compile:

```text
error CS1503: Argument 1: cannot convert from 'System.IO.StringReader' to 'CsvHelper.IParser'
error CS0200: Property or indexer 'IReaderConfiguration.HasHeaderRecord' cannot be assigned to -- it is read only
error CS0200: Property or indexer 'IParserConfiguration.Delimiter' cannot be assigned to -- it is read only
```

The current API wants a culture and an immutable configuration. The quick fix assistants often reach
for, `new CsvReader(reader, CultureInfo.CurrentCulture)`, compiles but reintroduces a culture bug
(chapter 03): on a de-DE machine the delimiter becomes `;`, and even with the delimiter set to `,`,
`18.99` silently parses as `1899`. Use the invariant culture instead:

```csharp
var config = new CsvConfiguration(CultureInfo.InvariantCulture)
{
    HasHeaderRecord = true,
    PrepareHeaderForMatch = args => args.Header.ToUpperInvariant(), // "isbn" matches Isbn
};
using var csv = new CsvReader(reader, config);
```

The same thing happens with the .NET platform and its test tooling:

| What the model writes | Why it's wrong today |
|-----------------------|--------------------|
| `using Xunit.Abstractions;` for `ITestOutputHelper` | xUnit v3 removed that namespace: `CS0234`. The type now lives in `Xunit`. |
| `builder.Services.AddSwaggerGen()` in a .NET 10 web API | The templates dropped Swashbuckle in .NET 9; built-in OpenAPI is `AddOpenApi()` and `MapOpenApi()`. |
| `new JsonSerializerOptions { IgnoreNullValues = true }` | Obsolete (warning `SYSLIB0020`, an error under `TreatWarningsAsErrors`); use `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull`. |
| `<PackageReference Include="coverlet.collector" />` with xUnit v3 on Microsoft.Testing.Platform | `coverlet.collector` is VSTest-only; use `coverlet.MTP` or `Microsoft.Testing.Extensions.CodeCoverage`. |

The cure is the same in every case: tell the model which versions you're on (chapter 02), and let the
compiler judge the result.

---

## Slopsquatting

**Slopsquatting** is registering a package name that AI models hallucinate, so that the next
developer who follows the model's advice installs the attacker's code. It's a twist on typosquatting
(registering `Newtonsoft.Jsom` and waiting for typos), aimed at machines instead of tired humans.

It works because hallucinations repeat. Researchers who generated 576,000 code samples with 16
code-generation models found an average of at least 5.2% hallucinated packages for commercial models
and 21.7% for open-source models, and more than 205,000 unique invented names. When they re-ran 500
prompts that had produced a hallucination, ten times each (Python only), 43% of the invented packages
came back in all ten runs and 39% never came back
([Spracklen et al., USENIX Security 2025](https://www.usenix.org/conference/usenixsecurity25/presentation/spracklen)).
The study covered Python and JavaScript, not NuGet, but nothing about the mechanism is specific to
those ecosystems.

Three things follow for .NET teams:

- **`NU1101` is the lucky outcome.** "Unable to find package" today means nobody has registered the
  name *yet*. Treat it as "rejected", never as "try again later".
- **Prefix reservation helps, but only for real prefixes.** nuget.org lets owners reserve ID
  prefixes, and reserved IDs show a verified checkmark. That protects `Microsoft.*` and many vendor
  names. It does nothing for `ToolsApp.ColorSpaces.Extensions` or `Isbn13.Net`, which nobody owns.
- **Agents act on names.** In agent mode, the model can run `dotnet add package` itself. If the
  command succeeds, the package's MSBuild targets and analyzers run inside your build on the next
  `dotnet build`. Keep package installs behind an approval (see the last section).

---

## The Verify Workflow

Every new `<PackageReference>` in an AI-generated diff is a decision you own. Four questions, in
order, and any "no" stops the change:

![Flow from "AI suggests dotnet add package X" through four checks: does it exist (search nuget.org with exact match), can you trust it (owner, downloads, repository, recent releases), does the API exist (compile a spike against that version), and is it allowed (approved list, license, source mapping), ending in adding it with a centrally pinned version and a lock file; any failed check leads to reject and report, with notes on slopsquatting and asking whether a package is needed at all](../../diagrams/png/12-verify-a-package.png)

*Figure: Verify a package before you add it. Any failed check means reject and report, not "try again later".*

Before the first question, ask a zeroth one: **do we need a package at all?** Many suggested
dependencies replace twenty lines you could write with the BCL. Demo 21's HSL conversion and
Activity 21's ISBN-10 conversion are both a few lines of arithmetic.

### 1. Does it exist?

`dotnet package search` queries your configured feeds. Use `--exact-match` so a near miss doesn't
look like a hit, and name the source when you want nuget.org specifically:

```powershell
dotnet package search ToolsApp.ColorSpaces.Extensions --exact-match --source https://api.nuget.org/v3/index.json
```

```text
****************************************
Source: nuget.org (https://api.nuget.org/v3/index.json)
No results found.
```

No results means stop. Don't let the assistant "find a similar package" either; ask it what the
package was supposed to do, then decide whether you need a package for that at all.

### 2. Can you trust it?

Existence proves almost nothing. Without `--exact-match`, the search table shows the owner and total
downloads (the exact-match table leaves them blank); `--verbosity detailed` adds the project URL,
description, vulnerability, and deprecation columns. The output below shows the figures at the time of
writing:

```powershell
dotnet package search Colourful --take 1 --source https://api.nuget.org/v3/index.json
```

```text
| Package ID | Latest Version | Owners      | Total Downloads |
| ---------- | -------------- | ----------- | --------------- |
| Colourful  | 3.2.0          | tompazourek | 301,318         |
```

Then open the package page on nuget.org and check the signals a search table can't show:

| Signal | What you're looking for |
|--------|-------------------------|
| Owners and prefix | A known person or organization; a verified checkmark if the prefix is reserved |
| Downloads and "Used by" | Real adoption, not a few hundred downloads from last week |
| Source repository | A linked repository whose code matches the package, with recent activity |
| Version history | Regular releases; no sudden new owner or a first-ever version published days ago |
| Deprecation and vulnerabilities | No deprecation banner pointing elsewhere; no known advisories |
| License | Compatible with your product; not missing |
| Target frameworks | Supports `net10.0` (or `netstandard2.0`) without pulling in a legacy dependency tree |

A real package can still be the wrong one. When Demo 21's recorded session rejected the invented
package, it offered `SixLabors.ImageSharp` as an alternative: a real, heavily used image-processing
library with its own license terms, and a very large dependency for thirty lines of color math.
Verify the alternative exactly as carefully as the original.

### 3. Does the API exist in that version?

Compile a spike before you touch the solution. A .NET 10 file-based app is ideal: one file, a
`#:package` directive pinned to the version you'd actually add, and `dotnet build`:

```csharp
// spike.cs: does the suggested code compile against the current CsvHelper?
#:package CsvHelper@33.1.0
using CsvHelper;

using var reader = new StringReader("isbn,price\n9781234560010,18.99\n");
using var csv = new CsvReader(reader);          // the suggested code
csv.Configuration.HasHeaderRecord = true;
```

```powershell
dotnet build spike.cs
```

Run the spike outside your solution folder. A file-based app inside a folder whose
`Directory.Packages.props` enables central package management (CPM) fails with `NU1008`, because CPM
forbids versions on individual references.

### 4. Is it allowed?

Your organization's rules decide the rest:

- **An approved list.** Many teams pre-approve `Microsoft.*` and a short list of vetted packages, and
  require a short written review for anything else.
- **The license.** Check it against your policy before the code ships, not after.
- **Package source mapping.** If your company uses a private feed, map which IDs may come from which
  source, so a public package can't impersonate an internal one:

```xml
<!-- nuget.config -->
<packageSourceMapping>
  <packageSource key="nuget.org">
    <package pattern="*" />
  </packageSource>
  <packageSource key="contoso">
    <package pattern="Contoso.*" />
  </packageSource>
</packageSourceMapping>
```

The most specific pattern wins, so `Contoso.Billing` can only come from the `contoso` feed. Without
mapping, a solution that uses CPM and more than one source gets warning `NU1507`.

---

## Adding a Package Properly

When a package passes all four checks, add it in a way that makes the next change reviewable, and
record the decision where reviewers will find it: a few lines in the pull request (or a verification
note in the repo) with the search output, owner, license, and why the BCL wasn't enough.

**Pin the version centrally.** With central package management, the version lives once in
`Directory.Packages.props` and project files reference the package without one:

```xml
<!-- Directory.Packages.props -->
<PackageVersion Include="BenchmarkDotNet" Version="0.15.8" />

<!-- benchmarks/ToolsApp.Benchmarks/ToolsApp.Benchmarks.csproj -->
<PackageReference Include="BenchmarkDotNet" />
```

**Commit a lock file.** `packages.lock.json` records the exact resolved graph, including transitive
packages, so a dependency can't change underneath you between builds:

```xml
<PropertyGroup>
  <RestorePackagesWithLockFile>true</RestorePackagesWithLockFile>
</PropertyGroup>
```

```powershell
dotnet restore --locked-mode   # in CI: fail if the graph no longer matches the lock file
```

**Let NuGet Audit run.** Restore checks your packages against known advisories by default. For
`net10.0` projects, `NuGetAuditMode` defaults to `all`, so transitive packages are audited too, and
findings arrive as warnings `NU1901` to `NU1904` (low to critical). To see the details or fix them:

```powershell
dotnet package list --vulnerable --include-transitive
dotnet nuget why ToolsApp.slnx SQLitePCLRaw.core   # which direct reference pulled this in?
dotnet package update --vulnerable                 # .NET 10: move to the first non-vulnerable versions
```

If you use `TreatWarningsAsErrors` (chapter 03), add `NU1901`–`NU1904` to `WarningsNotAsErrors`
unless you want an advisory published tomorrow to break every build; the
[NuGet audit docs](https://learn.microsoft.com/nuget/concepts/auditing-packages) suggest exactly that,
with a dedicated audit pipeline that does fail.

**Consider a cooling-off period.** Brand-new versions are where hijacked packages show up first.
NuGet in the .NET 11 SDK (release candidate 2 as of October 2026; GA due in November) adds a
per-source cooldown: a `minPublishAgeHours` setting on a package source in `nuget.config`, which
`dotnet nuget add source` and `dotnet nuget update source` set with `--min-publish-age-hours`.
`dotnet package add` and `dotnet package update` then skip versions younger than that, and a restore
of a floating version fails with `NU1020` if no version is old enough. It isn't in a GA SDK yet, so
check the NuGet release notes before you build a policy around it:

```powershell
# .NET 11 SDK (RC2 or later): require 48 hours before a new nuget.org version can be picked
dotnet nuget update source nuget.org --min-publish-age-hours 48 --configfile nuget.config
```

On .NET 10, which this course targets, the equivalent is a review rule: don't take a version published
in the last few days without a reason.

---

## Rules for Your Instruction Files

Most of this workflow can be delegated to the assistant, as long as the instructions are explicit and
the dangerous step still needs your approval. Put rules like these in `AGENTS.md`, `CLAUDE.md`, or
`.github/copilot-instructions.md` (Module 06):

```markdown
## Dependencies

- Never add a NuGet package without asking first. Prefer the BCL and code already in this solution.
- Before proposing a package, run `dotnet package search <id> --exact-match` and show the output.
  Then show the owner, total downloads, latest version, and source repository, from a search without
  `--exact-match` or the nuget.org page (exact-match output leaves owners and downloads blank).
- If a search finds nothing, say so and stop. Never suggest a "similar" package without verifying it
  the same way.
- Versions go in Directory.Packages.props only. Never add a Version attribute to a .csproj.
- After adding a package, run `dotnet build` and show any NU19xx audit warnings.
```

Then make the tool enforce the approval, so a rule the model forgets still holds.

> **GitHub Copilot:** In VS Code, agent mode asks before running terminal commands that aren't
> auto-approved. If your team uses `chat.tools.terminal.autoApprove`, add a `false` entry such as
> `"/^dotnet (add package|package add)\\b/": false` so package installs always need confirmation.
> That holds only at the default permission level: the **Allow all** level and **Autopilot** run
> every tool call without asking, so don't use them for sessions that may add packages. In Visual
> Studio 2026, keep the default confirmation for terminal commands. The **NuGet MCP server** is built
> into Visual Studio 2026 (enable "nuget" in the chat Tools menu) and gives the agent real package and
> vulnerability data instead of its memory.
>
> **Claude Code:** Add an `ask` rule in `.claude/settings.json` so installs always prompt, even in
> modes that otherwise run commands freely:
>
> ```json
> { "permissions": { "ask": ["Bash(dotnet add package *)", "Bash(dotnet package add *)",
>                            "PowerShell(dotnet add package *)", "PowerShell(dotnet package add *)"] } }
> ```
>
> You can also connect the NuGet MCP server, which runs from nuget.org with `dnx` (.NET 10 SDK):
> `claude mcp add nuget -- dnx NuGet.Mcp.Server --source https://api.nuget.org/v3/index.json --yes`.
> That command is assembled from the server's documented configuration; check `/mcp` to confirm it
> connected.

Finally, make "new package" a review checkpoint. Before approving any AI diff, look at every file that
can declare a dependency. New files appear in `git diff` only once Git tracks them, so mark them first:

```powershell
git add -N .                                  # include files the agent created
git diff --stat -- '*.csproj' '*Directory.*.props' '*Directory.*.targets' ':(icase)*nuget.config' '*packages.lock.json'
git diff --stat -G '^#:package' -- '*.cs'     # file-based apps declare packages in code
```

---

## Summary

- Models **invent package names and members** by pattern, especially from your own namespace, from
  other ecosystems, and from plausible suffixes, and they explain the invention confidently.
- **Version mismatches** are the subtler failure: code written for a different version of a real
  package (CsvHelper, xUnit) or of the platform (System.Text.Json, the web API templates). The
  **compiler** is the judge; give it the real version.
- **Slopsquatting** turns repeated hallucinations into a supply-chain attack, so `NU1101` means
  **reject**, never "try again later", and package installs by agents should need **approval**.
- Verify every suggested dependency in order: **does it exist** (`dotnet package search
  --exact-match`), **can you trust it** (owner, downloads, repository, releases), **does the API
  compile** (a file-based spike), and **is it allowed** (approved list, license, source mapping).
- Add approved packages with **central package management**, a **lock file**, and **NuGet Audit**
  turned on, and record the decision where reviewers can find it.
- Put the **dependency rules** in your instruction files, and back them with **tool permissions** and
  a **diff checkpoint** on every file that can declare a dependency (`.csproj`, the `Directory.*`
  files, `nuget.config`, lock files, and `#:package` lines).
