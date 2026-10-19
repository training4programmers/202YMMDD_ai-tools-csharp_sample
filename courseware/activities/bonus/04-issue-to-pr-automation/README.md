# Bonus Activity 04: Issue to PR with a Cloud Agent

## Objective

Delegate a whole feature to a cloud agent and review what comes back. The shop manager's request in
`REQUEST.md` ("show the author's other books") is friendly but vague. Turn it into an **agent-ready
issue**, prepare your own GitHub repository so the GitHub Copilot cloud agent and the Claude Code GitHub
Action can work in it, hand the issue to one of them, and review the resulting pull request exactly as you
would a teammate's, asking for at least one change.

## Prerequisites

- .NET 10 SDK, Git, PowerShell 7, and the GitHub CLI (`gh`) signed in to your account
- A GitHub repository you own (a private one is fine) and **one** of:
  - a paid GitHub Copilot plan with the cloud agent enabled (on by default for Pro and Pro+; a Business or
    Enterprise administrator must enable it), or
  - Claude Code, plus an Anthropic API key or a Claude subscription for the GitHub Action
- Activity 12, and the chapter
  [Issue to Pull Request: Cloud Agents on GitHub](../../../docs/module-07-agentic-coding/07-issue-to-pr-automation.md)

## Skills Practiced

- Turning a vague request into an agent-ready issue: goal, contract, acceptance criteria, constraints,
  pointers
- Writing an issue form that keeps future tasks in that shape
- Preparing the cloud agent's environment with `copilot-setup-steps.yml`
- Configuring the Claude Code GitHub Action with `claude.yml`
- Reviewing an agent's pull request and iterating through review comments

## Instructions

