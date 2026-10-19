# GitHub Issues and Projects with AI

Every agent run in this course has started from a task description: a prompt, a `TASK.md`, a set of
acceptance criteria. In a team, that description lives in a **GitHub issue**, and it's usually written
in a hurry by someone who already knows what they mean. An agent doesn't. It implements the words on the
page, fills every gap with a plausible guess, and writes tests that prove its guesses.

This chapter shows how to use an assistant to turn rough notes into issues an agent (and a reviewer) can
rely on, how issue forms, labels, and sub-issues keep that shape consistent, how to create and update
issues from the IDE through the GitHub MCP server, how to keep every AI-assisted branch, commit, and pull
request linked to the issue it implements, and how to keep a **Project** board in step with the work. It
assumes you've connected the server as described in [chapter 02](02-connecting-agents-to-github.md).

---

## From Rough Notes to a Testable Issue

Demo 22 starts from this stand-up note, saved as `ISSUE-DRAFT.md`:

```text
- rename palettes
- prevent duplicate names
- API only for now
- should be quick?
```

Ask an assistant to "write an issue for this" and you'll get something that reads well and decides
several things nobody decided *(illustrative)*:

```text
As a user, I want to rename palettes so that I can organize my colors.

Acceptance criteria:
- The user can rename a palette.
- Duplicate names are not allowed.
- The API returns an appropriate error for invalid input.
- The feature is performant.
```

Every criterion is untestable as written. What's a duplicate: `Ocean` and `ocean`? `Ocean` and
`Ocean `? What's "appropriate", a 400 or a 409? What does "performant" mean? An agent handed this issue
will pick answers, and your tests will enshrine them.

The fix is to make the assistant **ask before it writes**:

```text
Read ISSUE-DRAFT.md. Before writing anything, list the questions a product owner must answer to
make this feature testable. Then wait.
```

The questions it returns (case sensitivity, whitespace, status codes, renaming to your own name in a
different case, length limits) go to the product owner, not back to the model. Once someone with the
authority to decide has answered them, the assistant is very good at the next part:

```text
Decisions: duplicates ignore case and surrounding whitespace; a taken name returns 409 with problem
details; a palette may change the casing of its own name; names are 1 to 60 characters; renaming
colors is out of scope. Write the issue: a user story and numbered Given/When/Then acceptance
criteria using the seeded palettes (Ocean, Sunset). Save it to docs/issues/palette-rename.md.
```

Do this from wherever you work: VS Code, Visual Studio 2026, a Claude Code session, or the Copilot
CLI. The habit is the same everywhere: questions first, decisions from a person, then the draft, saved
as a file you can review as a diff.

---

## Acceptance Criteria an Agent and a Test Can Use

**Acceptance criteria** are the conditions an issue must meet to be done. For AI-assisted work, write
them so each one maps to exactly one automated test:

- **Given/When/Then** with concrete data: palette names, prices, dates, not "a valid value".
- **One behavior per criterion.** "Rename succeeds and duplicates are rejected" is two criteria.
- **Boundaries stated explicitly**, because that's where agents guess: "a book published *today* is not
  a pre-order".
- **Status codes and error shapes** for APIs: 400 with a validation problem for `Name`, 409 with
  problem details.
- **Out of scope**, listed. Agents love to add features nobody asked for.

Here are two of Demo 22's criteria after refinement:

```text
3. Duplicate names are rejected, ignoring case.
   Given the palettes "Ocean" and "Sunset" exist,
   when I rename "Ocean" to "Sunset" or "sunset",
   then the response is 409 Conflict with a problem details body,
   and "Ocean" keeps its name.

4. A palette may change the casing of its own name.
   Given the palette "Ocean" exists,
   when I rename it to "OCEAN",
   then the response is 200 OK and the name is "OCEAN".
```

Criterion 4 is the one an agent misses. A natural first implementation checks "does any palette have
this name?", which also matches the palette being renamed, and returns 409. A refined issue turns that
bug into a failing test before a line of code exists.

> **Where the criteria live.** The issue is the source of truth. The demos and activities also keep a
> copy in `docs/issues/` in the repository, because an agent reads repository files more reliably than
> it calls a tool, and the copy versions with the code that implements it. If you do the same, update
> both in the pull request that changes the behavior.

---

## Issue Forms, Labels, Sub-issues, and Issue Types

Refinement is easier when every issue starts in the right shape. GitHub gives you four tools for that.

### Issue forms

An **issue form** is a YAML file in `.github/ISSUE_TEMPLATE/` that turns "New issue" into a form with
named, optionally required fields. A feature-request form that asks for testable criteria:

```yaml
name: Feature request
description: A change to the API's behavior, with criteria a test can check.
title: "[Feature]: "
labels: ["enhancement"]
body:
  - type: textarea
    id: story
    attributes:
      label: User story
      description: As a <role>, I want <capability>, so that <benefit>.
    validations:
      required: true
  - type: textarea
    id: criteria
    attributes:
      label: Acceptance criteria
      description: Numbered Given/When/Then criteria with concrete values, one behavior each.
      placeholder: |
        1. Given ..., when ..., then the response is ... and ...
    validations:
      required: true
  - type: textarea
    id: out-of-scope
    attributes:
      label: Out of scope
  - type: dropdown
    id: ai-assistance
    attributes:
      label: AI assistance
      options:
        - None
        - Drafted with AI, reviewed by the author
```

