# Where AI Fits in the Delivery Lifecycle

Most of this course so far has been about the **inner loop**: you, an assistant, and a solution open
in Visual Studio 2026 or VS Code. You prompt, the agent edits, `dotnet build` and `dotnet test` judge
the result, and you review the diff. But software reaches users through a team process: someone writes
an issue, someone implements it, someone reviews the pull request, a workflow builds and scans it, a
release goes out, and someone supports it in production. AI can help at every one of those stages, and
each stage has its own risks.

This chapter maps where AI fits across that **delivery lifecycle** on GitHub: Issues and Projects for
planning, branches and pull requests for code and review, GitHub Actions for build and verification,
Releases, and the loop back from production. It sorts the touchpoints into what's mature and what's
still emerging as of October 2026, and it names the decisions that stay with people no matter how good
the tools get. The rest of Module 13 takes each stage in turn.

---

## The Lifecycle, Stage by Stage

Here is the whole picture: six stages, the AI touchpoint at each, and the places where a check or a
person must say yes before anything moves on.

![Six delivery stages from left to right: Plan, with GitHub Issues and Projects; Code, on a branch worked by a local agent or the Copilot cloud agent; Review, in the pull request with Copilot code review and human approval; Build and verify, in GitHub Actions running build, tests, CodeQL, and dependency review; Release, with GitHub Releases and release notes; and Operate, where incidents become new issues. AI assists at every stage, while required checks and human approval decide.](../../diagrams/png/13-ai-in-the-delivery-lifecycle.png)

*Figure: AI assists at every stage; required checks and human approval decide. Incidents loop back into Issues as new work.*

| Stage | What AI can do | What it produces | Who decides |
|-------|----------------|------------------|-------------|
| **Plan** (Issues, Projects) | Turn rough notes into issues, propose acceptance criteria, split large issues into sub-issues, spot duplicates | Draft issues | The product owner accepts the criteria |
| **Code** (branches) | A local agent or the Copilot cloud agent reads the issue, plans, edits code on a linked branch, runs build and tests | A branch and a pull request | The developer who owns the change |
| **Review** (pull requests) | Copilot code review comments on the diff; an agent reviews against your instruction files | Review comments and suggested changes | A human approver, required by a ruleset |
| **Build and verify** (GitHub Actions) | Deterministic checks first; then AI summarizes the pull request and triages failures | Check results, a PR comment, a triage note | Required checks decide; AI output is advisory |
| **Release** (Releases) | Draft release notes from merged pull requests and commits | A draft | A person edits it and publishes the release |
| **Operate** | Explain errors from logs and traces; reproduce a bug from an issue link | A hypothesis, a candidate fix, a new issue | The on-call engineer |

Two things stand out. First, every AI touchpoint produces a **draft**: an issue, a diff, a comment, a
note. Nothing in the table ships on AI say-so. Second, the stages are linked: a good issue makes a good
agent run possible, and an issue linked to its branch, pull request, checks, and release makes the
whole chain auditable. The rest of this chapter expands on both points.

### One issue, end to end

Here is how Demo 22 and Demo 23 fit together, following one small feature for the ToolsApp API through
every stage:

1. **Plan.** A stand-up note says "rename palettes, prevent duplicate names". In VS Code, an agent
   with the GitHub MCP server asks the questions the note leaves open (does case matter? which status
   code?). The product owner answers, and the agent creates an issue with numbered Given/When/Then
   criteria and the labels `enhancement`, `api`, and `ai-assisted`.
2. **Code.** `gh issue develop` creates a branch linked to the issue. The developer asks the agent to
   plan against the criteria, approves the plan, and lets it implement `PATCH /api/palettes/{id}` with
   one test per criterion. Build and tests pass locally, and each commit message ends with the issue
   number.
3. **Review.** The agent opens the pull request through the MCP server with `Fixes #<number>` in the
   description. Copilot code review flags a case-sensitive duplicate check that the path instructions
   warned about. The developer fixes it, requests another review, and a teammate approves.