Start the lab from the course repo root. This copies `begin/` to a folder outside the repo, makes it a
git repository, and commits a baseline, so `git diff` always shows exactly what you and your assistant
changed:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 04 -Bonus
cd ~\labs\bonus-activity-04-issue-to-pr-automation
```

Open the folder (or `Bookstore.slnx`) in Visual Studio 2026 or VS Code. To start over, re-run the
command with `-Force`. The reference in `solution/` is **one acceptable result**; yours will differ.

1. **Write the issue.** Read `REQUEST.md` and turn it into an issue in a file of your own. Decide every
   detail an agent would otherwise guess: the route and response shape, what "other books" includes and
   excludes, what "can sell today" means in the data, the parameter staff use to see everything, the order,
   the error statuses, the tests you expect, and the constraints. Check your contract against the seed
   data (`src/Bookstore.Api/Data/SeedData.cs`) and write at least two acceptance criteria with concrete
   expected results.

   <details>
   <summary>Guided help: step 1</summary>

   Start with the seed data: the books get ids in the order `SeedData.cs` adds them, two per author (1 and
   2 by Maya Okafor, 3 and 4 by Lars Henriksen, and so on), and book 4, "Northern Lights Field Guide", has
   0 copies. For a draft to edit, ask read-only (Ask mode, or Claude Code's plan mode):

   ```text
   Read REQUEST.md, src/Bookstore.Api/Endpoints/BookEndpoints.cs, Endpoints/AuthorEndpoints.cs,
   Contracts/BookContracts.cs, and Data/SeedData.cs. Don't change any files; reply with a draft GitHub
   issue in Markdown with the sections Goal, Contract, Acceptance criteria, Constraints and out of
   scope, and Pointers. In the contract, state the route, the response (reuse BookDto), that the book
   itself is never included, that "can sell today" means stock greater than 0, the name and default of
   the staff query parameter, the order (newest PublishedOn first, then title), and the status for an
   unknown book id. Write at least two acceptance criteria with the exact titles the seed data returns.
   List every decision you made that REQUEST.md didn't.
   ```

   Save the reply as `ISSUE.md` and edit it until every decision is one you'd defend.
   **Check:** with the seed data, book 1 returns only "Rivers of Glass"; book 3 returns an empty list, and
   "Northern Lights Field Guide" with the staff option; an unknown id returns `404`.
   **If your contract doesn't say what the staff option set explicitly to `false` does** (for example
   `?includeOutOfStock=false`), add it. The reference run's agent got exactly that case wrong, because the
   issue covered only the default and `true`.

   </details>

2. **Add an issue form.** Create `.github/ISSUE_TEMPLATE/agent-task.yml` with required fields for the parts
   of an agent-ready issue.

   <details>
   <summary>Guided help: step 2</summary>

   An issue form is YAML; GitHub offers it under **New issue** once it's on the default branch. Use any
   mode that can edit files:

   ```text
   Create .github/ISSUE_TEMPLATE/agent-task.yml, a GitHub issue form named "Agent-ready task" with a
   one-line description, the title prefix "[Task]: ", and the label agent-ready. Start with a short
   markdown block saying an agent can't ask what you meant. Then textareas: Goal, Contract (rendered as
   markdown), and Acceptance criteria, all required, the last pre-filled with a checklist that ends with
   the dotnet build and dotnet test --solution Bookstore.slnx gates; then optional Constraints and out of
   scope, and Pointers.
   ```

   Each required field should have this shape:

   ```yaml
     - type: textarea
       id: goal
       attributes:
         label: Goal
         description: What should change and why, in two or three sentences.
       validations:
         required: true
   ```

   **Check:** every field has its own `id`, and `validations` sits beside `attributes`, not inside it.

   **If GitHub later shows a blank issue instead of your form:** check the YAML's indentation first, then
   that the file is in `.github/ISSUE_TEMPLATE/` on the default branch.

   </details>

3. **Prepare the agents.** Add `.github/workflows/copilot-setup-steps.yml` (a single `copilot-setup-steps`
   job that installs the .NET 10 SDK and restores and builds `Bookstore.slnx`) and
   `.github/workflows/claude.yml` (responding to `@claude`, with limits on turns, time, and concurrency, and
   permission to run the `dotnet` gates). Commit.

   <details>
   <summary>Guided help: step 3</summary>

   Draft both workflows with your assistant (agent mode, or Claude Code's Manual mode (`default`)), then
   check them against the rules below.

   ```text
   Write .github/workflows/copilot-setup-steps.yml: one job named exactly copilot-setup-steps, on
   ubuntu-latest, timeout-minutes 20, permissions contents: read. Steps: actions/checkout@v7,
   actions/setup-dotnet@v6 with dotnet-version 10.0.x, then dotnet restore Bookstore.slnx and
   dotnet build Bookstore.slnx --no-restore. Triggers: workflow_dispatch, plus push and pull_request
   limited to changes to this file.
   ```

   ```text
   Write .github/workflows/claude.yml for anthropics/claude-code-action@v1 in interactive mode (no prompt
   input). Trigger on new issues, issue comments, and PR review comments, and skip the job unless the
   text contains @claude. Pass anthropic_api_key from the ANTHROPIC_API_KEY secret. Permissions:
   contents, pull-requests, and issues write; id-token write; actions read. timeout-minutes 30; a
   concurrency group per issue or PR number, with cancel-in-progress false; install .NET 10 before the
   action; claude_args with --max-turns 30 and --allowedTools for dotnet restore, build, test, and format.
   ```

   **Check:** the setup job is named `copilot-setup-steps` (Copilot ignores any other name), and
   `claude.yml` uses `@v1` and `claude_args`, not `@beta` or `direct_prompt`, which belong to the old
   version. Then commit:

   ```powershell
   git add .github ISSUE.md
   git commit -m "Add an agent-ready issue form and the agent workflows"
   ```

   **If the Claude step has a `prompt:` input:** remove it. A prompt switches the action to automation
   mode, so it runs on every trigger instead of waiting for `@claude`.

   </details>

4. **Push to your repository** and check that the setup workflow ran green in the **Actions** tab. For
   Claude, add the secret, or run `/install-github-app`.

   <details>
   <summary>Guided help: step 4</summary>

   Create a repository in your own account and push, in one step. A private repository is fine:

   ```powershell
   gh repo create bookstore-agent-tasks --private --source . --push
   gh label create agent-ready --description "Fully specified for a coding agent"

   gh run list --workflow copilot-setup-steps.yml --limit 1
   # one run of "Copilot Setup Steps"; wait until it shows completed and success
   ```

   If no run appears, start one: `gh workflow run copilot-setup-steps.yml`.

   Use the class organization instead only if your instructor set it up for this lab and, for the Claude
   path, an organization owner has installed the Claude GitHub App on it (members usually can't install
   apps on an organization). Then create `<class-org>/bookstore-agent-tasks-<your-handle>` with the same
   command, and ask whether the organization's `ANTHROPIC_API_KEY` secret already covers your repository.

   > **GitHub Copilot:** nothing more to set up; the cloud agent reads the setup workflow from `main`.
   >
   > **Claude Code:** setup needs admin access to the repository, which you have on your own. Run
   > `/install-github-app` in Claude Code from the lab folder, or install the
   > [Claude GitHub App](https://github.com/apps/claude) on the repository and set the secret yourself.
   > `gh secret set` asks for the value, so the key never lands in your command history:

   ```powershell
   gh secret set ANTHROPIC_API_KEY
   # prompts for the secret; paste the key (it isn't echoed)
   ```

   **Check:** the **Actions** tab shows "Copilot Setup Steps" with a green check.

   **If a private repository won't work for you** (for example, your account has no Actions minutes left
   for private repositories): fall back to a public repository in your personal account. Never put a
   secret in it; take the Copilot path in step 5. To make the one you created public, delete any secret
   first (`gh secret delete ANTHROPIC_API_KEY`), then run
   `gh repo edit --visibility public --accept-visibility-change-consequences`.

   </details>

5. **Delegate.** Create the issue on GitHub (paste your file, or use your new form), then assign it to
   Copilot or comment `@claude` with a short instruction. While it runs, note what you would have done
   differently if you were supervising live.

   <details>
   <summary>Guided help: step 5</summary>

   Create the issue from your file, or open **Issues** > **New issue** on GitHub and choose **Agent-ready
   task** to use your form:

   ```powershell
   gh issue create --title "Show an author's other books for a book" --body-file ISSUE.md `
       --label agent-ready
   # https://github.com/<you>/bookstore-agent-tasks/issues/1
   ```

   > **GitHub Copilot:** open the issue, choose **Copilot** under **Assignees**, and add the first prompt
   > below as extra instructions. It works on a `copilot/` branch and opens a draft pull request.
   >
   > **Claude Code:** comment on the issue with the second prompt. Follow the run with `gh run watch`;
   > Claude also keeps a progress comment on the issue up to date.

   ```text
   Stay inside this issue's contract: no changes to existing endpoints, DTOs, or tests, and no new
   packages. Before you finish, run dotnet build (no warnings) and dotnet test --solution Bookstore.slnx,
   and in the pull request description, tick each acceptance criterion with the test that proves it.
   ```

   ```text
   @claude Please implement this issue and open a pull request. Respect its constraints: no new
   packages, and no changes to existing endpoints, DTOs, or tests. Run dotnet build and dotnet test
   --solution Bookstore.slnx before you push, and map each acceptance criterion to its test in the
   pull request description.
   ```

   **Check:** within a few minutes there's a branch with commits, and a pull request (Claude may give you
   a link to create it; open it). While you wait, note where the agent looked, what it assumed, and where
   you'd have stepped in.

   **If nothing happens after an `@claude` comment:** open the run in the **Actions** tab; a missing
   secret or a missing Claude GitHub App is the usual cause.

   </details>