A `config.yml` in the same folder with `blank_issues_enabled: false` makes the forms the only way in.
For work you plan to hand to an agent, Module 07 shows an **agent-ready task** form with Goal,
Contract, Acceptance criteria, Constraints, and Pointers fields
([Issue to Pull Request: Cloud Agents on GitHub](../module-07-agentic-coding/07-issue-to-pr-automation.md)).
A form doesn't make criteria good, but it makes a missing one obvious.

### Labels, sub-issues, and issue types

- **Labels** carry the facts you'll filter on later: the area (`api`), the kind (`enhancement`), and,
  if your team measures it, `ai-assisted`. Labels must exist before an agent can apply them; create
  them once with `gh label create`, or through the MCP server's `labels` toolset.
- **Sub-issues** split a large issue into children that each fit one pull request. The parent shows the
  children and their progress, so the plan stays visible after the work is spread across several pull
  requests. The MCP server's `sub_issue_write` tool adds and removes them.
- **Issue types** (where your organization has them; organization owners define them, and new
  organizations start with Task, Bug, and Feature) classify issues consistently across every
  repository in the organization.

---

## Creating and Updating Issues Through MCP

With the `issues` toolset loaded, the assistant can create the issue it just drafted:

```text
Create a GitHub issue in <owner>/<repo> from docs/issues/palette-rename.md, with the labels
enhancement, api, and ai-assisted. Show me the title, body, and labels before you create it. Then
write the issue number into the table at the top of the file.
```

A few details make this reliable:

- **Name the repository** as `<owner>/<repo>`. An agent with access to several repositories will
  otherwise guess, usually from the git remote, which may be a fork.
- **Ask to see the fields first.** Combined with an approval prompt on `issue_write`, that gives you two
  chances to catch a mistake before anything lands on GitHub.
- **Read back after writing.** "Read issue 42 and show me the acceptance criteria" confirms the Markdown
  survived; long numbered lists sometimes don't.
- **Prefer a file as the source.** Drafting into `docs/issues/*.md` first means the text is reviewed
  as a diff and versioned, and the issue is created from exactly what you approved.

Refining existing issues works the same way, starting from a read:

```text
List the open issues in <owner>/<repo> labeled enhancement that have no "Acceptance criteria"
section. For each, propose numbered Given/When/Then criteria, but don't update anything.
```

```text
Issue 42 is too big for one pull request. Propose two to four sub-issues, each independently
testable. After I approve, create them and add them as sub-issues of 42.
```

The read tools (`issue_read`, `list_issues`, `search_issues`) are safe to auto-approve. The write tools
(`issue_write` to create or update, `add_issue_comment`, `sub_issue_write`) should ask every time.

The GitHub CLI is the scriptable alternative when you don't need an agent in the loop, and it's what
you'd use to create the issue from a reviewed file by hand:

```powershell
gh issue create --repo <owner>/<repo> --title "Rename palettes" --body-file docs/issues/palette-rename.md --label enhancement,api,ai-assisted
# https://github.com/<owner>/<repo>/issues/42
```

> **GitHub Copilot:** In Visual Studio 2026 or VS Code, use **Agent** mode with the GitHub MCP server's
> tools enabled, and approve each `issue_write` call when it asks. In the Copilot CLI, the server is
> already there. Visual Studio's Debugger Agent (18.5 and later) can also start from a GitHub issue link
> and work from issue to reproducer to validated fix (Module 09).
>
> **Claude Code:** Run a session in the repository with the server in `.mcp.json` (`/mcp` shows it).
> Keep the write tools under `ask` in `.claude/settings.json`, and use plan mode when you want the full
> set of changes proposed before any are made.

---

## Linking AI-Assisted Work to Issues

Links are what make the lifecycle traceable, and on GitHub most of them come from conventions rather
than settings.

- **Create the branch from the issue.** The issue's **Development** panel (in the sidebar) has **Create a
  branch**, and the GitHub CLI does the same from your terminal. Either way, the branch appears in the
  issue before any code exists, and a pull request opened from it is linked automatically:

  ```powershell
  gh issue develop 42 --name 42-palette-rename --checkout
  ```

- **Mention the issue in commit messages.** `#42` anywhere in a commit message adds the commit to issue
  42's timeline once it's pushed. End each message with `(#42)`, and an agent's commits are as
  traceable as yours.
- **Close it from the pull request.** A **closing keyword** (`close`, `closes`, `closed`, `fix`, `fixes`,
  `fixed`, `resolve`, `resolves`, `resolved`) followed by the issue number in the pull request
  **description**, such as `Fixes #42`, links the two and closes the issue when the pull request merges
  into the **default branch**. In the title it does nothing. Use one keyword per issue: `Fixes #42,
  fixes #43`. For an issue in another repository, write `Fixes <owner>/<repo>#42`.
