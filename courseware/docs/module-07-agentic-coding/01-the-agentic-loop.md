# The Agentic Loop: Plan, Act, Observe, Iterate

Until now, most of your AI help has been a conversation: you ask, the assistant answers, and you do the
work of pasting the code, running the build, and reporting back what broke. An **agent** removes you from
that relay. It reads the files itself, edits them, runs `dotnet build` and `dotnet test`, reads the output,
and tries again until the job is done or it gets stuck. That one change, letting the model act and see the
results of its actions, is what turns a chat assistant into a coding agent.

This chapter explains the loop every coding agent runs, the tools it uses to act, why .NET's fast and
precise feedback makes C# a good fit for agents, and how the loop decides it is finished. The most
important idea comes last: an agent is only as good as its definition of "done", so you will learn to hand
it acceptance criteria it can check for itself.

---

## From an answer to an agent

A few terms first, because the rest of the module depends on them.

- A **tool** is an action the model can ask the host application to perform: read a file, search the code,
  write a file, run a terminal command, or call an external service.
- A **tool call** is one such request. The model doesn't run anything itself; it produces a structured
  request ("run `dotnet test --solution ToolsApp.slnx`"), the host runs it (after asking you, if the
  permission settings say so), and the result goes back into the model's context.
- An **agent** is a model running in a loop with tools: it keeps choosing tool calls, reading their
  results, and choosing again, until it decides the task is complete.
- A **turn** is one pass through that loop. A typical feature takes the agent somewhere between ten and a
  hundred turns.

Compare the two ways of fixing a failing test:

| Step | Chat assistant | Agent |
|------|----------------|-------|
| Find the failing test | You paste the test output | It runs `dotnet test` and reads the output |
| Find the code under test | You paste the method | It searches the solution and opens the file |
| Change the code | You copy the suggestion into your editor | It edits the file |
| Check the fix | You rebuild and rerun | It rebuilds and reruns, and tries again if still red |

The agent does the same things you would, in the same order. What changes is who carries the information
between the steps, and how quickly.

---

## The loop in four steps

Every coding agent, whichever product it ships in, runs some version of the same cycle.

1. **Plan.** Read the request, the instruction files, and the relevant code, and decide on the next step or
   steps.
2. **Act.** Make a tool call: edit a file, create a test, or run a command.
3. **Observe.** Read what came back: compiler errors, analyzer warnings, test results, a stack trace, or a
   file's contents.
4. **Iterate.** Decide whether the goal is met. If not, plan the next step with what it just learned.

![A loop diagram: a goal made of your prompt and acceptance criteria feeds a Plan step, which leads to Act (edit files, run dotnet build and dotnet test), then Observe (compiler errors, analyzer warnings, test results), then a Done decision; if not done the loop returns to Plan, and if all checks are green the diff goes to human review. Side notes show the context the agent reads and the supervision level.](../../diagrams/png/07-agentic-loop.png)

*Figure: The agentic loop. The agent cycles through plan, act, and observe until the build, analyzers,
and tests say it is done, and only then hands a diff to a human.*

Here is a condensed, **illustrative** trace of an agent adding an export endpoint to the ToolsApp demo
solution. Your agent's steps and wording will differ from run to run.

```text
> Read TASK.md, AGENTS.md
> Search: "MapPaletteEndpoints"            -> src/ToolsApp.Api/Endpoints/PaletteEndpoints.cs
> Read PaletteEndpoints.cs, ColorMath.cs, ColorApiTests.cs
  Plan: add PaletteExporter to ToolsApp.Domain, map GET /{id}/export, add unit and API tests.
> Write src/ToolsApp.Domain/PaletteExporter.cs
> Edit  src/ToolsApp.Api/Endpoints/PaletteEndpoints.cs
> Run   dotnet build
  error CS0246: The type or namespace name 'FileContentHttpResult' could not be found
> Edit  PaletteEndpoints.cs  (add using Microsoft.AspNetCore.Http.HttpResults)
> Run   dotnet build                       -> Build succeeded. 0 Warning(s)
> Write tests/ToolsApp.Tests/PaletteExporterTests.cs
> Run   dotnet test --solution ToolsApp.slnx
  failed ToGpl_writes_the_gimp_format_with_lf_line_endings: Expected "...\n" Actual "...\r\n"
> Edit  PaletteExporter.cs  (use "\n" instead of Environment.NewLine)
> Run   dotnet test --solution ToolsApp.slnx -> total: 44, failed: 0
  Done: summary of files changed and tests added.
```

Notice the two failures. Neither was a disaster; each produced a precise message, and the agent fixed the
cause on the next turn. That is the loop working as intended.

---

## The tools an agent has

