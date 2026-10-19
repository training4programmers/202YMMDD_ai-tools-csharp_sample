# Demo 22: GitHub MCP and Issues (ToolsApp / Colors)

Instructor demo paired with [Activity 22](../../activities/22-github-mcp-and-issues/). It takes a rough
stand-up note all the way to a reviewed pull request on GitHub: a **local** agent with the GitHub MCP
server refines the idea into an issue, implements it on a branch linked to that issue, opens the pull
request, and Copilot code review (or its fallback) checks it against the team's instruction files.
[Bonus Demo 04](../bonus/04-issue-to-pr-automation/) hands an issue to a cloud agent instead; here the
agent works in your IDE or terminal, where you watch every step.

## Concept

- The **GitHub MCP server** gives an agent your issues, pull requests, and repository as tools. It acts
  as you, so scope it: load only the **toolsets** the task needs (the `X-MCP-Toolsets` header), make
  write tools ask first, and deny the ones the task never needs.
- Assistants write fluent issues that **invent decisions**. Make the agent ask the open questions, let
  the product owner answer them, then write **Given/When/Then** criteria with concrete values.
- **Traceability** is cheap when the agent does it: a branch created from the issue, `(#<number>)` in
  commit messages, and `Fixes #<number>` in the pull request description. The issue's **Development**
  panel shows the branch and the pull request, and merging closes the issue.
- **Copilot code review** joins as a reviewer when you request it (or automatically, with a branch
  ruleset). It reads `.github/copilot-instructions.md` and `.github/instructions/*.instructions.md`
  from the pull request's **head branch**, and it comments; it doesn't approve.
- When Copilot code review isn't available, a **fresh agent session** reviewing the diff against the
  same instruction files is the fallback.

![MCP clients (VS Code, Visual Studio 2026, Copilot CLI, and Claude Code) connect to the remote GitHub MCP server at api.githubcopilot.com/mcp with OAuth or a personal access token, choosing toolsets with X-MCP-Toolsets and read-only mode with X-MCP-Readonly, or to the local server, the ghcr.io/github/github-mcp-server container over stdio with a GITHUB_PERSONAL_ACCESS_TOKEN, --toolsets, and --read-only; both act on GitHub issues, pull requests, repositories, Projects, Actions, and code security](../../diagrams/png/13-github-mcp.png)

*Figure: One server, two ways to run it. This demo uses the remote server with only the toolsets the task needs; the local container is the fallback.*

## How to run