4. **Build and verify.** The pull request workflow runs the checks (build with warnings as errors,
   tests, format, CodeQL, dependency review), then an advisory job posts an AI summary as a comment.
5. **Release.** Merging closes the issue, and the Project board moves it to *Done* on its own. On `main`,
   a job drafts release notes; the release manager edits two lines and publishes the release.
6. **Operate.** A week later, a support ticket reports a 500 on rename. The on-call engineer gives the
   agent the stack trace and the issue; it finds an untested path, and a new bug issue, linked to the
   original, goes back into the backlog.

At every step, the AI saved time on the draft and a person, or a deterministic check, made the decision.

---

## What's Mature and What's Still Emerging

AI features on GitHub arrive quickly, and they're at different stages. Before you plan a rollout, know
which ones are generally available (GA), which are previews, and which depend on your plan.

| Capability | Status (October 2026) | Notes |
|------------|----------------------|-------|
| GitHub MCP server, **remote** (`https://api.githubcopilot.com/mcp/`) | GA since September 2025 | OAuth sign-in in VS Code and Visual Studio; a token header for Claude Code (chapter 02) |
| GitHub MCP server, **local** (container image) | Available, open source | The fallback when the remote server isn't an option (chapter 02) |
| **Copilot code review** | GA | Needs a paid Copilot plan; it comments, it doesn't approve (chapter 04) |
| Letting Copilot **approve** pull requests | **Public preview**, off by default | Leave it off (chapter 04) |
| **Copilot cloud agent** (issue → pull request) | GA on paid plans | Formerly the "coding agent" (Module 07, chapter 05) |
| **Claude Code GitHub Action** (`anthropics/claude-code-action@v1`) | Released (v1) | `@claude` mentions or a fixed prompt (chapters 05 and 06) |
| Headless agents in workflow jobs (`claude -p`, `copilot -p`) | Documented by both vendors | Copilot CLI accepts the workflow's own token since July 2026 (chapter 06) |
| **GitHub Agentic Workflows** | **Public preview** | GitHub's recommended direction for agent automation (chapter 06) |
| CodeQL code scanning, dependency review, Dependabot, secret scanning | GA | Free on public repositories; licensed for private ones (chapter 07) |
| **Copilot Autofix** for code scanning alerts | GA | Suggested fixes on alerts and in pull requests (chapter 07) |
| **Agentic autofix** (assign an alert to Copilot) | GA since October 15, 2026, **GitHub Enterprise Cloud only** | Other plans use an agent-drafted fix (chapter 07) |
| **AI Scan for pull requests** | GA since October 15, 2026 | Advisory findings for languages CodeQL doesn't cover; it can't block a merge |
| Visual Studio 2026 Debugger Agent from a GitHub issue link | Shipping (18.5 and later) | Issue → reproducer → validated fix (Module 09) |

> **A rule for this module:** when a feature is in preview, we say so and give a fallback. Previews
> change without notice, can be withdrawn, and have no service-level agreement. Pilot them; don't make
> a release process depend on them.

The pattern behind the table is worth noticing. The **read** side (an agent querying issues, pull
requests, and workflow runs) is GA and low risk. The **write** side inside your own tools (creating
issues, opening pull requests from your IDE) is GA but needs supervision. The parts that act **without
a person at the keyboard** (agents in workflows, automatic approval, autofix that opens its own pull
requests) are the newest, and that's no coincidence: they're the hardest to make safe.

---

## Choosing Where to Start

Teams that try to add AI everywhere at once usually end up trusting none of it. Start where mistakes
are cheap and easy to spot, and expand as the evidence comes in. Four questions help rank a candidate
touchpoint:

1. **How easy is the output to verify?** A pull request summary is checked by reading the diff. A
   release-notes draft is checked against the list of merged pull requests. A security fix needs
   tests, a scan, and a security-minded reviewer.
2. **What's the blast radius if it's wrong?** A bad summary wastes a reviewer's minute. A bad automated
   merge ships a bug.
3. **Can you undo it?** Comments and drafts are trivially reversible; a published release isn't.
4. **How often does it happen?** Something every pull request does pays back faster than something a
   release does.

