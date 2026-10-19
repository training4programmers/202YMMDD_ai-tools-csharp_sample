# Bonus Demo 09: Tools Review

A quick reference to the **AI-tool features this demo introduces**: letting the agent run
BenchmarkDotNet and report real numbers, Visual Studio 2026's Profiler Agent, and setting up a
"suggest, then measure" loop in which the assistant proposes optimizations as separate candidates and
the benchmark accepts or rejects them. The concepts are tool-neutral and come from Module 12 chapter
05; the GitHub Copilot and Claude Code specifics below are a snapshot. Menus and settings move often,
so the vendors' docs are the authority.

---

## Letting the agent run benchmarks

An agent that can run `dotnet run -c Release` can measure its own suggestions. The two things to
insist on are **Release** (BenchmarkDotNet refuses Debug builds) and **the raw summary table**, not a
paraphrase of it.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Where** | Agent mode in Visual Studio 2026 or VS Code, in the integrated terminal | Any session, through its shell tool |
| **Permission** | VS Code asks before running commands that aren't auto-approved; add a `chat.tools.terminal.autoApprove` entry for `dotnet run -c Release` if you'll iterate a lot | Allow `Bash(dotnet run -c Release *)` (or the `PowerShell(...)` form) in `.claude/settings.json` |
| **Watch out** | A full run takes minutes; ask for `--job short` while iterating and a default run for the final numbers | In Git Bash, an unquoted `--filter *` is expanded by the shell; quote it: `--filter '*'` |

```powershell
dotnet run -c Release --project benchmarks/ToolsApp.Benchmarks -- --filter '*' --job short
```

```text
Run the benchmark in Release and paste the summary table exactly as BenchmarkDotNet printed it,
including the Error and Allocated columns. Don't round or summarize the numbers.
```

---

## The Profiler Agent (Visual Studio 2026)

Visual Studio 2026 includes the **GitHub Copilot Profiler Agent**. It analyzes CPU and memory usage,
can write or optimize BenchmarkDotNet benchmarks, and validates fixes. Invoke it with `@profiler` in
Copilot Chat, or enable it in the Tools picker in agent mode; "Profile with Copilot" is also available
from Test Explorer.

> **GitHub Copilot:** In Visual Studio 2026, `@profiler` is a good starting point when you don't yet
> know where the time goes. In VS Code there's no profiler agent; ask agent mode to run the benchmark
> project, or use `dotnet-counters` and `dotnet-trace` from the terminal.
>
> **Claude Code:** There's no built-in profiler. Run the benchmark project, or collect a trace with
> `dotnet-trace` and ask Claude to help read the summary. Either way, the numbers come from a tool, not
> from the model.

---

## Candidates, not edits

When you ask for "more speed," assistants propose changes with the same confidence whether they help or
not: unsafe code, forced inlining, parallel loops, a spatial index. The habit that keeps the codebase
clean is to have the agent implement each idea as a **separate candidate class in the benchmark
project**, benchmark it next to the current version, and change production code only if the numbers
clearly justify it.

```text
Can it go faster still? Propose one more optimization, implement it as a separate candidate class in
the benchmarks project (not in NearestColorFinder), and benchmark it against the optimized version.
Tell me whether it's worth keeping, based on the numbers.
```

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Keep it contained** | Name the folder (`benchmarks/…/Candidates/`) and say "don't change the domain class" | Same; review with `git diff --stat` that only the benchmarks project changed |
| **Decide** | Compare the means against the `Error` column; a difference inside the noise is no difference | Same; ask the agent to state the margin, then check the table yourself |

---

## Proving behavior didn't change

Speed without identical results is a regression. Ask the agent to keep the original implementation as a
reference and to write an **equivalence test** before optimizing, then make the test part of the normal
`dotnet test` run.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Prompt** | "Before you change it, copy the current class to the benchmarks project and add a test that compares both on every named color plus 2,000 random colors" | Same |
| **Watch out** | Agents sometimes make the test project reference the benchmark *executable*; linking the one reference file into the test project is simpler | Same; check that the test compares whole results (name, hex, distance), not just names |

---

## Summary

- Let the agent **run BenchmarkDotNet in Release** and paste the **raw table**; quote `--filter '*'`.
- Visual Studio 2026's **Profiler Agent** (`@profiler`) can analyze CPU and memory and write benchmarks;
  elsewhere, use the benchmark project and the .NET diagnostic tools.
- Have the agent implement each optimization idea as a **candidate in the benchmark project**, and
  change production code only when the numbers **beat the noise**.
- Keep the original as a **reference** and an **equivalence test** in the regular test run, so "faster"
  never means "different".
