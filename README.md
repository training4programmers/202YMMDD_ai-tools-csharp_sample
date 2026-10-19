# AI-Assisted Development with C#: Course Sample

This repository is a sample of the five-day course **AI-Assisted Development with C#**. It contains
one complete module, exactly as students receive it: the reading, the slide deck, the instructor's
live demo, the hands-on labs with their reference solutions, and the module's exit ticket. The
[course outline (PDF)](courseware/outline/pdf/AIAssistedDevelopmentWithCSharp_Outline.pdf) covers the
whole week.

## The Course at a Glance

- **35 hours of live, instructor-led training** over five days, for professional C# and .NET
  developers who are new to AI coding tools.
- **Tool-agnostic, with two concrete tools.** Every technique is taught tool-neutrally first, then
  step by step in both **GitHub Copilot** and **Claude Code**, in Visual Studio 2026, VS Code, and
  the terminal.
- **Current .NET:** .NET 10 LTS and C# 14, ASP.NET Core minimal APIs, EF Core 10, and xUnit v3.
- **Built around practice.** Each of the 15 modules pairs its reading with an instructor demo and a
  hands-on lab. The full course has 90 reference chapters, 25 slide decks, 25 labs plus 10 bonus
  labs, and 15 short exit tickets. Every lab ships a reviewed reference solution with notes on what
  review caught and rejected.

| Day | Focus |
|-----|-------|
| 1 | How AI writes code, setting up your tools, prompting and context engineering for C# |
| 2 | Inline completions, next edit suggestions, and chat; IDE assistants in daily C# work |
| 3 | Project context files; agentic coding with build, analyzers, and tests in the loop |
| 4 | AI-driven testing, debugging, refactoring and legacy modernization, comprehension and docs |
| 5 | **C# pitfalls and verification** (this sample), Azure DevOps, security and governance, adoption |

## What's in This Sample: Module 12, C#-Specific Pitfalls and Verification

Module 12 opens Day 5. It teaches developers to catch the mistakes AI tools make specifically in
.NET code (invented NuGet packages, real packages used with the wrong version's API, stale framework
idioms, culture bugs) and to turn nullable reference types, Roslyn analyzers, and `dotnet format`
into quality gates that catch them automatically. In class, the module fills the first block of
Day 5: after a short recap of Day 4 come the slide deck (about 10 minutes), the instructor demo (about
20), the hands-on lab (about 25, with the rest as take-home), and the exit ticket (about 5).

| Material | What it is |
|----------|------------|
| **Reading** | Five reference chapters (below), each tool-neutral first, then Copilot and Claude Code side by side |
| **[Slides](courseware/slides/pdf/21-CSharp-Pitfalls-And-Verification-Slides.pdf)** | The lecture deck as PDF, with the [PowerPoint](courseware/slides/pptx/21-CSharp-Pitfalls-And-Verification-Slides.pptx) and the [Markdown source](courseware/slides/md/21-CSharp-Pitfalls-And-Verification-Slides.md) |
| **[Demo 21: C# Pitfalls and Verification](courseware/demos/21-csharp-pitfalls-and-verification/)** | The instructor's walkthrough with the exact prompts, the starting code (`begin/`), the recorded outcome (`solution/`), and a [tools review](courseware/demos/21-csharp-pitfalls-and-verification/TOOLS-REVIEW.md) of the Copilot and Claude Code features it uses |
| **[Activity 21: Verify, Then Gate](courseware/activities/21-csharp-pitfalls-and-verification/)** | The hands-on lab: verify an assistant's package suggestions with evidence, rebuild an import without them, and turn on the quality gates. Includes the starting code, one reviewed solution, and its [solution notes](courseware/activities/21-csharp-pitfalls-and-verification/solution/SOLUTION-NOTES.md) |
| **[Bonus Demo 09: Performance Review](courseware/demos/bonus/09-performance-review/)** and **[Bonus Activity 09: Measure Before You Optimize](courseware/activities/bonus/09-performance-review/)** | An optional pair for fast finishers and take-home practice: AI-assisted performance review, measured with BenchmarkDotNet |
| **[Exit ticket](courseware/assessments/module-12-exit-ticket.md)** | The five-minute, ungraded check that closes the module, with the instructor's answer key |
| **[Setup guide](courseware/setup/SETUP-STUDENT-MACHINES.md)** | What students install before Day 1 (it covers the whole course) |

The five chapters:

1. [Hallucinated NuGet Packages and API Versions](courseware/docs/module-12-csharp-pitfalls-and-verification/01-hallucinated-nuget-packages-and-api-versions.md)
2. [Stale Framework Knowledge: Verifying Against Current .NET Docs](courseware/docs/module-12-csharp-pitfalls-and-verification/02-stale-framework-knowledge.md)
3. [Nullable and Analyzer Warnings as Quality Gates](courseware/docs/module-12-csharp-pitfalls-and-verification/03-nullable-and-analyzer-warnings-as-quality-gates.md)
4. [EditorConfig, Roslyn Analyzers, and `dotnet format` for Generated Code](courseware/docs/module-12-csharp-pitfalls-and-verification/04-editorconfig-analyzers-and-dotnet-format.md)
5. [Performance Review: Allocations, LINQ, and Async Overhead](courseware/docs/module-12-csharp-pitfalls-and-verification/05-performance-review.md)

## Try a Lab Yourself

The labs are real .NET 10 solutions. You need Windows with
[PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows),
the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), and Git. To work the lab the
way students do, you also need GitHub Copilot or Claude Code; building and testing doesn't.

```powershell
git clone https://github.com/training4programmers/202YMMDD_ai-tools-csharp_sample.git
cd 202YMMDD_ai-tools-csharp_sample
.\courseware\utils\start-lab.ps1 -Activity 21
cd ~\labs\activity-21-csharp-pitfalls-and-verification
dotnet build                             # green, with the single warning the lab starts from
dotnet test --solution Bookstore.slnx
```

Then follow the [Activity 21 instructions](courseware/activities/21-csharp-pitfalls-and-verification/README.md).
`start-lab.ps1` copies the lab out of this repository into its own Git repository with a baseline
commit, so `git diff` shows exactly what you and the assistant changed. Other options:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 21 -Solution   # the reviewed reference solution
.\courseware\utils\start-lab.ps1 -Demo 21                 # the instructor demo's starting point
.\courseware\utils\start-lab.ps1 -Activity 9 -Bonus       # the bonus performance lab
```

## Reading Notes

- **This is one module of fifteen.** The material mentions other modules, demos, and activities by
  number; those are part of the full course and aren't included here.
- **Two lab apps, on purpose.** Instructor demos use *ToolsApp*, a small color-palette API; student
  labs use *Bookstore*. Students practice each technique on different code than the demo, so they
  learn to transfer it instead of replaying the instructor's prompts.
- **AI output varies run to run.** AI responses quoted in the material are labelled illustrative,
  and each lab solution is *one acceptable result*, not the only answer.

## License

The programming code in this repository is available under the MIT license; see [LICENSE](LICENSE).
The course documents (the reading, exercise instructions, slides, diagrams, exit ticket, and outline)
are not covered by that license. Please don't share or redistribute them.