Applied to the lifecycle, that gives a sensible order for most .NET teams:

| Start here | Then | Later, with guardrails |
|------------|------|------------------------|
| Read-only MCP queries ("what's assigned to me?", "summarize this PR's review threads") | Refining issues through MCP, with approval prompts on every write | Delegating issues to a cloud agent |
| AI pull request summaries posted as comments | Copilot code review, automatic through a ruleset | Autofix-style fixes for security alerts |
| Failure triage notes on red builds | Release-notes drafts | Agents that change code inside a workflow |

---

## Where Humans Stay Accountable

Module 01 made the point for a single diff: **you sign the code**. Across the lifecycle there are more
signatures, and each one belongs to a person.

- **Acceptance criteria.** An assistant can draft them, but the product owner decides what "done"
  means. Agents implement whatever the criteria say, including criteria nobody actually agreed to.
- **The merge.** Copilot code review leaves a *Comment* review. By default it doesn't approve, doesn't
  request changes, and doesn't count toward required approvals. Keep a ruleset on the default branch
  that requires a pull request and at least one human approval, and leave the "Copilot can approve"
  preview off. GitHub already stops authors approving their own pull requests, and stops the person
  who asked the Copilot cloud agent for a pull request from approving it.
- **The release.** Put deployments behind a GitHub Actions **environment** with required reviewers, so
  a person approves each one. An AI-drafted release note is input to that decision, not part of it.
- **Security alerts.** Only people with the right repository permissions can dismiss a code scanning
  alert, and the dismissal records a reason. An agent that "fixes" an alert by suppressing an analyzer
  or proposing to dismiss it as a false positive hasn't fixed anything.
- **Incidents.** An agent can propose a cause and a patch. The on-call engineer decides whether to roll
  back, hotfix, or wait.

"The AI wrote it" isn't an answer in a code review, an incident review, or an audit. The tools make it
easier to produce a lot of plausible work; your process has to make it easy to see who checked it.
Module 14 turns this into
review rules and code owners.

---

## Traceability: The Thread That Ties It Together

**Traceability** means you can follow a change from the request that motivated it to the code, the
review, the checks, and the release, and back again. GitHub builds most of the links for you if you use
its conventions. AI makes them more important, not less: when an agent wrote a third of the diff, the
link from the commit to the issue (and its acceptance criteria) is how a reviewer, an auditor, or a
future maintainer understands why the code exists.

A traceable AI-assisted change has:

- an **issue** with acceptance criteria a test can check;
- a **branch linked to the issue**, created from the issue's **Development** panel or with
  `gh issue develop`, so the issue shows the work as soon as it starts;
- **commits that mention the issue** (`(#42)` at the end of the message), which appear in the issue's
  timeline;
- a **pull request** whose description says `Fixes #42`, so merging into the default branch closes the
  issue, and that says how AI was used;
- **checks** whose results, plus any advisory AI notes, are attached to the pull request;
- a **release** whose notes list the merged pull requests;
- a **label** such as `ai-assisted`, if your team wants to measure AI's share of the work
  (Module 15 covers
  measurement).

```markdown
## AI assistance

- Issue refined with an agent through the GitHub MCP server; criteria approved by the PO.
- Implementation by an agent from a reviewed plan; every acceptance criterion has a test.
- Reviewed by Copilot code review and by @<reviewer>.
```

A short section like this in the pull request template costs nothing and answers the first question
every reviewer now asks. Chapter 03 covers the linking conventions in detail.

---

## The Two Toolchains Across the Lifecycle

