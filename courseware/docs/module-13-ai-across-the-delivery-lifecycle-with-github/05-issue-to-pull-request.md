# Issue to Pull Request

The most-requested AI workflow on any team is some version of "give the issue to an agent and get a pull
request back". On GitHub it exists three ways: a **local agent** in your IDE or terminal with the GitHub
MCP server, the **Copilot cloud agent** assigned to the issue, and the **Claude Code GitHub Action**
answering an `@claude` mention. [Module 07](../module-07-agentic-coding/07-issue-to-pr-automation.md)
covers how the two cloud options work: setup, `copilot-setup-steps.yml`, the action's workflow file, and
their built-in protections. This chapter doesn't repeat that.

Instead, it looks at the same three paths from the **lifecycle**: how each one keeps the issue, branch,
pull request, and Project board linked; where review and the required checks come in; how to choose
between delegating and working locally; and how to write issues an agent can finish.

---

## Three Paths from an Issue to a Pull Request

| | Local agent + GitHub MCP server | Copilot cloud agent | Claude Code GitHub Action |
|---|---|---|---|
| **Started by** | You, in a session | Assigning the issue to **Copilot** | An `@claude` mention on the issue |
| **Runs on** | Your machine | A GitHub Actions runner | A GitHub Actions runner, in your workflow |
| **Who watches** | You, live | Nobody until review | Nobody until review |
| **Branch** | One you create from the issue | A `copilot/` branch | A branch the action pushes |
| **How you correct it** | Interrupt and redirect | Comment `@copilot` on the pull request | Comment `@claude` on the pull request |
| **Local tools** | Everything you have: Visual Studio, Windows, the debugger | What `copilot-setup-steps.yml` installs | What your workflow installs |
| **Cost** | AI usage | AI credits **plus** Actions minutes | API usage **plus** Actions minutes |
| **Main risk** | Your time | A large draft to review | A workflow with write permissions |

All three end the same way: a pull request that a person reviews, that the required checks judge, and
that a person approves. The differences are in who's watching while the work happens and how the links
get made.

---

## Path 1: A Local Agent with the GitHub MCP Server

This is the path Demo 22 and Activity 22 take. The agent runs in VS Code, Visual Studio 2026, or a
terminal beside them, so you supervise every step, and the MCP server gives it the issue and the pull
request tools ([chapter 02](02-connecting-agents-to-github.md)).

1. **Read the issue.** "Read issue 42 in `<owner>/<repo>` and summarize its acceptance criteria."
2. **Branch from the issue**, so GitHub links them before any code exists:
   `gh issue develop 42 --name 42-palette-rename --checkout`.
3. **Plan.** Use plan mode (Module 07) and review the plan against the criteria.
4. **Implement and verify.** The agent edits code and runs `dotnet build` and `dotnet test` until both
   pass; every criterion gets a test. Commit messages end with `(#42)`.
5. **Push and open the pull request** through the MCP server, with `Fixes #42` in the description and a
   section on how AI was used.
6. **Review**: request Copilot code review ([chapter 04](04-copilot-code-review-on-github.md)) or run the
   fallback review in a fresh session, then a person approves.

```text
Read issue #42 in <owner>/<repo>. Plan the implementation against its acceptance criteria and wait
for my approval. After I approve: implement the plan on this branch, run dotnet build and
dotnet test --solution ToolsApp.slnx until both pass, and commit with (#42) at the end of the
message. Then push, open a pull request to main through the GitHub MCP server with "Fixes #42" in the
description, and show me the title and body before you create it.
```

**What links itself:** the branch (created from the issue), each commit (through `#42`), and the pull
request (through the branch and `Fixes #42`). Merging closes the issue and, with the Project's default
workflows, moves it to Done.

**Trade-offs:** you're present for the whole run, which costs your time but keeps every write
supervised. It works with both GitHub Copilot and Claude Code, and it's the only path that can use
Windows-only or IDE-only tools, such as a legacy .NET Framework build or the Visual Studio debugger.

---

## Path 2: The Copilot Cloud Agent