6. **Review the pull request.** Check every acceptance criterion, read the whole diff, check out the branch
   and run the gates locally, and request at least one specific, checkable change in a review comment.
   Merge only when you would accept the same pull request from a person.

   <details>
   <summary>Guided help: step 6</summary>

   Criteria first, then the whole diff, then run it:

   ```powershell
   gh pr checkout <number>
   git diff --stat main...HEAD
   # the books endpoint file, a new test file, the .http file; nothing else
   dotnet build
   # Build succeeded. 0 Warning(s)
   dotnet test --solution Bookstore.slnx
   # Test run summary: Passed!   (the starter has 19 tests; the reference solution has 25)
   dotnet run --project src/Bookstore.Api
   ```

   With the API running, send the new requests in `src/Bookstore.Api/Bookstore.Api.http`, plus the case
   your criteria may not cover:

   ```http
   GET http://localhost:5080/api/books/3/more-by-author?includeOutOfStock=false
   # expect [] (an empty list)
   ```

   Then ask for one specific, checkable change in a pull request comment that starts with `@copilot` or
   `@claude`, for example:

   ```text
   @copilot ?includeOutOfStock=false returns out-of-stock books: only a missing parameter is treated as
   false. Treat anything other than true as false, and add a test that book 3 with
   includeOutOfStock=false returns an empty list.
   ```

   **Check:** the agent pushes a new commit to the same branch, and after `git pull` the tests still pass.
   When you'd accept it from a person, mark a draft ready (`gh pr ready <number>`) and merge it.

   **If the diff changes existing tests or adds a package:** ask for that to be reverted; the issue ruled
   both out.

   </details>

