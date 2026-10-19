# Stale Framework Knowledge: Verifying Against Current .NET Docs

.NET ships a major version every November, and C# moves with it. A model is trained on a snapshot of
the internet that stops at its **training cutoff**, and even before that date, the newest material is
thin: there are fifteen years of .NET Framework answers online and only months of .NET 10 ones. So an
assistant's sense of "how you write this in C#" lags the platform you actually build on, sometimes by
a release, sometimes by a decade.

This chapter covers the symptoms of stale knowledge in C# work, how to ground a model in the version
you're really using (context files, current docs, and a docs MCP server), and why the compiler, not
the model and not this chapter, is the final authority on what exists.

---

## Training Cutoffs Meet a Yearly Platform

At the time of writing:

- **.NET 10** (LTS) and **C# 14** shipped in November 2025. C# 14 added extension members, the
  `field` keyword, null-conditional assignment, and more.
- **.NET 11** (STS) and **C# 15** are at release candidate (RC2 shipped October 13, 2026), with GA
  expected on November 10, 2026. C# 15 introduces union types and closed hierarchies; examples written
  against the RC may change at GA.
- **.NET 8 and .NET 9** both leave support on November 10, 2026.

A model released a few months after a .NET release has seen relatively little code that uses it. Ask
for "a minimal API endpoint with validation" and you may get a hand-rolled validation filter, because
`builder.Services.AddValidation()` arrived in .NET 10 and the internet hasn't caught up. Ask about C#
15 and you may get confident syntax from a proposal that changed before it shipped.

Two things make this worse in practice:

- **The model doesn't know what it doesn't know.** Its report of its own cutoff is itself a guess
  (Module 01's demo probes this). It won't say "I'm unsure about .NET 10"; it answers in whatever era
  its training data favors.
- **Old code still compiles.** A `Startup` class, `WebClient`, and Newtonsoft.Json all build on .NET
  10. Staleness rarely fails the build; it ships quietly.

---

## Symptoms of Stale Knowledge

### Obsolete APIs

Since .NET 5, many obsoletions carry their own **SYSLIB** diagnostic ID and a link to the
replacement, so the compiler names the problem for you. These are the ones assistants produce most
often:

| ID | What the model writes | What .NET 10 wants |
|----|-----------------------|--------------------|
| `SYSLIB0014` | `WebClient`, `WebRequest`, `HttpWebRequest` | `HttpClient` from `IHttpClientFactory` |
| `SYSLIB0020` | `JsonSerializerOptions.IgnoreNullValues = true` | `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` |
| `SYSLIB0021` | `new SHA256Managed()`, `new AesManaged()` | `SHA256.HashData(bytes)`, `Aes.Create()` |
| `SYSLIB0023` | `new RNGCryptoServiceProvider()` | `RandomNumberGenerator.GetBytes(...)` and friends |
| `SYSLIB0041`, `SYSLIB0060` | `new Rfc2898DeriveBytes(password, salt)` | `Rfc2898DeriveBytes.Pbkdf2(...)` with an explicit hash and iteration count |
| `SYSLIB0011` | `BinaryFormatter` | `System.Text.Json` or another safe serializer; since .NET 9 the in-box `BinaryFormatter` throws |

Here's what one looks like in practice. Demo 21's AI-generated export code computes an ETag the way a
2015 blog post would *(illustrative of the pattern; this one is from the demo's starting point)*:

```csharp
using var sha = new SHA256Managed();
var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(text));
var etag = "\"" + BitConverter.ToString(hash).Replace("-", "") + "\"";
// warning SYSLIB0021: 'SHA256Managed' is obsolete: 'Derived cryptographic types are obsolete.
// Use the Create method on the base type instead.'
```

The current version is one line, and allocates less:

```csharp
var etag = $"\"{Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))}\"";
```

Notice that the default build reports only the `SYSLIB0021` warning. The other two problems,
`ComputeHash` instead of the static `HashData` and `BitConverter.ToString(...).Replace(...)` instead
of `Convert.ToHexString`, show up only with a stronger analyzer level (`CA1850`, `CA1872`; chapter 03).
Staleness is a spectrum, and the default build only sees the far end of it.

SYSLIB warnings can't be silenced with the generic obsolete-member warning `CS0618`; you suppress them
by their own ID. If an assistant adds `#pragma warning disable SYSLIB0021` to "fix" the build, that's a
suppression to reject in review, not a fix.

### Old Hosting and Project Models

These compile, run, and look professional, which is why they survive review:

```csharp
// Illustrative: the shape models reach for when the prompt doesn't name a version
public class Startup
{
    public void ConfigureServices(IServiceCollection services) { /* ... */ }
    public void Configure(IApplicationBuilder app, IWebHostEnvironment env) { /* ... */ }
}

public static IHostBuilder CreateHostBuilder(string[] args) =>
    Host.CreateDefaultBuilder(args).ConfigureWebHostDefaults(web => web.UseStartup<Startup>());
```

Other common examples:

- Controllers and `[ApiController]` for a three-endpoint service the team builds with minimal APIs.
- `services.AddSwaggerGen()` and Swashbuckle in a .NET 10 API that uses built-in `AddOpenApi()`.
- `Newtonsoft.Json` added by default, with `[JsonProperty]` attributes, in a `System.Text.Json` app.
- `WebApplicationFactory<Startup>` in tests, or `public partial class Program { }` added by hand
  (in .NET 10 a source generator emits it; both forms compile).
- `.sln` files and `dotnet sln add` advice in a repo that uses `.slnx`.
- xUnit v2 idioms in an xUnit v3 project: `using Xunit.Abstractions;` (`CS0234`), `IAsyncLifetime`
  methods returning `Task` instead of `ValueTask`, `coverlet.collector` for coverage under
  Microsoft.Testing.Platform.

### Missing New APIs

The quieter symptom: code that works but ignores what the platform now does for you. When you review
AI code for a .NET 10 / C# 14 codebase, these are the gaps to look for:

| Instead of… | .NET 10 / C# 14 offers |
|-------------|------------------------|
| A hand-rolled validation filter | `builder.Services.AddValidation()` for DataAnnotations on minimal APIs |
| `Results.Ok(x)` returning `IResult` | `TypedResults.Ok(x)` with `Results<Ok<T>, NotFound>` |
| A private backing field for one setter rule | The `field` keyword: `set => field = value.Trim();` |
| Static helper classes for "extension properties" | C# 14 extension members: `extension(Color c) { public bool IsDark => ... }` |
| `if (x == null) throw new ArgumentNullException(nameof(x));` | `ArgumentNullException.ThrowIfNull(x);` |
| `new List<string> { "a", "b" }` | Collection expressions: `["a", "b"]` |
| A tiny console project for a script | A file-based app: `dotnet run tool.cs` with `#:package` |
| `DateTime.Now` in testable code | `TimeProvider` (injectable, with `FakeTimeProvider` in tests) |
| `lock (_sync)` on an `object` | `System.Threading.Lock` (C# 13) |

None of these is a bug on its own. Together they're the difference between code that fits your
codebase and code a reviewer has to rewrite.

---

## Grounding the Model in Your Versions

You can't update a model's training data, but you can control what it sees in its context window
(Module 03). Four techniques, from cheapest to most thorough.

### 1. Pin the versions in your context files

State the versions where every session will read them, so the assistant doesn't have to guess:

```markdown
## Platform (read before writing code)

- Target framework: net10.0. C# 14 (the default for net10.0; don't set LangVersion).
- ASP.NET Core 10 minimal APIs with route groups, TypedResults, and AddValidation().
- EF Core 10 with SQLite. System.Text.Json only; never add Newtonsoft.Json.
- Tests: xUnit v3 on Microsoft.Testing.Platform. Run them with `dotnet test --solution ToolsApp.slnx`.
- Don't use preview language features. If you're unsure whether an API exists in .NET 10, say so and
  check the docs (Microsoft Learn MCP server) before using it.
```

And make the build agree with the file. The target framework in `Directory.Build.props` decides the
default language version, so `net10.0` means C# 14 without any `LangVersion` property. Setting
`<LangVersion>latest</LangVersion>` or `preview` invites code that your teammates' SDKs, or your CI
image, might not compile.

### 2. Give the model current docs

When the task depends on something new, put the documentation in the context instead of hoping the
model remembers it:

- **Paste the relevant section** of the Microsoft Learn page ("What's new in C# 14", the API
  reference) into the prompt. Cheap and precise.
- **Let the agent fetch the page.** Agentic tools can read a URL you give them.
- **Connect a documentation MCP server** so the agent can search and fetch docs on its own when it's
  unsure.

The **Microsoft Learn MCP Server** is Microsoft's remote MCP server for its official documentation.
It's publicly available, free, needs no authentication, and exposes tools to search the docs, fetch a
full article, and search code samples (`microsoft_docs_search`, `microsoft_docs_fetch`, and
`microsoft_code_sample_search` at the time of writing; the tool list can change). Its endpoint is
`https://learn.microsoft.com/api/mcp`, using streamable HTTP.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Fetch one page** | Agent mode's web fetch tool (`#web/fetch` in VS Code), or paste the URL and ask it to read it | The built-in web fetch tool: give it the URL |
| **Add the Learn MCP server** | VS Code: `.vscode/mcp.json`; Visual Studio 2026: `<SolutionDir>\.mcp.json` (both use the `"servers"` key) | `claude mcp add --transport http microsoft-learn https://learn.microsoft.com/api/mcp` (add `--scope project` to share it via `.mcp.json`) |
| **Watch out** | Check the chat Tools picker: the server's tools must be listed and enabled for agent mode to call them | Approve the server on first use; `/mcp` shows whether it connected |

```jsonc
// .vscode/mcp.json (VS Code) or .mcp.json at the solution root (Visual Studio 2026)
{
  "servers": {
    "microsoft-learn": {
      "type": "http",
      "url": "https://learn.microsoft.com/api/mcp"
    }
  }
}
```

Then ask for grounding explicitly; models don't always reach for a tool on their own:

```text
Before you write this, use the Microsoft Learn MCP server to check how ASP.NET Core 10 minimal APIs
validate request bodies, and link the page you used.
```

### 3. Ask for sources, and read them

"Link the page you used" is a cheap habit with a big payoff. A link you can open is verifiable; a
paraphrase of a page the model may never have seen isn't. If the answer comes back without a source,
or with a URL that 404s, treat the claim as unverified.

### 4. Ask the compiler

For anything version-specific, a two-minute experiment beats any amount of debate with the model. A
.NET 10 file-based app needs no project:

```csharp
// field.cs: does the C# 14 field keyword work the way the assistant claims?
var color = new Swatch { Hex = "#00a6a6" };
Console.WriteLine(color.Hex);   // #00A6A6

class Swatch
{
    public string Hex { get; set => field = value.ToUpperInvariant(); } = "";
}
```

```powershell
dotnet run field.cs
```

Run it outside any folder whose `Directory.Packages.props` turns on central package management, or
the `#:package` directives in more complex experiments fail with `NU1008`. To try C# 15 features, use
the .NET 11 RC SDK in a separate folder with its own `global.json`, and remember that preview syntax
isn't something to ship.

---

## The Compiler Is the Source of Truth

Every technique above reduces staleness; none eliminates it. The model can read a current page and
still blend it with an older pattern. What doesn't drift is the toolchain you build with:

- **`dotnet build`** knows exactly which APIs exist in your target framework and package versions.
  Invented members are `CS1061`; removed namespaces are `CS0234`; obsolete APIs are `SYSLIB` warnings.
- **`TreatWarningsAsErrors`** turns "obsolete" from a note into a stop sign, so an agent can't finish
  with a `SYSLIB` warning in the output (chapter 03).
- **Analyzers at a stronger `AnalysisLevel`** catch the near-misses the default build allows, such as
  `CA1850` and `CA1872` in the ETag example above (chapters 03 and 04).
- **Tests** catch the behavior changes no compiler sees.

That's also why agents (Module 07) are better at avoiding stale APIs than chat answers: an agent that
runs `dotnet build` sees `SYSLIB0014` and fixes it in the next turn. Chat output you paste never got
that feedback.

> **GitHub Copilot:** In agent mode, ask Copilot to build after each change and to treat `SYSLIB`
> warnings as failures. In VS Code, the `#read/problems` tool gives the agent the Problems panel
> directly. In Visual Studio 2026, the Error List is what to watch; the chat experience is changing
> across 18.x updates, so menus may differ slightly.
>
> **Claude Code:** Put "run `dotnet build` after every change; treat SYSLIB and CS warnings as errors"
> in `CLAUDE.md`, or turn on `TreatWarningsAsErrors` so the build itself refuses. A `PostToolUse` hook
> can run the build automatically after edits (Module 07).

---

## Summary

- Models trail the platform: **C# 14 / .NET 10** code is underrepresented in training data, and **C#
  15 / .NET 11** (RC at the time of writing, GA expected November 2026) is barely there, so assistants
  default to older idioms.
- Stale knowledge shows up as **obsolete APIs** (`SYSLIB0014`, `SYSLIB0020`, `SYSLIB0021`, …), **old
  hosting and project models** (`Startup`, Swashbuckle, Newtonsoft, xUnit v2), and **missing new
  APIs** (`AddValidation()`, `TypedResults`, `field`, extension members, `TimeProvider`).
- **Pin your versions** in context files and in `Directory.Build.props`; don't set `LangVersion` to
  `latest` or `preview` to make AI code compile.
- **Ground the model in current docs**: paste sections, let the agent fetch pages, or connect the
  **Microsoft Learn MCP Server** (`https://learn.microsoft.com/api/mcp`), and ask for **links** you
  can check.
- Settle version questions with a **file-based app** and `dotnet run`, and let **the compiler, the
  analyzers, and the tests** be the source of truth, not the model and not your memory.
