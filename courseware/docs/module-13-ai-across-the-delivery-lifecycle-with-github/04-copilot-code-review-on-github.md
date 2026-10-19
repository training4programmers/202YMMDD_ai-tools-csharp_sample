# Copilot Code Review on GitHub

AI makes it cheap to produce pull requests, so review becomes the bottleneck. An automated first pass
that catches the routine problems (a missing `CancellationToken`, a `!` hiding a null, an untested
acceptance criterion) frees human reviewers for design and intent. **Copilot code review** is GitHub's
version of that first pass: **Copilot** appears as a reviewer on a pull request, reads the diff and the
code around it, and posts comments, often with a suggested change you can apply in a couple of clicks.

This chapter covers what the reviewer does and doesn't do, how to request a review and make it
automatic with a ruleset, how to steer it with instruction files at repository and path level, how to
act on its comments, the fallback when it isn't available, and what it costs. Copilot code review is
**generally available** and needs a paid Copilot plan; GitHub's
[Using Copilot code review](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/request-a-code-review/use-code-review)
page is the authority, and it changes often enough to check before a rollout.

---

## What It Does, and What It Doesn't

When a review runs, Copilot posts its feedback like any other reviewer:

- comments on the specific lines they apply to, often with a **suggested change**;
- a **Comment** review by default: not *Approve*, not *Request changes*, so it doesn't count toward a
  ruleset's required approvals and doesn't block a merge on its own;
- one review per request: it **doesn't re-review new pushes** unless you've configured it to (below),
  and on a re-review it may repeat a comment you dismissed;
- it **doesn't read replies**. Comments you add to its threads are for the humans; Copilot doesn't see
  them and won't answer.

That last point matters for how you work with it. Replying "this is intentional" teaches Copilot
nothing. To change what it flags next time, change the instructions (below).

> **Leave approval off.** "Copilot can approve pull requests" is a **public preview** (since September
> 2026), off by default at the enterprise, organization, and repository levels. Leave it off. An
> approval is a person's statement that they've read the change and accept responsibility for it; an AI
> reviewer can't make that statement, and a ruleset that counts its approval has quietly removed the
> human from the merge.

Copilot code review runs its agentic analysis on GitHub Actions runners, which is why it has a cost in
Actions minutes on private repositories (see *Cost*, at the end).

---

## Requesting a Review

There are four ways to ask, and they all do the same thing:

- **On the pull request:** under **Reviewers** in the sidebar, select **Request** next to **Copilot**.
- **From the terminal:**

  ```powershell
  gh pr edit 17 --add-reviewer "@copilot"
  ```

- **From an agent**, through the GitHub MCP server's `copilot` toolset: "Request a review from Copilot
  on pull request 17" calls `request_copilot_review` (keep it under approval).
- **Through the REST API**, by requesting the `copilot-pull-request-reviewer[bot]` reviewer, for scripts.

Each review runs at an **effort level**: **Lite** for a cost-efficient pass on glaring issues,
**Balanced** (the default since September 28, 2026) for deeper analysis of complex logic and
security-sensitive code with a higher-reasoning model, and **Max**, the highest-effort review, which uses
several high-reasoning models for the pull requests that need the most scrutiny. Higher levels use more
AI credits. Pick the level per review in the **Reviewers** section, or set your default in your Copilot
settings; organization owners and repository admins set the default for automatic reviews.

After you push fixes, use the **re-request** button next to Copilot's name in **Reviewers** to get a
fresh review, before a human approves.

---

## Making It Automatic: A Ruleset

To have every pull request into a branch reviewed without anyone remembering to ask, add a **branch
ruleset** rule:

1. In the repository, open **Settings** > **Rules** > **Rulesets**, and choose **New ruleset** >
   **New branch ruleset**.
2. Name it, and set **Enforcement status** to **Active**.
3. Under **Target branches**, add the default branch (or a pattern such as `release/*`).
4. Under **Branch rules**, select **Automatically request Copilot code review**.
5. Optionally select **Review new pushes**, so Copilot reviews again after each push (without it, it
   reviews each pull request once), and **Review draft pull requests**, so it starts before the author
   marks the pull request ready.
6. Select **Create**.

An organization owner can create the same ruleset once at the organization level and target
repositories by pattern, which is how you roll it out to a team. Individuals on Copilot Pro, Pro+, and
Max plans can also turn on automatic review for their own pull requests in their Copilot settings.

The rule only *requests* the review. It sits beside the rules that actually protect the branch:
**Require a pull request before merging** with at least one **required approval** from a person,
**Require status checks to pass**, and, for the files that matter most, **Require review from Code
Owners** (Module 14). Put
them in the same ruleset. (If you enabled GitHub Code Quality before August 2026, it may have created a
review ruleset for you; it no longer does, so check the rulesets list rather than assuming.)