Assign the issue to **Copilot** (from **Assignees** on the issue, from the agents panel, or from an agent
session through the MCP server's `assign_copilot_to_issue` tool) and it works in the background: a
`copilot/` branch, a draft pull request, and a review request to you when it's done. Module 07 has the
mechanics; from the lifecycle, four things matter.

- **The issue is the whole brief.** Nobody can answer the agent's questions mid-run, so the
  refinement work in [chapter 03](03-github-issues-and-projects-with-ai.md) pays off most here. Standing
  rules come from the repository's instruction files and `AGENTS.md`; the toolchain comes from
  `copilot-setup-steps.yml`.
- **Check the link before you merge.** The pull request should link the issue, through a closing keyword
  in its description or the **Development** panel. If it doesn't, add `Fixes #42` to the description
  yourself, or the issue stays open and the board stays wrong.
- **The checks wait for a person.** By default, your workflows don't run on the agent's pull request until
  someone with write access approves them. Until then the required checks are pending and the pull
  request can't merge, which is the point: a person looks before any workflow runs on code nobody has
  read.
- **The requester can't approve it.** The developer who asked Copilot for the pull request can't be its
  approving reviewer, so a second person always signs off.

When the draft is wrong, decide which kind of wrong it is. If the agent misread a clear issue, comment
`@copilot` on the pull request with a specific, checkable request and let it iterate. If the issue itself
was ambiguous, close the pull request, fix the acceptance criteria, and assign it again; iterating on a
draft built from a bad brief costs more than restarting from a good one.

---

## Path 3: The Claude Code GitHub Action

With `anthropics/claude-code-action@v1` in a workflow (Module 07 shows the file), an `@claude` mention on
an issue starts Claude Code on a runner. It reads the issue, works on a branch, and replies on the issue
as it goes; follow-up `@claude` comments on the pull request ask for changes.

From the lifecycle:

- **Only people with write access can start it**, and bot accounts are rejected unless you list them,
  which keeps agents from triggering each other.
- **Make sure the result links the issue** the same way as Path 2: a pull request with `Fixes #42`.
  Put that rule in `CLAUDE.md` so every run follows it.
- **Let your checks run on its commits.** GitHub doesn't start workflows for commits pushed with the
  default `GITHUB_TOKEN`. The action's default authentication (the Claude GitHub App) doesn't have that
  problem, so don't pass `github_token: ${{ secrets.GITHUB_TOKEN }}` to it, or your required checks
  never run.
- **The workflow holds write permissions** (`contents`, `pull-requests`, `issues`), and the issue text is
  untrusted input that anyone who can comment can influence. Keep `--max-turns`, `timeout-minutes`, and
  `concurrency` in the workflow, and route the workflow file to a maintainer with `CODEOWNERS`.

---

## Delegate, or Work Locally?

None of the paths is always right. These questions decide it for a given issue:

| Question | Favors delegating (Path 2 or 3) | Favors working locally (Path 1) |
|----------|----------------------------------|----------------------------------|
| Is the issue fully specified? | Yes: contract, criteria, constraints, pointers | No, or you expect to make decisions as you go |
| How big is it? | Small: one pull request, a few files | Cross-cutting, or a refactor you need to steer |
| What does it need to build and test? | The .NET SDK on Linux, installed by setup steps | Windows-only builds, Visual Studio, local services, a debugger |
| Does it touch sensitive code or data? | No | Yes: authentication, payments, migrations, anything with secrets |
| Who reviews, and when? | A reviewer with time today for a draft they didn't watch being written | You, as you go |
| What does it cost? | Actions minutes as well as AI usage | AI usage only |

A sensible pattern for a team new to this: work locally (Path 1) until your issues are consistently
agent-ready, delegate small, well-specified issues next, and keep the sensitive work local for good.

---

## Writing Issues an Agent Can Finish

Whichever path you choose, the issue is the agent's brief. Issues that agents finish well share a shape
(Module 07 calls it an **agent-ready issue**):

- **Small.** One pull request's worth of change, reviewable in under half an hour.
- **Concrete acceptance criteria** ([chapter 03](03-github-issues-and-projects-with-ai.md)), one test each.
- **Pointers into the code.** "Follow the patterns in `Endpoints/PaletteEndpoints.cs`" saves the agent a
  search and you a review comment.
- **Constraints.** "No new packages", "don't change the public DTOs", "EF Core migrations aren't used in
  this repo".
- **Out of scope**, stated.

Compare two versions of the same request:

```text
Weak:   Add palette renaming. Make sure duplicates aren't allowed.

Strong: PATCH /api/palettes/{id} with { "name": "..." } renames a palette. Acceptance criteria 1-6 in
        the description (Given/When/Then). Follow the route group and TypedResults patterns in
        src/ToolsApp.Api/Endpoints/PaletteEndpoints.cs. Names are unique ignoring case; enforce it in
        the database as well as the handler. No new packages. Out of scope: renaming colors, UI.
```

The weak version will produce working code. It will also produce a dozen decisions you didn't make.

---

## Keeping the Project Board Honest

The Project board is where the rest of the team sees the work, so it should show what's really
happening, whichever path you took.

| Moment | What should happen | Who does it |
|--------|--------------------|-------------|
| Work starts (branch created) | Status → **In progress**; assignee set (a person, or Copilot) | Nothing does it automatically yet: the person who starts it, or an agent through the `projects` toolset |
| Pull request opened | The pull request links the issue (`Fixes #42`); status → **In progress** if it isn't already | The agent links it, checked by the reviewer; the Project's **Pull request linked to issue** workflow sets the status |
| Review | The item stays In progress until a person approves | Nobody needs to move it |
| Merge | Issue closes; status → **Done** | GitHub, through the closing keyword and the Project's default workflows |
| Draft abandoned | Close the pull request; move the item back to **Todo** with a comment saying why | The person who closed it |

The gap is the first row: between creating the branch and opening the pull request, the board still says
Todo, so on longer work either move the item yourself or open a draft pull request early. Two rules keep
it trustworthy. **Only a merge marks work Done**, never an agent's own report that it
finished. And **an abandoned agent draft is information**: a comment on the issue saying what went wrong
(ambiguous criteria, missing pointer, the wrong toolchain) improves the next attempt more than the draft
itself would have.

---

## Reviewing the Agent's Pull Request

An agent's pull request deserves the same review as a colleague's, plus a few checks aimed at how agents
fail ([Module 07](../module-07-agentic-coding/07-issue-to-pr-automation.md) has a full checklist):

1. **Read the issue first**, then the diff. Does every acceptance criterion have a test, and does each
   test actually exercise the rule? Break one rule and rerun the tests if you're unsure (Module 08).
2. **Check `git diff --stat` for surprises**: files outside the task, changed tests, new packages, edited
   instruction files or workflow YAML.
3. **Let the checks judge.** Build, tests, CodeQL, and dependency review (chapters 06 and 07) run on the
   pull request; a draft that hasn't passed them isn't ready for a human.
4. **Approve as a person.** The agent's own summary of what it did is a claim, not evidence.

> **GitHub Copilot:** Assign the issue to Copilot for Path 2, then review its draft pull request and
> comment `@copilot` with the changes you want. For Path 1, use agent mode in Visual Studio 2026 or VS
> Code with the GitHub MCP server.
>
> **Claude Code:** Run Path 1 in a terminal session beside Visual Studio, with the GitHub MCP server and
> its write tools under `ask`. For Path 3, mention `@claude` on the issue once the action's workflow is in
> the repository.

---

## Summary

- An issue reaches a pull request three ways: a **local agent with the GitHub MCP server** (supervised),
  the **Copilot cloud agent** (assign the issue to Copilot), and the **Claude Code GitHub Action**
  (`@claude`). Module 07 covers the cloud mechanics; this chapter covers the lifecycle.
- Traceability comes from the same conventions on every path: a **branch from the issue**, **`#42` in
  commits**, and **`Fixes #42` in the pull request description**. Check that an agent's pull request
  links its issue before you merge.
- On delegated paths, the **checks wait for a person** (Copilot's pull request workflows need approval;
  the action must not push with the default `GITHUB_TOKEN`), and the **requester can't approve** a
  Copilot pull request.
- **Delegate** small, fully specified issues that build on Linux; **work locally** when the issue is
  ambiguous, cross-cutting, sensitive, or needs Windows or IDE tools.
- Agents finish **small**, **concrete**, **constrained** issues with **pointers into the code** and an
  **out-of-scope** list.
- Keep the **Project board honest**: In progress when work starts, **Done only on merge**, and abandoned
  drafts moved back with a reason. Review agent pull requests **against the issue** and **approve as a
  person**.
