# Demo 12: The Agentic Loop and Plan Mode (ToolsApp / Colors)

Instructor demo paired with [Activity 12](../../activities/12-agentic-loop-and-plan-mode/). The agent plans
a multi-file feature in plan mode, the instructor edits the plan before approving it, and then the agent
builds the feature through its own build-and-test loop.

## Concept

- An **agent** runs a loop: plan, act (edit files, run commands), observe the output, and iterate until
  its definition of done is met.
- **Plan mode** makes the agent research and propose without editing. Reviewing and editing the plan is
  the cheapest place to catch design mistakes.
- A `TASK.md` with **acceptance criteria** and **verification commands** gives the loop a finish line it
  can check.
- After approval you choose the **supervision level**: here, edits are accepted automatically and terminal
  commands still ask.
- .NET's compiler and test output are precise enough for the agent to fix its own mistakes; the human
  reviews the final `git diff`.

![The agentic loop: a goal with acceptance criteria feeds Plan, then Act, Observe, and a Done decision; failures loop back to Plan, and all-green work goes to human review.](../../diagrams/png/07-agentic-loop.png)

*Figure: The agentic loop the agent runs in this demo.*

## How to run

From the course repo root, copy the starting point to a lab folder outside the repo (so the repo's own
AGENTS.md/CLAUDE.md don't leak into the session) and open it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 12 -Open
cd ~\labs\demo-12-agentic-loop-and-plan-mode
dotnet build
dotnet test --solution ToolsApp.slnx
```

Use Claude Code (CLI or VS Code extension) or GitHub Copilot in VS Code (agent session) or Visual Studio
2026 (Plan agent, then Agent mode). A frontier/reasoning-tier model gives the most useful plan. Start a
fresh session in the lab folder. The run takes 10 to 20 minutes, mostly the agent's build-and-test loop.

If the live run goes sideways, `start-lab.ps1 -Demo 12 -Solution` copies the recorded outcome, including
`PLAN.md`, the plan as approved.

## Demo trail

> **Short on time (about 20 minutes).** Run Steps 1 and 2 live: the plan review and the plan edits are
> the lesson. In Step 3, approve and narrate the first few iterations; if the loop is still running
> after about eight minutes, switch to the `-Solution` copy (`PLAN.md`, `PaletteExporter.cs`,
> `PaletteEndpoints.cs`) for Step 4's review, and let the live run finish in the background.

**Code to open (in order):**

1. `TASK.md`: the contract for `GET /api/palettes/{id}/export?format=css|json|gpl`, the three formats, the
   errors, and the acceptance criteria. Point out that every criterion is checkable.
2. `AGENTS.md`: the "Definition of done" section. Then `CLAUDE.md`, which imports `@AGENTS.md` and adds
   only the Claude Code working rules (plan first, run the gates yourself), and
   `.github/copilot-instructions.md`, whose last line repeats what done means. Visual Studio reads only
   the `.github` file, which is why the essentials are repeated there.
3. `src/ToolsApp.Api/Endpoints/PaletteEndpoints.cs`: the route group and `TypedResults` pattern the agent
   should follow.
4. `src/ToolsApp.Domain/ColorMath.cs` (`ParseHex`, `ToHex`) and `ColorModels.cs` (the `Rgb` record
   struct): domain logic lives here.
5. `tests/ToolsApp.Tests/ToolsAppApiFactory.cs`: how API tests get a throwaway database.

**Step 1: Plan, don't edit.** Switch to plan mode: in Claude Code press `Shift+Tab` until the status bar
shows `plan mode on` (or start the prompt with `/plan`); in VS Code or Visual Studio pick the **Plan**
agent; in the Copilot CLI press `Shift+Tab` to plan mode. Prompt (plan mode):

```text
Read TASK.md and AGENTS.md, then plan the palette export feature. Don't change any files yet.
In the plan, list: the files you'll create or change, where the formatting logic will live, how you'll
parse and validate the format parameter, the exact content types and download names, and the unit and
API tests you'll add. List anything in TASK.md you find ambiguous as questions.
```

While it works, point out that it is reading files and searching, not editing. When the plan appears, read
it aloud against the review questions in chapter 03. Plans vary from run to run; the usual catches are:

- parsing `format` with `Enum.TryParse`, which also accepts `"1"` and `"99"`;
- building GPL lines with `Environment.NewLine` (CRLF on Windows), though the format requires LF;
- proposing a package (often Swashbuckle or Scalar) that `TASK.md` rules out;
- API tests that hard-code palette id 2.

**Step 2: Edit the plan, then approve it.** In Claude Code press `Ctrl+G` to edit the plan in your editor,
or choose **No, keep planning** and type the changes. In Copilot, reply in the chat. Prompt (plan mode):

```text
Changes to the plan: parse format with an explicit case-insensitive lookup of css, json, and gpl, not
Enum.TryParse (it accepts "1"). Build GPL text with "\n", never Environment.NewLine. Don't add any
packages. In the API tests, look up the Ocean palette's id through GET /api/palettes instead of
assuming it's 2. Show me the revised plan.
```

When the revised plan is right, approve it with edits auto-accepted and commands still asking: in Claude
Code choose **Yes, auto-accept edits** if offered; if the first option reads **Yes, and use auto mode**,
choose **Yes, manually approve edits** instead and press `Shift+Tab` once so the status bar shows
`accept edits on`. In VS Code, switch to the **Agent** with **Manual permissions** and say:

```text
Implement the approved plan. After each change, run dotnet build and dotnet test --solution ToolsApp.slnx
and fix what fails. Finish with the list of files you changed and the final test summary line.
```

**Step 3: Watch the loop.** Edits land without prompts; each `dotnet build` and `dotnet test` asks first.
Approve them (Claude Code offers to stop asking for the same command in this project). Narrate the cycle:
the build error it reads and fixes, the test it adds, a failing assertion and the fix that follows. If it
starts editing files outside the plan, press `Esc` (Claude Code) or stop the session, and redirect it.

**Step 4: Review the diff, not the summary.** When it reports done:

```powershell
git status
git diff --stat
git diff --stat -- tests/        # only new test files; existing tests unchanged
dotnet build
dotnet test --solution ToolsApp.slnx
```

Open `src/ToolsApp.Domain/PaletteExporter.cs` and `PaletteEndpoints.cs` and check them against `TASK.md`:
the lookup table, the LF line endings, `TypedResults.File` for the download name. Then run the API
(`dotnet run --project src/ToolsApp.Api`) and send the export requests from `ToolsApp.Api.http`. If
anything is wrong, show `/rewind` (Claude Code) or **Restore Checkpoint** (VS Code) before reaching for
`git restore`.

**Key teaching beat:** The plan is where you have the most leverage at the lowest cost. Every edit you made
to it (the `Enum.TryParse` trap, the line endings, the extra package) was a bug that never got written.
After that, the agent's loop and the acceptance criteria did the routine work, and the human's job shrank to
the two things only a human can do here: judging the design and reviewing the diff.

## Reference

- Concepts: [Module 07: Agentic Coding](../../docs/module-07-agentic-coding/):
  [The Agentic Loop](../../docs/module-07-agentic-coding/01-the-agentic-loop.md),
  [Your Coding Agent in Depth](../../docs/module-07-agentic-coding/02-your-coding-agent-in-depth.md),
  [Plan Mode and the Supervision Spectrum](../../docs/module-07-agentic-coding/03-plan-mode-and-the-supervision-spectrum.md)
- Tools: [Tools Review](TOOLS-REVIEW.md)
- Practice: [Activity 12](../../activities/12-agentic-loop-and-plan-mode/)