---

## What It Reads

Out of the box, Copilot reviews against general good practice. **Custom instructions** tell it what your
team cares about. Copilot code review reads them from the pull request's **head branch** (the branch
being merged) since July 2026:

| File | Scope | Use it for |
|------|-------|------------|
| `.github/copilot-instructions.md` | The whole repository | Review priorities: what to always check, and what not to comment on |
| `.github/instructions/**/*.instructions.md` with `applyTo` | Files matching the `applyTo` glob | Rules for one project, folder, or file type, such as API endpoints or tests |
| `AGENTS.md` at the repository root | The whole repository | Project context shared with other agents (Module 06) |
| `CLAUDE.md` and `REVIEW.md` | The whole repository | Also read at the time of writing, so a team using both tools can share one set of review rules |

Three consequences of how it reads them:

- **You can test instruction changes in the same pull request.** Because the head branch is read, a
  pull request that edits `api.instructions.md` is reviewed with the edited rules.
- **A pull request can also change its own review rules.** That's the other side of the same coin. Put
  `.github/copilot-instructions.md`, `.github/instructions/`, `AGENTS.md`, and `CLAUDE.md` under a
  `CODEOWNERS` entry so a maintainer approves every change to them. That gates the merge, not the
  review: the pull request that edits the rules is still reviewed with its own edits, so the
  maintainer reads those changes as carefully as code.
- **Only the first 4,000 characters of each instruction file count** for code review. Anything after
  that is ignored, so put the review rules first and keep them short. The limit doesn't apply to
  Copilot Chat or the cloud agent, which is one more reason to keep review rules separate from long
  architecture notes.

A path-scoped file that shouldn't steer reviews (a code-generation recipe, say) can opt out with
`excludeAgent: "code-review"` in its front matter.

---

## Writing Review Instructions That Work

The guidance that works is the same for any reviewer: be specific, focus on high-impact problems,
include an example where a rule could be misread, say what *not* to comment on, and keep it short,
because every word is processed on every review. For a C# codebase, the most valuable rules target the
failure modes from Modules 01 and 12. A repository-level `.github/copilot-instructions.md`:

```markdown
## Always check
- Acceptance criteria: if the PR says "Fixes #<n>" or changes a file in docs/issues/, name any
  criterion that has no test, and any test that can't fail.
- Nullable honesty: flag every new `!` and `= null!` without a comment proving the value isn't null.
- Async: flag `.Result`, `.Wait()`, `async void`, and EF Core or HTTP calls without the CancellationToken.
- EF Core: flag `AsEnumerable()` or `ToList()` before `Where`, and `ToLower()` on columns.
- Tests: flag deleted, skipped, or weakened tests.
- Dependencies: flag every new PackageReference; versions belong in Directory.Packages.props.

## Don't
- Don't comment on formatting; dotnet format runs in the workflow.
- Don't suggest Newtonsoft.Json, Startup.cs, or controllers.
```

A "Don't" section is as useful as the checklist: every false positive you prevent saves a reviewer from
reading and dismissing it.

Rules for one part of the solution go in a path-scoped file. This is Demo 22's
`.github/instructions/api.instructions.md`, shortened; the `applyTo` glob limits it to the API project,
so it never fires on a test-only pull request:

```markdown
---
applyTo: "src/ToolsApp.Api/**/*.cs"
---

# ToolsApp API endpoint rules

- Return `TypedResults`, and list every outcome in the `Results<...>` union.
- 400 for a malformed request, 404 for an unknown id, 409 (as problem details) when a valid request
  conflicts with existing data, such as a duplicate name.
- Every handler takes a `CancellationToken` and passes it to every EF Core call.
- Palette names are unique ignoring case. The column uses the `NOCASE` collation, so compare with
  `p.Name == name`; don't call `ToLower()` on the column.
```

The same files steer GitHub Copilot in Visual Studio 2026 and VS Code while the code is being written
(Module 06), so one set of rules shapes both the code Copilot writes and the review it gives. That's a
good reason to commit them on `main` before the feature work starts, as Demo 22 does, even though the
review would also pick them up from the feature branch.

---

## Acting on the Results

Copilot's comments are a first pass, not a verdict. For each one, decide:

| The comment is | Do this |
|----------------|---------|
| Right, and the suggested change is right | Apply the suggestion (or batch several into one commit), then build and test; one-click edits still need the compiler |
| Right, but the suggestion is wrong or incomplete | Fix it your way; resolve the thread with a one-line note saying what you did |
| Wrong for this codebase | Resolve it with the reason, give it a thumbs down, and if it will recur, add a "Don't" line to the instructions |
| Noise (style, taste) | Resolve it, and tighten the instructions so it stops |

An *illustrative* example from Demo 22, on the first attempt at the palette rename:

```text
Copilot commented on src/ToolsApp.Api/Endpoints/PaletteEndpoints.cs, line 71:
The duplicate check uses p.Name.ToLower() == name.ToLower(). Per the API instructions, palette names
are compared with the column's NOCASE collation; ToLower() on the column bypasses the unique index,
and the index itself is case-sensitive, so concurrent requests could still create "Ocean" and "ocean".
Suggested change: compare p.Name == name and add UseCollation("NOCASE") to the Name property.
```

That one is right, and it came straight from the path instructions. The fix also needs a model change
the suggestion can't apply for you, which is typical: accept the idea, implement it properly, and let
the tests confirm it. When both Copilot code review and the Copilot cloud agent are enabled, **Fix with
Copilot** on a comment hands the change to the cloud agent instead, on the same pull request or a new
one; review what comes back like any other commit.

Then **re-request a review** after you push, and before a human approves. Two habits keep the reviewer
honest:

- **No comments isn't approval.** A clean review means Copilot found nothing it looks for. The human
  reviewer still owns intent, design, and anything the instructions don't mention.
- **Track the hit rate for a few weeks.** If most comments get resolved as noise, the instructions need
  work, or the feature isn't paying for itself on that repository.

---

## When Copilot Code Review Isn't Available: The Fallback

If your plan doesn't include it, your organization hasn't enabled it, or a policy rules it out, you can
get most of the value from an agent in a **fresh session** that reviews the diff against the **same
instruction files**:

```text
You are reviewing a pull request. Run git diff main...HEAD. Review it strictly against
.github/copilot-instructions.md, the .instructions.md files whose applyTo patterns match the changed
files, and the acceptance criteria in docs/issues/. For each finding give the file, the line, the
problem, and a suggested fix. Don't edit any files.
```

A fresh session matters: the agent that wrote the code is the worst reviewer of it, because it shares
the same assumptions. Post the findings as review comments yourself, or keep them in the pull request
description's review log (Demo 22's `PR-DESCRIPTION.md` shows one).

> **GitHub Copilot:** In Visual Studio 2026 or VS Code, open a **new** chat in **Ask** or **Agent** mode
> and use the prompt above; the `.github/` instruction files load automatically.
>
> **Claude Code:** Start a new session (or `/clear`) in the repository and use the prompt above, or run
> the built-in `/code-review` command on your branch, on any plan. Organizations on Team and Enterprise
> plans can also turn on Claude Code's managed **Code Review**, which reviews GitHub pull requests
> automatically, posts inline comments and a check run, and never approves or blocks. You tune it with
> `REVIEW.md` and `CLAUDE.md`, and it reads `REVIEW.md` from the base branch, never from the pull
> request's own changes.

---

## Cost

Each review consumes **GitHub AI Credits** from the plan of the person or organization the review is
billed to. The effort level, the size of the pull request, and the length of your instruction files all
affect how many. On **private repositories**, each review also consumes **GitHub Actions minutes** (since
June 2026); public repositories aren't affected. So automatic review with **Review new pushes** on a busy
private repository costs more than you might guess: every push is a review.

To estimate before a wide rollout, enable the ruleset on one or two repositories, compare the effort
levels, and watch the usage for two weeks. [Chapter 08](08-cost-visibility.md) shows where the credits and
minutes appear and how to put a budget on them.

**Turning it off** is equally simple: disable the rule in the ruleset (or set the ruleset to
**Disabled**), and turn off automatic review in your personal Copilot settings if you enabled it there.

---

## Summary

- **Copilot code review** (GA, paid plans) joins a pull request as a reviewer and posts **comments with
  suggested changes**. It leaves a **Comment** review, doesn't count toward **required approvals**,
  doesn't **read replies**, and doesn't **re-review** pushes unless configured to.
- Request it from **Reviewers**, with `gh pr edit --add-reviewer "@copilot"`, or through the MCP
  server's **`request_copilot_review`**. Make it automatic with the ruleset rule **Automatically request
  Copilot code review**, optionally with **Review new pushes** and **Review draft pull requests**.
- It reads **`.github/copilot-instructions.md`**, path-scoped **`.github/instructions/*.instructions.md`**
  (**`applyTo`**; **`excludeAgent: "code-review"`** opts a file out), and **`AGENTS.md`** from the PR's
  **head branch**, and only the **first 4,000 characters** of each file. Protect those files with
  **CODEOWNERS**.
- **Act on each comment** deliberately, **re-request** after pushes, and tune the instructions from the
  noise. Leave the **"Copilot can approve" preview off**; a person approves the merge.
- Without Copilot code review, a **fresh agent session** reviewing the diff against the same
  instructions is the **fallback**; Claude Code adds `/code-review` and, on Team and Enterprise plans, a
  managed Code Review.
- Reviews cost **AI credits**, plus **Actions minutes** on **private repositories**.