- **Link by hand when you must.** The pull request's **Development** panel can link an issue without a
  keyword.
- **Enforce it in review.** GitHub has no built-in rule that a pull request must link an issue, so make
  it part of the pull request template and the review checklist (or a small required workflow that
  checks the description, if your team wants a hard rule).

| Link | How | What happens |
|------|-----|--------------|
| Issue → branch | **Create a branch** in the Development panel, or `gh issue develop` | The branch, and later its pull request, show on the issue |
| Commit → issue | `#42` in the commit message | The commit appears in the issue's timeline |
| Pull request → issue | `Fixes #42` in the description, or the Development panel | Linked; merging into the default branch closes the issue |
| Issue → Project | The project's auto-add workflow, or adding it by hand | The item gets a status and moves with the work (next section) |

This is the instruction an agent needs to do all of it:

```text
Implement issue #42 on this branch. End each commit message with (#42). When dotnet build and
dotnet test --solution ToolsApp.slnx pass, push, and open a pull request to main through the GitHub
MCP server with "Fixes #42" on its own line in the description and an "AI assistance" section.
```

---

## Projects: Keeping the Board Honest

A **Project** is GitHub's planning board: a table, board, or roadmap of issues and pull requests from
one or more repositories, with fields you define. The **Status** field (Todo, In progress, Done by
default) drives the board; you can add others, such as an **Iteration**, an estimate, or a
single-select **AI assistance** field if your team prefers that to a label.

Projects have **built-in workflows**. Two are on in every new project: when an issue or pull request
is **closed**, and when a pull request is **merged**, its status becomes **Done**. Others you can turn on
add new issues from a repository automatically when they match a filter, set new items to Todo, or
archive old ones. A newer one, **Pull request linked to issue** (added in November 2025), sets an issue
to **In progress** as soon as a pull request linked to it exists; check that it's on in the Project's
**Workflows** settings. Combined with the linking conventions above, most of the lifecycle takes care of
itself: opening a pull request that says `Fixes #42` moves issue 42 to In progress, and merging it closes
the issue and moves it to Done.

The gap is at the start, between creating the branch and opening the pull request. During that time
nothing moves the item, so the board can say Todo while someone, or an agent, has been working for a
day. Decide who closes the gap. A person can, when they create the branch; an agent can, through the
MCP server's `projects` toolset, if you load it and approve the write; or opening a **draft** pull
request early lets the linked-pull-request workflow do it. Then use the assistant for the part people
skip, checking the board against reality:

```text
Using the GitHub MCP server, list the items in our project whose Status is In progress. For each,
tell me whether it has a linked branch or an open pull request, and when either last changed. Don't
change anything.
```

---

## Traceability for AI-Assisted Work

Links tell you *what* changed for *which* request. For AI-assisted work, two more habits tell you *how*:

- **Label the issue** (`ai-assisted`, or more specific labels such as `ai-refined` and `ai-implemented`)
  if your team wants to measure AI's share of the work over time. Agree on the labels first; a label
  nobody applies consistently measures nothing.
- **Say how AI was used in the pull request.** A short "AI assistance" section in your pull request
  template (chapter 01, and Module 14)
  records whether an agent refined the criteria, wrote the code, or reviewed it, and who checked each
  step.

```markdown
## AI assistance
- [ ] Issue criteria drafted with AI and approved by: @<po>
- [ ] Code written mostly by an agent; every acceptance criterion has a test
- [ ] AI review (Copilot code review or an agent review) addressed; human approval below
```

And keep the issue honest. When review changes the behavior, update the acceptance criteria in the
issue and the repository copy in the same pull request; an agent picking up the next issue reads the
criteria, not the review thread.

---

## Summary

- An assistant turns rough notes into well-written issues quickly, but it **invents decisions**; make it
  **ask the questions first**, and have the product owner answer them.
- Write **acceptance criteria** as **Given/When/Then** with **concrete values**, **one behavior per
  criterion**, explicit **boundaries**, and an **out-of-scope** list, so each maps to one test.
- **Issue forms** (`.github/ISSUE_TEMPLATE/*.yml`) keep issues in that shape; **labels**, **sub-issues**,
  and **issue types** organize them.
- Create and refine issues **through MCP** (`issue_write`, `sub_issue_write`) with **write tools on
  approval**, a named `<owner>/<repo>`, and a **read-back**; `gh issue create --body-file` is the
  scriptable alternative.
- Link the work: a **branch from the issue** (Development panel or `gh issue develop`), **`#42` in commit
  messages**, and **`Fixes #42` in the pull request description**, which closes the issue on merge into
  the **default branch**.
- **Projects** move closed and merged items to **Done** by default, and the **Pull request linked to
  issue** workflow sets an issue to **In progress** once a pull request links it; decide who covers the
  gap between the branch and the pull request, and use the assistant to check the board against reality.