What an agent can do depends entirely on the tools the host gives it. Most coding agents share a common
core:

| Tool category | What it does | Why it matters |
|---------------|--------------|----------------|
| Read and search | Open files, list folders, search text or symbols | Gathers context before it changes anything |
| Edit and write | Change existing files, create new ones | Produces the actual change |
| Terminal | Run commands such as `dotnet build`, `dotnet test`, `git diff` | Gets feedback from the real toolchain |
| Web fetch | Read documentation pages | Checks an API it isn't sure about |
| MCP tools | Call external servers: issue trackers, databases, internal APIs | Reaches data outside the repo (chapter 06) |
| Subagents | Hand a subtask to a helper with its own context | Keeps the main conversation focused (chapter 05) |

Each tool is also a risk. A read tool can expose a secret, an edit tool can damage code you didn't mean to
touch, and a terminal tool can run anything your user account can run. That is why every tool call goes
through a **permission** layer you control, covered in chapter 03.

> **GitHub Copilot:** In Visual Studio 2026, choose **Agent** in the chat mode dropdown, then select
> **Tools** to see and switch tools on or off, including MCP tools. In VS Code, agent sessions show a
> **Configure Tools** control in the chat input. The Copilot CLI uses the same tool set in the terminal.
>
> **Claude Code:** The built-in tools have plain names you'll see in permission prompts and settings:
> `Read`, `Grep`, `Glob`, `Edit`, `Write`, `Bash`, `PowerShell` (used on Windows when Git Bash isn't
> installed), and `WebFetch`. MCP tools appear as `mcp__<server>__<tool>`.

---

## Why .NET suits agents

An agent improves only as fast as its feedback. C# and the .NET toolchain give it unusually good feedback:
**fast**, **precise**, and **machine-readable**.

### The compiler points at the exact problem

A C# compiler error names the file, the line, the column, a stable error code, and a readable message:

```text
src/ToolsApp.Api/Endpoints/PaletteEndpoints.cs(58,23): error CS0103: The name 'PaletteExporter' does not exist in the current context
src/ToolsApp.Api/Endpoints/ColorSearchEndpoints.cs(22,55): warning CS8602: Dereference of a possibly null reference.
```

An agent can act on that without guessing. It also means a whole class of AI mistakes never survives the
first build: a hallucinated method name, a wrong parameter type, a missing `using`, or a stale API that was
removed. In a dynamic language the same mistake might only surface at runtime, or never.

### Nullable analysis and analyzers catch what compiles but is wrong

With `<Nullable>enable</Nullable>` the compiler tracks which references may be null, so a missing null
check becomes a warning (CS8602) instead of a production `NullReferenceException`. Roslyn analyzers go
further: CA2016 flags a `CancellationToken` that isn't passed on, CA1305 flags culture-sensitive
formatting, and your `.editorconfig` can turn team style rules into warnings. Each finding carries an ID
the agent can look up and fix. Chapter 04 shows how to switch these on as gates.

### Tests report expected versus actual

xUnit v3 failures name the test, the inline data that failed, and the values that differed:

```text
failed ToolsApp.Tests.ColorMathTests.Rate_maps_ratios_to_wcag_levels(ratio: 4.5, expected: AA) (5ms)
  Assert.Equal() Failure: Values differ
  Expected: AA
  Actual:   AALarge
```

That is a near-perfect bug report: the boundary case, the expected value, and the actual one.

### What the toolchain can't tell the agent

Feedback has limits, and the agent doesn't know where they are unless you tell it.

- The compiler can't tell a correct business rule from a wrong one. Only a test can.
- A passing test suite only proves what the tests check. If nothing tests the 409 response, the agent can
  get it wrong and still finish green.
- Runtime behavior (a slow query, a deadlock, a culture-specific bug on a German server) usually needs a
  specific test, a `.http` request, or a log to become visible.

So the practical rule is: **if you want the agent to get something right, make it checkable**, and give it
the command that checks it.

---

## When does the loop stop?

The loop has to end somewhere. In practice it stops for one of these reasons:

- **The model decides the task is complete.** This is the normal case, and the one to be skeptical of:
  "done" means the model believes it is done, not that it is.
