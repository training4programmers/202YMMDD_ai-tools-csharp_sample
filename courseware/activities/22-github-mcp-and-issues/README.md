# Activity 22: Issue to PR with the GitHub MCP Server

## Objective

Configure an AI agent to work with GitHub, then use it to take a Bookstore feature from a rough idea to
a reviewed pull request. The feature is **pre-orders**: books with a future `PublishedOn` date can be
quoted but not stocked. You'll connect the GitHub MCP server to your assistant with least privilege,
have the agent refine `ISSUE-DRAFT.md` into an issue with testable acceptance criteria and create it on
GitHub, add review instructions for Copilot code review, implement the feature on a branch linked to
the issue, and open and review the pull request, all with the links that make the work traceable.

![MCP clients (VS Code, Visual Studio 2026, Copilot CLI, and Claude Code) connect to the remote GitHub MCP server at api.githubcopilot.com/mcp with OAuth or a personal access token, choosing toolsets with X-MCP-Toolsets and read-only mode with X-MCP-Readonly, or to the local server, the ghcr.io/github/github-mcp-server container over stdio with a GITHUB_PERSONAL_ACCESS_TOKEN, --toolsets, and --read-only; both act on GitHub issues, pull requests, repositories, Projects, Actions, and code security](../../diagrams/png/13-github-mcp.png)

*Figure: You'll connect the remote server with only the toolsets this task needs. The local container is the fallback if your network blocks the remote one.*

## Prerequisites

- .NET 10 SDK, Git, the GitHub CLI signed in (`gh auth status`), and Visual Studio 2026 or VS Code
  with C# Dev Kit
- One AI tool, signed in: GitHub Copilot (Agent mode in VS Code or Visual Studio 2026) or Claude Code
- **GitHub access from your instructor:** membership in the class organization, with a Copilot seat
  and permission to create private repositories there