From the course repo root, copy the starting point to a lab folder outside the repo (so the repo's own
AGENTS.md/CLAUDE.md don't leak into the session) and open it. Copy the recorded outcome too, because
Steps 1 and 3 take files from it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 22 -Open
.\courseware\utils\start-lab.ps1 -Demo 22 -Solution
cd ~\labs\demo-22-github-mcp-and-issues
dotnet build
dotnet test --solution ToolsApp.slnx
```

Before class:

- **A GitHub repository for the lab**, private, in the class organization (you need permission to
  create repositories there). With the GitHub CLI signed in (`gh auth status`), push the lab and create
  the two labels the issue uses (`enhancement` exists in every new repository):

  ```powershell
  gh repo create <class-org>/toolsapp-demo22 --private --source . --push
  gh label create api --description "API change"
  gh label create ai-assisted --description "Work done with an AI assistant"
  ```

  *Fallback:* a public repository in your personal account (`gh repo create toolsapp-demo22 --public
  --source . --push`); never put a secret in it. Don't add a ruleset that requires pull requests on
  `main` yet: Step 3 pushes straight to `main`.
- **Copilot code review:** a paid Copilot plan (the class organization's seat). Open any pull request's
  **Reviewers** menu and confirm **Copilot** is listed. Each review uses GitHub AI Credits, and on a
  private repository it also uses GitHub Actions minutes. If it isn't available, plan to show the
  fallback in Step 6.
- **For Claude Code: a fine-grained personal access token**, created on the morning of Day 5 (GitHub
  **Settings** > **Developer settings** > **Personal access tokens** > **Fine-grained tokens** >
  **Generate new token**). Resource owner: the class organization. **Repository access: All
  repositories** (the class organization holds only lab repositories, and the token then covers ones
  created later). Permissions: **Contents**, **Issues**, and **Pull requests** set to **Read and
  write** (**Metadata** read is added automatically). Expiration: 7 days. The class organization is
  set not to require approval for fine-grained tokens. Store it as a user environment variable, then
  open a new terminal (and restart VS Code):

  ```powershell
  $token = Read-Host 'Paste the token' -MaskInput
  [Environment]::SetEnvironmentVariable('GITHUB_PAT', $token, 'User')
  ```

  *Fallback (a personal public repository):* resource owner your account, and **Only select
  repositories** with that one repository. VS Code, Visual Studio, and the Copilot CLI sign in with
  your GitHub account instead, so they need no token.
- **One agent, signed in:** GitHub Copilot agent mode in VS Code or Visual Studio 2026, or Claude Code.
  Use a frontier tier for Steps 2 and 4.
- Budget about 25 minutes, with the *Short on time* list at the top of the Demo trail (the full trail
  takes about 40).

If the live run goes sideways, the `-Solution` copy (`~\labs\demo-22-github-mcp-and-issues-solution`)
holds the recorded outcome: the MCP configs, the refined issue, the instruction files, the
implementation with tests, and `PR-DESCRIPTION.md` with an illustrative review log.

## Demo trail

> **Short on time (about 25 minutes).** Connect one client only in Step 1, and run Steps 2 and 3 live.
> In Step 4, run the plan prompt and review the plan, then copy `src/` and `tests/` from the `-Solution`
> copy onto the linked branch instead of waiting for the agent; build, test, and commit with
> `(#<number>)`. Run Step 5 live. In Step 6, request Copilot and come back to its comments after the
> activity, or show the illustrative review log in the solution's `PR-DESCRIPTION.md`.

**Code to open (in order):**

1. `ISSUE-DRAFT.md`: the rough note. Ask the room what they'd build from it; you'll get different
   answers, which is the point.
2. `src/ToolsApp.Api/Endpoints/PaletteEndpoints.cs`: the route group and `TypedResults` patterns the
   new endpoint should follow. There's no rename yet.
3. `src/ToolsApp.Api/Data/ToolsAppDbContext.cs`: the **unique index** on `Palette.Name`. Point out that
   SQLite compares with a case-sensitive collation by default, so "Ocean" and "ocean" can coexist.
4. `src/ToolsApp.Api/Contracts/ColorContracts.cs`: where request records and DataAnnotations live.
5. `tests/ToolsApp.Tests/ColorApiTests.cs`: the API test pattern with `ToolsAppApiFactory`.
6. From the solution, after the demo: `.vscode/mcp.json`, `.mcp.json`, `.claude/settings.json`,
   `docs/issues/palette-rename.md`, `.github/copilot-instructions.md`, and
   `.github/instructions/api.instructions.md`.

**Step 1: Connect the GitHub MCP server.** Copy the configuration from the solution, show both clients,
then pick one for the rest of the demo:

```powershell
$sol = "$HOME\labs\demo-22-github-mcp-and-issues-solution"
Copy-Item "$sol\.vscode", "$sol\.claude" . -Recurse -Force
Copy-Item "$sol\.mcp.json" .
```

- *VS Code or Visual Studio 2026:* open `.vscode/mcp.json` and show the `"type": "http"` server with
  the `X-MCP-Toolsets` header and no token. Start it (**MCP: List Servers** in VS Code), sign in with
  your GitHub account when asked, switch chat to **Agent**, and open the tool picker: only the
  `context`, `issues`, `pull_requests`, `repos`, and `copilot` tools are there. In Visual Studio, enable
  the tools in the agent-mode **Tools** picker.
- *Claude Code:* the token comes from the `GITHUB_PAT` user environment variable you set before
  class, so `.mcp.json` holds only its name. Show the class the token's settings page (never the token
  itself), because students create theirs in Activity 22 step 2. Then, in a terminal opened after you
  set the variable:

  ```powershell
  claude
  ```

  Approve the project server when asked, then run `/mcp` to show it connected and list its tools.

Prompt (Agent mode, or a Claude Code session):

```text
Using the GitHub MCP server: who am I signed in as, and what open issues and pull requests does
<owner>/toolsapp-demo22 have? Don't change anything.
```

Point out: `get_me`, `list_issues`, and `list_pull_requests` ran without asking, and the answer is
"none yet". The toolsets header means there are no Actions, Projects, or code security tools in this
session at all; `context` is listed because naming toolsets replaces the defaults, and `get_me` lives
there. In Claude Code, open `.claude/settings.json`: read tools under `allow`; the write tools the task
uses (issues, comments, the branch, the pull request, and the review request) plus `git push` under
`ask`; and under `deny`, the ones it never needs: `merge_pull_request` (a person merges), `push_files`,
`create_or_update_file`, and `delete_file` (they commit through the API, around your local build and
tests), the repository-level `create_repository`, `delete_repository`, and `fork_repository`, and
`assign_copilot_to_issue` and `create_pull_request_with_copilot` (they would hand the work to the cloud
agent, which is Bonus Demo 04's job). Write tools don't share a naming pattern, so you check the tool
list, not the names. In Copilot, approve each write call when it asks, and untick the same nine denied
tools in the tool picker.

**Step 2: Refine the issue before anyone writes code.** Prompt (Agent mode or Claude Code):

```text
Read ISSUE-DRAFT.md. Before writing anything, list the questions a product owner must answer to
make this feature testable. Then wait.
```

Read the questions aloud (case sensitivity, whitespace, status codes, renaming to your own name in a
different case, length limits). Play product owner and answer:

```text
Decisions: duplicates ignore case and surrounding whitespace; a taken name returns 409 with problem
details; a palette may change the casing of its own name; names are 1 to 60 characters; renaming
colors is out of scope. Write the issue: a user story and numbered Given/When/Then acceptance
criteria using the seeded palettes (Ocean, Sunset). Save it to docs/issues/palette-rename.md.
```

Review beat: every criterion should name concrete data and one behavior. Reject "the API returns an
appropriate error". Then create it:

```text
Create a GitHub issue in <owner>/toolsapp-demo22 from docs/issues/palette-rename.md, with the labels
enhancement, api, and ai-assisted. Show me the title, body, and labels before you create it. Then
write the issue number into the table at the top of the file.
```

Approve the `issue_write` call when prompted, then open the issue (`gh issue view <number> --web`) and
show the body, the labels, and the empty **Development** panel in the sidebar.

**Step 3: Commit the review instructions on `main`.** Copy the two instruction files from the solution
(or ask the agent to write them from the bullet points in chapter 04), then commit them with the MCP
configuration and the issue copy:

```powershell
Copy-Item "$sol\.github" . -Recurse -Force
git add .github .vscode .claude .mcp.json docs
git commit -m "Add review instructions, MCP configuration, and the palette rename issue (#<number>)"
git push
```

Walk through `.github/copilot-instructions.md` (what a reviewer must always check, and what not to
comment on) and `.github/instructions/api.instructions.md` (its `applyTo` pattern). Copilot code
review reads these files from the pull request's **head** branch, so they would also work if they
arrived in the feature pull request. Committing them on `main` first is a team habit: every branch
starts with them, Copilot chat follows the same rules while the code is being written, and the feature
pull request's diff stays about the feature.

**Step 4: Plan and implement on a branch linked to the issue.** Create the branch from the issue, so
GitHub links them before any code exists:

```powershell
gh issue develop <number> --name <number>-palette-rename --checkout
```

Refresh the issue: the branch is in its **Development** panel. Prompt (plan mode in Claude Code, or
Plan in VS Code, then Agent):

```text
Plan the implementation of issue #<number> against docs/issues/palette-rename.md: PATCH
/api/palettes/{id} following the patterns in PaletteEndpoints.cs, with one test per acceptance
criterion in a new PaletteRenameApiTests class. Uniqueness must hold in the database too, not only in
the handler. Plan only.
```

Review the plan: does it make the index case-insensitive (the `NOCASE` collation), exclude the palette
being renamed from the duplicate check, and trim the name? Then approve:

```text
Implement the plan on this branch. Run dotnet build and dotnet test --solution ToolsApp.slnx until
both pass. Commit with "(#<number>)" at the end of the message. Don't push yet.
```

Verify, don't trust: run the build and tests yourself, then show `git diff main --stat` and
`git log --oneline -1`. Optional proof that the tests can fail: remove `.UseCollation("NOCASE")` and
rerun; the two case-insensitive 409 tests fail.

**Step 5: Open the pull request through MCP.** Prompt:

```text
Push this branch, then open a pull request to main in <owner>/toolsapp-demo22 through the GitHub
MCP server. Use the issue title; in the body put "Fixes #<number>" on its own line, summarize the
change from docs/issues/palette-rename.md, and add an "AI assistance" section. Show me the title and
body before you create it.
```

Approve `git push` and `create_pull_request`. Then show the issue again: the **Development** panel now
lists the pull request next to the branch, and the commit with `(#<number>)` appears in the issue's
timeline. Point out that the closing keyword in the description is what links the pull request, and
that it closes the issue only when the pull request merges into the default branch.

**Step 6: Review, and act on the comments.**

- *Copilot code review:* ask the agent, and approve the `request_copilot_review` call:

  ```text
  Request a review from Copilot on that pull request.
  ```

  (Or select **Copilot** in the pull request's **Reviewers** menu.) When it finishes, walk through each
  comment: apply one suggestion, fix one differently, and resolve one with a reason. Push a fix and
  request Copilot again. To make this automatic for every pull request, show **Settings** > **Rules** >
  **Rulesets** > **New branch ruleset**, target the default branch, and turn on **Automatically request
  Copilot code review** (with **Review new pushes**, it reviews again after each push).
- *Fallback:* open a **new** session (a fresh chat, or `/clear` in Claude Code) and prompt:

  ```text
  Review git diff main...HEAD strictly against .github/copilot-instructions.md, the
  .instructions.md files whose applyTo patterns match the changed files, and
  docs/issues/palette-rename.md. For each finding give file, line, problem, and a suggested fix.
  Don't edit files.
  ```

If the implementation is already clean, show the *illustrative* review log in the solution's
`PR-DESCRIPTION.md`: a `ToLower()` duplicate check, a 400 where the criteria say 409, and a missing test
for criterion 4 are the typical first-attempt findings. Finish with a person: a second account (or a
colleague) approves, because GitHub doesn't let the author approve their own pull request. Merge it,
and show the issue closed with the pull request linked.

**Key teaching beat:** the expensive mistakes in this demo were all made before any code existed. The
rough note would have produced a working endpoint with a case-sensitive duplicate check and the wrong
status code, and tests that confirmed both. Refining the issue turned those guesses into failing tests,
the instruction files turned them into review comments, and the links made the whole path traceable.
AI made each step faster; a person still decided what "done" meant and approved the merge.

## Reference

- Concepts: [Module 13: AI Across the Delivery Lifecycle with GitHub](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/),
  especially [Connecting Agents to GitHub](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/02-connecting-agents-to-github.md),
  [GitHub Issues and Projects with AI](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/03-github-issues-and-projects-with-ai.md),
  [Copilot Code Review on GitHub](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/04-copilot-code-review-on-github.md), and
  [Where AI Fits in the Delivery Lifecycle](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/01-where-ai-fits-in-the-lifecycle.md)
- MCP basics: [MCP Servers](../../docs/module-07-agentic-coding/06-mcp-servers.md) (Module 07)
- Tools: [Tools Review](TOOLS-REVIEW.md)
- Practice: [Activity 22](../../activities/22-github-mcp-and-issues/)