- **A limit is reached.** Headless and CI runs usually set a turn limit (`--max-turns` for Claude Code) or a
  continuation limit (`--max-autopilot-continues` for the Copilot CLI's autopilot mode), and some tools
  also support a spending cap.
- **You stop it.** Press `Esc` in Claude Code, `Ctrl+C` in the Copilot CLI, or the stop button in the IDE
  chat. Stopping early is a normal part of supervising an agent, not a failure.
- **It gets blocked.** A permission is denied, a command fails in a way it can't work around, or it needs a
  decision only you can make.

Watch for the three ways the loop goes wrong:

1. **Premature "done".** The agent reports success without running the tests, or after running only some
   of them. The fix is to require the verification commands in the task and check the transcript.
2. **Thrashing.** The same error comes back two or three times while the agent tries small variations.
   Stop it, read the error yourself, and give it the missing fact, or narrow the task.
3. **Scope creep.** It "improves" files the task never mentioned. Constrain the scope in the prompt and
   review the diff file by file.

---

## Acceptance criteria are the "done" signal

The single most effective thing you can do for an agent is to tell it, in checkable terms, what done looks
like. Compare two requests for the same feature:

```text
Add an export endpoint for palettes.
```

```text
Implement TASK.md: GET /api/palettes/{id}/export?format=css|json|gpl.

Acceptance criteria:
- For the seeded Ocean palette, the CSS and GPL bodies match the examples in TASK.md exactly.
- Each format has the content type and download name in the TASK.md table.
- Unknown id returns 404; a missing or unknown format returns 400 naming "format".
- Formatting logic lives in ToolsApp.Domain with unit tests; the endpoint has API tests.

Verify with: dotnet build (no warnings) and dotnet test --solution ToolsApp.slnx.
Don't add packages or change existing tests. Finish with a list of files changed.
```

The second prompt gives the loop a finish line. Every criterion maps to something the agent can run or
read, and the last two lines set the boundaries.

### Put the task in a file

For anything bigger than a small fix, write the task to a file such as `TASK.md` and point the agent at
it. A file survives across sessions, can be reviewed in a pull request, and is easier to read than a long
prompt. A useful structure:

1. **Goal**: what should change and why, in two or three sentences.
2. **Contract**: endpoints, inputs and outputs, error cases, with examples.
3. **Acceptance criteria**: a checklist where every item can be verified.
4. **Constraints**: no new packages, don't touch existing tests, follow `AGENTS.md`.
5. **Verification**: the exact commands that must pass.

### Put the standing rules in the instruction file

Rules that apply to every task belong in `AGENTS.md`, `CLAUDE.md`, or `.github/copilot-instructions.md`
(module 06), so you don't repeat them in every prompt. The demo solutions use a short "Definition of done"
section:

```markdown
## Definition of done

- `dotnet build` reports no warnings and `dotnet test --solution ToolsApp.slnx` passes.
- New behavior has tests: domain logic in a unit test, endpoints through `ToolsAppApiFactory`.
- Don't change or delete existing tests to make them pass. If a test looks wrong, stop and ask.
```

> **GitHub Copilot:** Visual Studio reads `.github/copilot-instructions.md` (and path-specific
> `.github/instructions/*.instructions.md` files) but not `AGENTS.md` or `CLAUDE.md`, so repeat the
> definition of done there. VS Code and the Copilot CLI also read `AGENTS.md`.
>
> **Claude Code:** Reads `CLAUDE.md`. Keep the shared rules in `AGENTS.md` and import them with a first
> line of `@AGENTS.md`, as the lab solutions do.

---

## The loop costs tokens

Every turn sends the conversation so far, the instruction files, and the tool results back to the model.
A long session with large test outputs can use a great deal of **context** (the text the model can see at
once) and a corresponding amount of your plan's usage. Three habits keep that under control:

- **One task per session.** Start a fresh session (or clear the old one) for the next task, so yesterday's
  debugging output isn't re-sent with every turn.
- **Small, verifiable tasks.** "Add the export endpoint" is a good agent task. "Modernize the API" is
  several tasks.
- **Keep output short.** Ask for `dotnet build -v q` and failing tests only, not a full verbose log.

Chapter 02 covers the session commands and usage views in each tool.

---

## Summary

- An **agent** is a model running in a loop with **tools**: it plans, acts, observes the results, and
  iterates until it decides the task is complete.
- The **tools** (read, edit, terminal, web, MCP, subagents) define what an agent can do, and every tool
  call passes through a **permission** layer you control.
- .NET gives agents **fast, precise, machine-readable feedback**: compiler errors with codes and locations,
  nullable warnings, analyzer rule IDs, and test failures with expected and actual values.
- The loop stops when the model **thinks** it is done, a **limit** is reached, you **interrupt** it, or it
  is **blocked**; watch for premature "done", thrashing, and scope creep.
- **Acceptance criteria** that the agent can check, plus the **verification commands**, are the most
  effective way to make "done" mean done.
- Each turn re-sends context, so **one task per session** and **small tasks** keep agents fast and
  affordable.
- Try it in [Demo 12](../../demos/12-agentic-loop-and-plan-mode/) and
  [Activity 12](../../activities/12-agentic-loop-and-plan-mode/).