7. **Reflect.** Which lines of your issue did the agent rely on, and what did it get wrong because the issue
   didn't say?

   <details>
   <summary>Guided help: step 7</summary>

   Use the pull request as evidence. Ask read-only (Ask mode, or Claude Code's plan mode):

   ```text
   Compare ISSUE.md with what pull request <number> changed (gh pr diff <number>) and with its
   description. Don't change anything. For each line of the Contract and the Acceptance criteria, say
   whether the change implements it and where. Then list every decision the change made that the issue
   didn't specify: names, defaults, ordering, status codes, and files touched.
   ```

   **Check:** you can name at least one line of your issue the agent clearly relied on (a route, a
   parameter name, an example title) and one gap it filled with a guess.

   **If the agent got nothing wrong:** look at what you asked for in review; that request is the line
   your issue was missing. Add it to the checklist your issue form pre-fills.

   </details>

<details>
<summary>Prompting hints</summary>

- Write the issue for a reader with no context: name the route, the parameter and its default, the order,
  and the response type (reusing the existing `BookDto` saves the agent a decision).
- Turn "only ones we can actually sell" into a data rule, and give the staff option an explicit name.
- Include expected results that the seed data makes checkable, such as which titles come back for a given
  book with and without the staff option.
- Point at the existing endpoints that are closest to what you want, and list what must not change.
- In review comments, ask for one change at a time and say how you will check it.

</details>

## Requirements

- Your issue has a goal, a contract with every status code, at least two concrete acceptance criteria, its
  constraints, and pointers to existing code.
- `.github/ISSUE_TEMPLATE/agent-task.yml` exists with required fields for the issue parts.
- `copilot-setup-steps.yml` has one job named `copilot-setup-steps`, installs .NET 10, and ran green on
  GitHub.
- `claude.yml` responds to `@claude` and limits turns, run time, and concurrency.
- A cloud agent opened a pull request for your issue, and you reviewed it with at least one change request.
- The merged feature meets your acceptance criteria; `dotnet build` has no warnings and
  `dotnet test --solution Bookstore.slnx` passes.

## Expected behavior

For an issue like the one in `solution/ISSUE.md` (yours may differ):

```http
### Book 1 ("The Quiet Compiler"): its author's other in-stock book, "Rivers of Glass"
GET http://localhost:5080/api/books/1/more-by-author

### Book 3: an empty list, because the author's other book has no copies
GET http://localhost:5080/api/books/3/more-by-author

### Book 3 with the staff option: "Northern Lights Field Guide"
GET http://localhost:5080/api/books/3/more-by-author?includeOutOfStock=true

### Unknown book: 404
GET http://localhost:5080/api/books/9999/more-by-author
```

```powershell
gh pr checkout <number>
dotnet build                          # Build succeeded. 0 Warning(s)
dotnet test --solution Bookstore.slnx # all tests pass, including the new endpoint's tests
```

## Stretch goal

Add a ruleset to your repository that requires a pull request, one approving review, and a passing
build-and-test check on the default branch, and add a `CODEOWNERS` entry so changes to `.github/workflows/`
need your review. Then ask the agent for a small follow-up change and watch the rules apply to its pull
request. (Leave `solution/` as is; this is extra practice.)

## Reference

- Concepts: [Module 07: Agentic Coding](../../../docs/module-07-agentic-coding/), especially
  [Issue to Pull Request: Cloud Agents on GitHub](../../../docs/module-07-agentic-coding/07-issue-to-pr-automation.md)
- Worked example: [matching demo](../../../demos/bonus/04-issue-to-pr-automation/)