- **For Claude Code:** you'll create a fine-grained personal access token for the class organization
  in step 2 (the organization doesn't require approval for them). GitHub Copilot signs in with your
  GitHub account instead.
- **No class organization?** Use a public repository in your personal account (Copilot code review
  then needs your own paid Copilot plan; without one, use the fallback review in step 8), or follow
  the **offline path** in step 1
- Module 13 chapters 01–04, and [Demo 22](../../demos/22-github-mcp-and-issues/)

## Skills Practiced

- Configuring the GitHub MCP server for your assistant, scoped to the toolsets a task needs
- Making write tools require approval while read tools run freely, and denying the ones a task never
  needs
- Turning a rough idea into an issue with concrete Given/When/Then acceptance criteria
- Creating and linking issues, branches, commits, and pull requests through an agent
- Writing repository-level and path-level review instructions for Copilot code review
- Reviewing an agent's pull request against the issue, with Copilot code review or the fallback

## Instructions

Start the lab from the course repo root. This copies `begin/` to a folder outside the repo, makes it a
git repository, and commits a baseline, so `git diff` always shows exactly what you and your assistant
changed:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 22
cd ~\labs\activity-22-github-mcp-and-issues
```

Open the folder (or `Bookstore.slnx`) in Visual Studio 2026 or VS Code. To start over, re-run the
command with `-Force`. The reference in `solution/` is **one acceptable result**; yours will differ.

> **In class / take-home.** In class (about 50 minutes, across a break): steps 1 to 7, on the offline
> path if you can't reach GitHub. Step 8 (the review) if time allows; otherwise it's take-home, with the
> stretch goal.

1. **Connect your repository.** Create a private repository for the lab in your class organization,
   push `main` to it, and create an `ai-assisted` label there. *Fallback:* a public repository in your
   personal account; never put a secret in it. *Offline path:* skip the pushes and GitHub writes in
   the steps below; do everything else locally, and record what the agent *would* have sent in
   `docs/issues/` and `PR-DESCRIPTION.md`.

   <details>
   <summary>Guided help: step 1</summary>

   Run these from the lab folder. Pick a repository name that's unique in the class organization, such
   as `bookstore-preorders-<your-handle>`:

   ```powershell
   gh auth status
   # Logged in to github.com account <your-handle>

   gh repo create <class-org>/bookstore-preorders-<your-handle> --private --source . --push
   # Creates the repository, adds it as the origin remote, and pushes main

   gh label create ai-assisted --description "Work done with an AI assistant"
   # Creates the label (enhancement already exists in every new repository)
   ```

   **Check:** `git remote -v` lists `origin`, and `gh repo view --web` shows your baseline commit on
   `main`.

   **If it doesn't:** if `gh repo create` says you can't create repositories in the organization, ask
   your instructor, or use the fallback in your personal account:
   `gh repo create bookstore-preorders --public --source . --push`. With no GitHub access at all, take
   the offline path and skip the label.

   </details>

2. **Configure the MCP server for your tool.** Add the remote GitHub MCP server so that:
   - it loads only the `context`, `issues`, `pull_requests`, and `repos` toolsets (add `copilot` only if
     you'll request Copilot's review through MCP in step 8);
   - it signs in **without a token or repository name hard-coded** in a committed file;
   - read tools run without asking, the write tools this task uses **ask first**, and the tools it never
     needs (merging, committing files through the API, the repository-level tools, and handing work to
     the cloud agent) are denied or switched off.

   Commit the configuration. Prove it works with a read-only question about your repository.
   *Offline path:* skip the token and the live question; instead, check that the files parse, hold no
   token or repository name, and match the three rules above.

   <details>
   <summary>Guided help: step 2</summary>

   > **GitHub Copilot:** `.vscode/mcp.json`, key `"servers"`: the same entry as below without the
   > `Authorization` header. Start it from **MCP: List Servers** and sign in with your GitHub account;
   > you need no token. Untick `merge_pull_request`, `push_files`, `create_or_update_file`,
   > `delete_file`, `create_repository`, `delete_repository`, `fork_repository`,
   > `assign_copilot_to_issue`, and `create_pull_request_with_copilot` in the tool picker (the last two
   > come with the `copilot` toolset).
   >
   > **Claude Code:** `.mcp.json` as below, plus `.claude/settings.json` that allows the read tools
   > (`get_me`, `issue_read`, `list_issues`, `search_issues`, `pull_request_read`, `list_pull_requests`,
   > `get_file_contents`, each written `mcp__github__<tool>`), asks for the write tools the task uses
   > (`issue_write`, `add_issue_comment`, `create_branch`, `create_pull_request`, `update_pull_request`,
   > `request_copilot_review`, and the other comment and review tools), and denies the nine above.

   ```json
   { "mcpServers": { "github": {
       "type": "http",
       "url": "https://api.githubcopilot.com/mcp/",
       "headers": {
         "Authorization": "Bearer ${GITHUB_PAT}",
         "X-MCP-Toolsets": "context,issues,pull_requests,repos,copilot" } } } }
   ```

   For Claude Code, create the token now: GitHub **Settings** > **Developer settings** > **Personal
   access tokens** > **Fine-grained tokens** > **Generate new token**. Resource owner: the class
   organization. Repository access: **All repositories** (the organization holds only lab
   repositories, and the token then covers ones created later). Permissions: **Contents**, **Issues**,
   and **Pull requests** set to **Read and write** (**Metadata** read is added automatically).
   Expiration: 7 days. *Fallback repository in your own account:* resource owner your account, and
   **Only select repositories** with that one repository. Store the token as a user environment
   variable, then open a new terminal (and restart VS Code) so it's there:

   ```powershell
   $token = Read-Host 'Paste the token' -MaskInput
   [Environment]::SetEnvironmentVariable('GITHUB_PAT', $token, 'User')
   # Then, in a new terminal in the lab folder:
   claude
   ```

   Commit the files, then ask:

   ```text
   Check the GitHub MCP connection before we use it for real. Call get_me, list the open issues in
   <owner>/<repo>, and read ISSUE-DRAFT.md from main with get_file_contents. Read only: if any step
   would need a write tool, stop and tell me which one.
   ```

   Offline, there's no server to ask, so check the files instead:

   ```powershell
   $files = '.mcp.json', '.vscode/mcp.json', '.claude/settings.json' | Where-Object { Test-Path $_ }
   $files | ForEach-Object { $null = Get-Content -Raw $_ | ConvertFrom-Json; "$_ parses" }
   Select-String -Path $files -Pattern 'github_pat_|ghp_|bookstore-preorders'
   # Each file parses, and Select-String prints nothing: no token and no repository name
   ```

   **Check:** the answer names your account, no open issues, and the draft's title; in Claude Code no
   call asked for approval. Offline: does every file parse with no `Select-String` match, and is each of
   the nine tools denied or unticked?

   **If it doesn't:** `/mcp` (Claude Code) shows the server's state: a failed server usually means
   `GITHUB_PAT` isn't set in that terminal (open a new one after setting it), and a missing `get_me`
   means `context` isn't in the toolsets header.

   </details>

3. **Refine the issue.** Have the agent read `ISSUE-DRAFT.md` and list the questions a product owner
   must answer before it writes anything. Decide the answers yourself (your instructor may play product
   owner). Then have it write the issue: a user story with numbered Given/When/Then criteria that use
   concrete dates and prices, saved to `docs/issues/pre-orders.md`.

   <details>
   <summary>Guided help: step 3</summary>

   Two prompts in the same session: questions first, then your decisions.

   > **GitHub Copilot:** Ask mode for the questions, then Agent mode to write the file.
   >
   > **Claude Code:** plan mode (Shift+Tab until the footer shows it) for the questions, then normal
   > mode.

   ```text
   ISSUE-DRAFT.md is a forwarded request for pre-orders in the Bookstore API. Don't write the issue
   yet. Read it with src/Bookstore.Domain/Book.cs and src/Bookstore.Api/Endpoints/BookEndpoints.cs,
   then list, numbered, every decision a product owner must make before each rule can be tested,
   including any the draft doesn't mention (the error shape, which responses show the flag). Stop
   after the list.
   ```

   Answer as the product owner. These are the decisions the reference solution used; yours may differ:

   ```text
   Decisions: "today" is the UTC date from TimeProvider; published today is not a pre-order; moving a
   stocked book into the future is rejected with 400 on Stock, not auto-zeroed; no special pricing or
   coupons; every book response includes isPreOrder. Write the issue body: a user story, those
   definitions, and numbered Given/When/Then criteria with today fixed at 2026-10-16 and concrete
   prices, plus an out-of-scope list. Save it to docs/issues/pre-orders.md.
   ```

   **Check:** each criterion names one behavior with concrete values (a date such as 2026-12-01, a
   status code, the `Stock` field), and there are criteria for the update path, the published-today
   boundary, and quoting a pre-order.

   **If it doesn't:** reply "Criterion N says 'a future date'. Rewrite it with an exact date and the
   exact status code, so one test can check it."

   </details>

4. **Create it on GitHub.** Have the agent create the issue in your repository with the `enhancement`
   and `ai-assisted` labels. Check it in the browser, and record its number in
   `docs/issues/pre-orders.md`.

   <details>
   <summary>Guided help: step 4</summary>

   Agent mode in either tool. The write must stop for your approval.

   ```text
   Open a GitHub issue in <owner>/<repo> with the title and body from docs/issues/pre-orders.md and
   the labels enhancement and ai-assisted. Before calling issue_write, show me exactly what you'll
   send. After it's created, read it back with issue_read and put its number in the table at the top
   of the file.
   ```

   Approve the `issue_write` call only after you've read the title, body, and labels. Then look at it:

   ```powershell
   gh issue list --label ai-assisted
   # One open issue: Pre-order books can be quoted but not stocked (#1 in a new repository)

   gh issue view <number> --web
   ```

   **Check:** the criteria render as a numbered list, both labels are on the issue, and its number
   matches the table in `docs/issues/pre-orders.md`.

   **If it doesn't:** if a label is missing, `gh label list` shows whether it exists; create it and ask
   the agent to add it. If the write ran without asking, fix your step 2 rules before you go on.
   Offline: skip the creation and leave `#<issue-number>` in the table.

   </details>

5. **Add review instructions on `main`.** Write `.github/copilot-instructions.md` (what a reviewer must
   always check in this codebase, and what not to comment on) and one path-scoped
   `.github/instructions/*.instructions.md` file with an `applyTo` pattern for the domain project.
   Commit and push them to `main` **before** you branch, so the feature branch and its pull request
   start with them.

   <details>
   <summary>Guided help: step 5</summary>

   Agent mode. The prompt names this codebase's rules, so the files describe the Bookstore rather than
   C# in general:

   ```text
   Write two review instruction files for this repository. .github/copilot-instructions.md: what a
   reviewer must always check here (acceptance criteria have tests, decimal money rounded
   AwayFromZero, no DateTime.Now/Today/UtcNow in application code, rules shared by create and update,
   the CancellationToken on every EF Core call, new ! operators, weakened tests, new packages,
   secrets) and what not to comment on (formatting; controllers, AutoMapper, MediatR, Newtonsoft).
   .github/instructions/domain.instructions.md with applyTo "src/Bookstore.Domain/**/*.cs": no
   framework dependencies, DateOnly dates passed in, decimal money, guard clauses, XML docs that state
   edge cases. Keep every bullet short and checkable.
   ```

   Read both files and cut anything vague, then commit and push (this also pushes your step 2 commit):

   ```powershell
   git add .github docs
   git commit -m "Add review instructions and the pre-order issue (#<number>)"
   git push
   ```

   **Check:** `domain.instructions.md` opens with front matter holding
   `applyTo: "src/Bookstore.Domain/**/*.cs"`, and `git log --oneline origin/main -1` shows your commit.

   **If it doesn't:** a path file without `applyTo` is ignored; ask the agent to add the front matter.

   </details>

6. **Implement on a linked branch.** Create a branch from the issue so GitHub links them (for example
   with `gh issue develop`), then plan first and implement. Every acceptance criterion gets a test, date
   rules are tested with a fixed clock, and commit messages end with `(#<number>)`.

   <details>
   <summary>Guided help: step 6</summary>

   Create the branch from the issue (offline: `git switch -c pre-orders`):

   ```powershell
   gh issue develop <number> --name <number>-pre-orders --checkout
   # Creates the branch on GitHub, linked to the issue, and switches to it
   ```

   > **GitHub Copilot:** the Plan agent (VS Code, VS 2026) for the plan, then Agent mode.
   >
   > **Claude Code:** plan mode for the plan; accept it to start implementing.

   ```text
   Issue #<number> is specified in docs/issues/pre-orders.md. Propose an implementation plan, no
   edits: the pre-order rule in Bookstore.Domain taking today as a DateOnly; one stock rule in
   ValidateAsync used by both create and update; IsPreOrder on BookDto in every book response;
   TimeProvider registered in Program.cs and injected into the handlers; tests with a clock fixed at
   2026-10-16, one per criterion, mapped by number.
   ```

   **Check the plan:** the rule compares with `>`, not `>=` (published today isn't a pre-order); the
   stock check sits in `ValidateAsync`, which create and update share; the tests replace
   `TimeProvider` with a fixed clock. Then approve:

   ```text
   The plan is approved. Implement it on the current branch, then run dotnet build and
   dotnet test --solution Bookstore.slnx and fix what fails until the build has 0 warnings and every
   test passes. Make one commit whose message ends with "(#<number>)". Don't push.
   ```

   **Check:** the tests pass (the reference has 28 in total), and `git log --oneline -1` ends with
   `(#<number>)`. **If a date test could depend on the day it runs:** search `src/` for `DateTime.`
   and ask the agent to use the injected `TimeProvider` instead.

   </details>

7. **Open the pull request through MCP.** Have the agent write the description to `PR-DESCRIPTION.md`
   (how AI was used, and `Fixes #<number>`), push the branch, and open a pull request to `main` with that
   description. Check the issue's **Development** panel.

   <details>
   <summary>Guided help: step 7</summary>

   Agent mode. Approve `git push` and `create_pull_request` only after you've read what they'll send.

   ```text
   Write PR-DESCRIPTION.md for this branch: a title, "Fixes #<number>" on its own line, what changed,
   how to verify, and an "AI assistance" section that says which steps the agent did. Commit it with
   "(#<number>)" at the end of the message, push the branch, then open a pull request to main in
   <owner>/<repo> through the GitHub MCP server with that file as the body. Show me the title and
   body before you create it.
   ```

   ```powershell
   gh pr view --web
   # Opens the pull request; follow the Fixes link to the issue
   ```

   **Check:** the issue's **Development** panel lists your branch and the pull request, and the commits
   ending `(#<number>)` appear in the issue's timeline.

   **If it doesn't:** the link comes from the closing keyword in the description, and the pull request
   must target `main`. Ask the agent to fix the body with `update_pull_request`, then refresh the issue.

   </details>

8. **Review it.** If Copilot code review is available to you, request Copilot as a reviewer and act on
   every comment (apply, fix differently, or resolve with a reason), then request it again after your
   fixes. Otherwise, run the fallback: a **fresh** agent session that reviews `git diff main...HEAD`
   against your instruction files and the issue. Record the findings and what you did about each in
   `PR-DESCRIPTION.md`.

   <details>
   <summary>Guided help: step 8</summary>

   With Copilot code review, select **Copilot** under **Reviewers** on the pull request, or ask the
   agent (this needs the `copilot` toolset):

   ```text
   Request a Copilot code review on pull request #<pr-number> in <owner>/<repo>, then stop. I'll read
   the comments myself.
   ```

   Without it, open a fresh session (a new chat, or `/clear` in Claude Code) and ask:

   ```text
   You didn't write this branch; review it as a strict reviewer. Read docs/issues/pre-orders.md,
   .github/copilot-instructions.md, and .github/instructions/domain.instructions.md, then git diff
   main...HEAD. Go criterion by criterion: name the test that covers each one, or say that none does.
   Then list rule violations. Give file and line for every finding. Read only: don't edit files.
   ```

   **Check:** every finding has a file and line, and each one lands in the table in `PR-DESCRIPTION.md`
   with what you did about it. Push your fixes, then request Copilot again.

   **If the review finds nothing:** no comments isn't approval. Change `>` to `>=` in the stock rule in
   `ValidateAsync` and rerun the tests: only `A_book_published_today_is_not_a_pre_order_and_can_be_stocked`
   should fail. Undo it, and note the check in your table.

   </details>

<details>
<summary>Prompting hints</summary>

- For step 2, look at how the demo's solution expresses each requirement: the toolsets in a header, the
  token from an environment variable or a sign-in, and approval rules keyed by tool name. Your tool's
  file format differs; your instructor can confirm which file your tool reads.
- Also for step 2: list the tools your toolsets actually load before you write the approval rules.
  Write tools don't share a naming pattern, and some of them commit files to a branch without your local
  build. Naming toolsets replaces the defaults, so check that the tool that says who you are is still
  there.
- For step 3, ask for questions first and say explicitly that the model should wait. Give your answers as
  decisions, not suggestions.
- Include the boundary cases in your decisions: is a book published *today* a pre-order? What happens to
  a stocked book whose date moves into the future? Is there special pricing?
- For step 4, name the repository and the labels, and ask to see the issue before it's created.
- For step 6, point the agent at the issue file and the existing endpoint and test patterns, and say
  where "today" should come from so tests can control it.
- For step 7, name the repository and the target branch, say where the closing keyword goes, and ask the
  agent to show the pull request fields before it creates anything.
- For step 8, the reviewer session should not be the session that wrote the code. Ask for findings with
  file and line, and no edits.
</details>

## Requirements

- MCP configuration committed for your tool, loading only the `context`, `issues`, `pull_requests`, and
  `repos` toolsets (plus `copilot` if you request Copilot's review through MCP), with no token or
  repository-specific value in the file.
- The write tools the task uses require approval (a permission rule, or your client's per-call
  confirmation), and `merge_pull_request`, `push_files`, `create_or_update_file`, `delete_file`,
  `create_repository`, `delete_repository`, `fork_repository`, `assign_copilot_to_issue`, and
  `create_pull_request_with_copilot` are denied or switched off.
- `docs/issues/pre-orders.md` has numbered Given/When/Then criteria with concrete values, including the
  "published today" boundary, the update path, and quoting a pre-order; the same issue exists on GitHub
  with the `ai-assisted` label (or, offline, is ready to create).
- `.github/copilot-instructions.md` and a path-scoped `.github/instructions/*.instructions.md` with
  `applyTo` are on `main` before the feature branch.
- Creating or updating a book with a future `PublishedOn` and `Stock` greater than 0 returns 400 with an
  error for `Stock`; pre-orders with no stock can be created and quoted at the normal price; book
  responses include `isPreOrder`.
- "Today" comes from an injected clock, and tests fix it; every criterion has a test.
- `dotnet build` reports 0 warnings and `dotnet test --solution Bookstore.slnx` passes.
- `PR-DESCRIPTION.md` contains `Fixes #<number>`, has an AI assistance section, and records the review
  findings and how you handled each; on GitHub, the issue's Development panel shows the branch and the
  pull request.

## Expected behavior

```powershell
dotnet build
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)

dotnet test --solution Bookstore.slnx
# Test run summary: Passed!
#   total: 19 plus your new tests, failed: 0

dotnet run --project src/Bookstore.Api
```

With the API running, from a second PowerShell 7 terminal (use a date after today, and a valid ISBN-13
that isn't in the catalog):

```powershell
$api = 'http://localhost:5080/api/books'
$book = @{ title = "Tomorrow's Catalog"; isbn = '978-1-23456-010-2'; price = 22.00; stock = 3
           genre = 'NonFiction'; publishedOn = '2099-03-01'; authorId = 4 }

(Invoke-WebRequest $api -Method Post -ContentType 'application/json' -Body ($book | ConvertTo-Json) -SkipHttpErrorCheck).StatusCode
# 400  (a pre-order can't have stock)

$book.stock = 0
$created = Invoke-RestMethod $api -Method Post -ContentType 'application/json' -Body ($book | ConvertTo-Json)
$created.isPreOrder
# True

$quote = @{ lines = @(@{ bookId = $created.id; quantity = 2 }) } | ConvertTo-Json -Depth 3
Invoke-RestMethod http://localhost:5080/api/quotes -Method Post -ContentType 'application/json' -Body $quote
# subtotal discount  tax total
# -------- --------  --- -----
#       44        0 2.64 46.64
```

On GitHub, the issue's **Development** panel lists the branch and the pull request, its timeline shows
the commits that end with `(#<number>)`, and merging the pull request into `main` closes the issue.

## Stretch goal

Add a second server entry, `github-readonly`, with the `X-MCP-Readonly: true` header for
refinement-only sessions, and compare its tool list with your main entry's. Then make Copilot code
review automatic: add a branch ruleset for `main` with **Automatically request Copilot code review**
and **Review new pushes**, push a small change to your pull request, and see whether Copilot reviews it
again without being asked. (Leave `solution/` as is; this is extra practice.)

## Reference

- Concepts: [Module 13: AI Across the Delivery Lifecycle with GitHub](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/),
  especially [Connecting Agents to GitHub](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/02-connecting-agents-to-github.md),
  [GitHub Issues and Projects with AI](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/03-github-issues-and-projects-with-ai.md),
  [Copilot Code Review on GitHub](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/04-copilot-code-review-on-github.md), and
  [Where AI Fits in the Delivery Lifecycle](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/01-where-ai-fits-in-the-lifecycle.md)
- Worked example: [matching demo](../../demos/22-github-mcp-and-issues/)
