# AI-Assisted Development with C#: Course Sample

This repository is a sample of the five-day course **AI-Assisted Development with C#**. It contains
three complete modules, exactly as students receive them: the reading, the slide decks, the
instructor's live demos, the hands-on labs with their reference solutions, and each module's exit
ticket. The [course outline (PDF)](courseware/outline/pdf/AIAssistedDevelopmentWithCSharp_Outline.pdf)
covers the whole week.

The three modules follow one thread from the course: put an agent to work on a real .NET solution,
verify what it produces, and deliver the result through GitHub.

| Module | Day | What developers learn |
|--------|-----|-----------------------|
| [07: Agentic Coding](#module-07-agentic-coding) | 3 | Run a coding agent safely: plan mode, supervision, build, analyzers and tests in the loop, hooks, skills, subagents, and MCP servers built in C# |
| [12: C#-Specific Pitfalls and Verification](#module-12-c-specific-pitfalls-and-verification) | 5 | Catch the mistakes AI makes in .NET code, and turn nullable reference types, analyzers, and `dotnet format` into quality gates |
| [13: AI Across the Delivery Lifecycle with GitHub](#module-13-ai-across-the-delivery-lifecycle-with-github) | 5 | Take work from issue to reviewed pull request with the GitHub MCP server and Copilot code review, and put agents and GitHub Advanced Security into GitHub Actions |

## What's New in This Update

- **Delivery on GitHub, end to end.** Module 13 and every delivery example in the course now teach
  GitHub: Issues and Projects, pull requests, Copilot code review, GitHub Actions, and GitHub Advanced
  Security. Its labs run in a GitHub organization the instructor provides, and each has an offline
  path.
- **Guided help in every lab.** Each lab step states the goal and the acceptance criteria rather than
  a prompt to paste. Under every step there is now a collapsed **Guided help** toggle with the exact
  prompt or commands, the tool mode, and what to check. Students who get stuck can unblock themselves
  without waiting for the instructor. In review steps, the answer itself sits in one more toggle, so
  opening the help doesn't give the finding away.
- **Two more modules.** This sample adds Module 07 (agentic coding) and Module 13 (GitHub delivery)
  to Module 12, which has been updated too.

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
| 3 | Project context files; **agentic coding** with build, analyzers, and tests in the loop (Module 07, in this sample) |
| 4 | AI-driven testing, debugging, refactoring and legacy modernization, comprehension and docs |
| 5 | **C# pitfalls and verification** and **delivery on GitHub** (Modules 12 and 13, in this sample), security and governance, adoption |

## Module 07: Agentic Coding

Module 07 is where the course turns agentic. Developers hand a multi-file feature to a coding agent,
review and edit its plan before any code exists, and choose how closely to supervise it. They keep it
honest with `dotnet build`, analyzers, tests, and `dotnet format` in the loop. Then they package the
team's workflow as files everyone shares: hooks that enforce rules, subagents and skills that package
procedures, and an MCP server written in C# that gives the agent read-only access to the
application's data. The module fills most of Day 3, about four hours: three rounds of slide deck,
live demo, and hands-on lab, then the exit ticket.

| Material | What it is |
|----------|------------|
| **Reading** | Seven reference chapters (below), each tool-neutral first, then Copilot and Claude Code side by side |
| **Slides** | Three decks as PDF, each with its PowerPoint and Markdown source: [The Agentic Loop and Plan Mode](courseware/slides/pdf/12-Agentic-Loop-And-Plan-Mode-Slides.pdf), [The Verify Loop](courseware/slides/pdf/13-The-Verify-Loop-Slides.pdf), [Hooks, Skills, Subagents, and MCP](courseware/slides/pdf/14-Hooks-Skills-Subagents-And-MCP-Slides.pdf) |
| **[Demo 12](courseware/demos/12-agentic-loop-and-plan-mode/) and [Activity 12: Plan, Then Let the Agent Build](courseware/activities/12-agentic-loop-and-plan-mode/)** | The agent plans an orders feature in plan mode; the developer improves the plan, approves it at a chosen supervision level, and judges the result by its tests and diff |
| **[Demo 13](courseware/demos/13-the-verify-loop/) and [Activity 13: Drive the Agent to Green](courseware/activities/13-the-verify-loop/)** | A half-finished migration with failing tests and a lenient build: set up strict gates and at least one guard, then drive the agent to green without it touching the existing tests |
| **[Demo 14](courseware/demos/14-hooks-skills-subagents-and-mcp/) and [Activity 14: Extend an MCP Server and Package Workflows](courseware/activities/14-hooks-skills-subagents-and-mcp/)** | Extend a C# MCP server with stock and pricing tools, register it in both assistants, and package a format hook, a reviewer or test-writer agent, and a skill |
| **[Bonus Demo 04](courseware/demos/bonus/04-issue-to-pr-automation/) and [Bonus Activity 04: Issue to PR with a Cloud Agent](courseware/activities/bonus/04-issue-to-pr-automation/)** | An optional pair: write an agent-ready issue, hand it to the Copilot cloud agent or the Claude Code GitHub Action, and review the pull request it opens |
| **[Exit ticket](courseware/assessments/module-07-exit-ticket.md)** | The five-minute, ungraded check that closes the module, with the instructor's answer key |

The seven chapters:

1. [The Agentic Loop: Plan, Act, Observe, Iterate](courseware/docs/module-07-agentic-coding/01-the-agentic-loop.md)
2. [Your Coding Agent in Depth](courseware/docs/module-07-agentic-coding/02-your-coding-agent-in-depth.md)
3. [Plan Mode and the Supervision Spectrum](courseware/docs/module-07-agentic-coding/03-plan-mode-and-the-supervision-spectrum.md)
4. [The Verify Loop: Build, Analyzers, Tests, and Format in the Loop](courseware/docs/module-07-agentic-coding/04-the-verify-loop.md)
5. [Subagents, Hooks, and Skills: Packaging Repeatable Workflows](courseware/docs/module-07-agentic-coding/05-subagents-hooks-and-skills.md)
6. [MCP Servers: Connecting Agents to Internal Tools and Data](courseware/docs/module-07-agentic-coding/06-mcp-servers.md)
7. [Issue to Pull Request: Cloud Agents on GitHub](courseware/docs/module-07-agentic-coding/07-issue-to-pr-automation.md)

## Module 12: C#-Specific Pitfalls and Verification

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

The five chapters:

1. [Hallucinated NuGet Packages and API Versions](courseware/docs/module-12-csharp-pitfalls-and-verification/01-hallucinated-nuget-packages-and-api-versions.md)
2. [Stale Framework Knowledge: Verifying Against Current .NET Docs](courseware/docs/module-12-csharp-pitfalls-and-verification/02-stale-framework-knowledge.md)
3. [Nullable and Analyzer Warnings as Quality Gates](courseware/docs/module-12-csharp-pitfalls-and-verification/03-nullable-and-analyzer-warnings-as-quality-gates.md)
4. [EditorConfig, Roslyn Analyzers, and `dotnet format` for Generated Code](courseware/docs/module-12-csharp-pitfalls-and-verification/04-editorconfig-analyzers-and-dotnet-format.md)
5. [Performance Review: Allocations, LINQ, and Async Overhead](courseware/docs/module-12-csharp-pitfalls-and-verification/05-performance-review.md)

## Module 13: AI Across the Delivery Lifecycle with GitHub

Module 13 follows Module 12 on Day 5 and takes AI past the editor into the team's delivery process on
GitHub. Developers connect their assistant to GitHub through the GitHub MCP server with least
privilege, refine a rough idea into an issue with testable acceptance criteria, and take it to a pull
request that Copilot code review checks against the team's instruction files. Then they turn a
build-and-test workflow into deterministic gates (warnings as errors, tests, format, CodeQL, and
dependency review) followed by advisory AI jobs, designed for untrusted pull request text, scoped
tokens, and cost limits. The module fills about three hours of Day 5: two rounds of slide deck, live
demo, and hands-on lab, then the exit ticket.

| Material | What it is |
|----------|------------|
| **Reading** | Eight reference chapters (below), each tool-neutral first, then Copilot and Claude Code side by side |
| **Slides** | Two decks as PDF, each with its PowerPoint and Markdown source: [GitHub MCP and Issues](courseware/slides/pdf/22-GitHub-MCP-And-Issues-Slides.pdf), [AI in GitHub Actions and GHAS](courseware/slides/pdf/23-AI-In-GitHub-Actions-And-GHAS-Slides.pdf) |
| **[Demo 22](courseware/demos/22-github-mcp-and-issues/) and [Activity 22: Issue to PR with the GitHub MCP Server](courseware/activities/22-github-mcp-and-issues/)** | Connect the GitHub MCP server, have the agent write and create the issue, implement it on a linked branch, open the pull request, and request Copilot code review |
| **[Demo 23](courseware/demos/23-ai-in-github-actions-and-ghas/) and [Activity 23: Put an Agent in the Workflow](courseware/activities/23-ai-in-github-actions-and-ghas/)** | Build the gates and two advisory AI jobs, a pull request risk review and a failure triage, that run with either Claude Code or the Copilot CLI. The demo uses an AI-written SQL injection to show CodeQL, Copilot Autofix, and the triage at work |
| **[Exit ticket](courseware/assessments/module-13-exit-ticket.md)** | The five-minute, ungraded check that closes the module, with the instructor's answer key |

The eight chapters:

1. [Where AI Fits in the Delivery Lifecycle](courseware/docs/module-13-ai-across-the-delivery-lifecycle-with-github/01-where-ai-fits-in-the-lifecycle.md)
2. [Connecting Agents to GitHub](courseware/docs/module-13-ai-across-the-delivery-lifecycle-with-github/02-connecting-agents-to-github.md)
3. [GitHub Issues and Projects with AI](courseware/docs/module-13-ai-across-the-delivery-lifecycle-with-github/03-github-issues-and-projects-with-ai.md)
4. [Copilot Code Review on GitHub](courseware/docs/module-13-ai-across-the-delivery-lifecycle-with-github/04-copilot-code-review-on-github.md)
5. [Issue to Pull Request](courseware/docs/module-13-ai-across-the-delivery-lifecycle-with-github/05-issue-to-pull-request.md)
6. [Agents in GitHub Actions](courseware/docs/module-13-ai-across-the-delivery-lifecycle-with-github/06-agents-in-github-actions.md)
7. [GitHub Advanced Security as a Quality Gate](courseware/docs/module-13-ai-across-the-delivery-lifecycle-with-github/07-github-advanced-security.md)
8. [Cost Visibility](courseware/docs/module-13-ai-across-the-delivery-lifecycle-with-github/08-cost-visibility.md)

## Also Included

- **[Course outline](courseware/outline/pdf/AIAssistedDevelopmentWithCSharp_Outline.pdf)**: the whole
  five-day course, section by section ([Markdown source](courseware/outline/md/AIAssistedDevelopmentWithCSharp_Outline.md)).
- **[Setup guide](courseware/setup/SETUP-STUDENT-MACHINES.md)**: what students install before Day 1
  (it covers the whole course).

## Try a Lab Yourself

The labs are real .NET 10 solutions. You need Windows with
[PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows),
the [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0), and Git. To work a lab the
way students do, you also need GitHub Copilot or Claude Code; building and testing doesn't.

```powershell
git clone https://github.com/training4programmers/202YMMDD_ai-tools-csharp_sample.git
cd 202YMMDD_ai-tools-csharp_sample
.\courseware\utils\start-lab.ps1 -Activity 13
cd ~\labs\activity-13-the-verify-loop
dotnet build                             # builds, with the lenient settings the lab starts from
dotnet test --solution Bookstore.slnx    # 8 of 26 fail on purpose: getting to green is the lab
```

Then follow the [Activity 13 instructions](courseware/activities/13-the-verify-loop/README.md).
`start-lab.ps1` copies the lab out of this repository into its own Git repository with a baseline
commit, so `git diff` shows exactly what you and the assistant changed. The same command starts any
lab in this sample:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 12             # also 14, 21, 22, and 23
.\courseware\utils\start-lab.ps1 -Activity 13 -Solution   # the reviewed reference solution
.\courseware\utils\start-lab.ps1 -Demo 14                 # an instructor demo's starting point
.\courseware\utils\start-lab.ps1 -Activity 4 -Bonus       # a bonus lab (4 or 9)
```

Labs 22 and 23 work against GitHub. In class, each student has a private repository in an
organization the instructor provides, and both labs also have an offline path. Bonus 04 runs in the
student's own GitHub repository.

## Reading Notes

- **This is three modules of fifteen.** The material mentions other modules, demos, and activities
  by number; those are part of the full course and aren't included here.
- **Two lab apps, on purpose.** Instructor demos use *ToolsApp*, a small color-palette API; student
  labs use *Bookstore*. Students practice each technique on different code than the demo, so they
  learn to transfer it instead of replaying the instructor's prompts.
- **AI output varies run to run.** AI responses quoted in the material are labelled illustrative,
  and each lab solution is *one acceptable result*, not the only answer.

## License

The programming code in this repository is available under the MIT license; see [LICENSE](LICENSE).
The course documents (the reading, exercise instructions, slides, diagrams, exit tickets, and outline)
are not covered by that license. Please don't share or redistribute them.