Everything in this module is explained tool-neutrally first. Where the concrete steps differ, you'll
see both of the course's tools side by side. Here is the overview:

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Issues and Projects** | Agent mode in Visual Studio 2026 or VS Code with the remote GitHub MCP server (OAuth); Copilot CLI has the server built in | A Claude Code session with the remote GitHub MCP server and a fine-grained token |
| **Implementation** | Agent mode in the IDE, or the Copilot cloud agent assigned to the issue | Interactive sessions in the terminal or the VS Code extension, beside Visual Studio; or `@claude` through the Claude Code GitHub Action |
| **Review** | Copilot code review, requested or automatic through a ruleset | `/code-review` in a session, a fresh session against your instruction files, Claude Code's managed Code Review (Team and Enterprise plans), or the action in a workflow |
| **GitHub Actions** | `copilot -p` with the workflow's `GITHUB_TOKEN` and the `copilot-requests: write` permission | `claude -p` (headless) or `anthropics/claude-code-action@v1`, with an API key from a secret |
| **Security fixes** | Copilot Autofix; agentic autofix on GitHub Enterprise Cloud | An agent-drafted fix from the IDE, reading the alert through the MCP server |
| **Billing** | GitHub AI Credits, plus Actions minutes for anything that runs on a runner | Plan limits or per-token API billing through the Claude Console, plus Actions minutes in workflows |

---

## Module Map

| Chapter | Stage | You'll learn |
|---------|-------|--------------|
| [02 Connecting Agents to GitHub](02-connecting-agents-to-github.md) | All | The remote and local GitHub MCP servers, toolsets, read-only mode, authentication for each client, least privilege |
| [03 GitHub Issues and Projects with AI](03-github-issues-and-projects-with-ai.md) | Plan | Refining issues and acceptance criteria, issue forms and sub-issues, linking branches, commits, and pull requests |
| [04 Copilot Code Review on GitHub](04-copilot-code-review-on-github.md) | Review | Requesting reviews, automatic review with a ruleset, instruction files, acting on comments |
| [05 Issue to Pull Request](05-issue-to-pull-request.md) | Code | The local agent, the Copilot cloud agent, and the Claude Code GitHub Action seen from the lifecycle |
| [06 Agents in GitHub Actions](06-agents-in-github-actions.md) | Build, release | Headless agents for PR summaries, failure triage, and release notes, and how to keep them safe |
| [07 GitHub Advanced Security as a Quality Gate](07-github-advanced-security.md) | Build and verify | CodeQL and merge protection, Copilot Autofix and its fallback, Dependabot, dependency review, secret scanning |
| [08 Cost Visibility](08-cost-visibility.md) | All | AI credits, Actions minutes, budgets and cost centers, the Claude Console |

Two demos carry the module. [Demo 22](../../demos/22-github-mcp-and-issues/) takes a rough idea through
an issue, a local agent with the GitHub MCP server, and a pull request reviewed by Copilot code review;
[Activity 22](../../activities/22-github-mcp-and-issues/) does the same for the Bookstore.
[Demo 23](../../demos/23-ai-in-github-actions-and-ghas/) builds a workflow with CodeQL and advisory AI
jobs and fixes a planted SQL injection with an agent;
[Activity 23](../../activities/23-ai-in-github-actions-and-ghas/) puts an agent in the Bookstore's
workflow. [Bonus Demo 04](../../demos/bonus/04-issue-to-pr-automation/) hands an issue to a cloud agent.

---

## Summary

- AI can help at every **lifecycle stage**: **plan** (Issues and Projects), **code**, **review**,
  **build and verify** (GitHub Actions), **release**, and **operate**, and at every stage it produces a
  **draft** for a person.
- The **read** side (MCP queries) is GA and low risk; the remote **GitHub MCP server**, **Copilot code
  review**, the **Copilot cloud agent**, CodeQL, and **Copilot Autofix** are GA. **Copilot approving pull
  requests** is a **preview** to leave off, **GitHub Agentic Workflows** is a **public preview**, and
  **agentic autofix** is GA only on **GitHub Enterprise Cloud**.
- Start where output is **easy to verify**, **cheap if wrong**, and **reversible**, then expand.
- People own the **acceptance criteria**, the **merge**, the **release**, **alert dismissals**, and
  **incident decisions**; Copilot's review is a comment, not an approval.
- **Traceability** (issue ↔ branch ↔ commits ↔ pull request ↔ checks ↔ release) comes from GitHub's
  own conventions (`gh issue develop`, `#42` mentions, `Fixes #42`) and matters more when agents write
  part of the code; record **how AI was used** in the pull request.
