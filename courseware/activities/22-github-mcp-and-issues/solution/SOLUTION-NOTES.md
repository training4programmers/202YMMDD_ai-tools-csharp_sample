# Activity 22: Solution Notes

> This is **one acceptable result**, produced with an AI assistant and then reviewed by a person. Your
> prompts, your assistant's output, and your final code will differ. That's expected, as long as you
> meet the activity's Requirements.

## How this result was produced

- **Tools and modes:** an agent session (Claude Code) for the issue, the code, the tests, and the pull
  request, with the remote GitHub MCP server configured as in `.mcp.json`: the
  `https://api.githubcopilot.com/mcp/` endpoint, the toolsets `context`, `issues`, `pull_requests`,
  `repos`, and `copilot` in the `X-MCP-Toolsets` header, and a fine-grained personal access token read
  from the `GITHUB_PAT` user environment variable (created in step 2: the class organization as
  resource owner, all repositories, Contents, Issues, and Pull requests read and write, 7-day expiry).
  `.vscode/mcp.json` holds the same server for GitHub Copilot agent mode in VS Code or Visual Studio
  2026, which sign in with the GitHub account instead of a token.
- **Approvals:** `.claude/settings.json` lets the read tools run, makes the write tools the task uses
  (issues, comments, the branch, the pull request, reviews) and `git push` ask first, and denies the
  ones it never needs: `merge_pull_request`, the three tools that commit files through the API
  (`push_files`, `create_or_update_file`, `delete_file`, which would skip the local build and tests),
  the repository-level `create_repository`, `delete_repository`, and `fork_repository`, and
  `assign_copilot_to_issue` and `create_pull_request_with_copilot` (the cloud agent isn't part of this
  task). The `copilot` toolset is there only for `request_copilot_review`. In Copilot, the per-call
  confirmation does the same job, with the denied tools turned off in the tool picker.
- **Model tier:** a frontier tier for refining the issue and planning; the same session implemented
  the plan.
- **Iterations:** two refinement rounds on the issue, one plan, one implementation pass, and one round
  of fixes after review.
- **GitHub artifacts:** the issue, branch, and pull request live in the class organization's
  repository, so they can't ship in this folder. `docs/issues/pre-orders.md` and `PR-DESCRIPTION.md`
  are their copies, with `<issue-number>` placeholders. The branch came from `gh issue develop`, so the
  issue's Development panel listed it before any code existed.
- **Copilot code review:** requested through MCP (prompt 9). Where Copilot code review isn't
  available, the fallback is a second, fresh agent session that reviews `git diff main...HEAD` against
  the two instruction files and the issue (prompt 10).

## Prompts used

```text
1. Check the GitHub MCP connection before we use it for real. Call get_me, list the open issues in
   <owner>/<repo>, and read ISSUE-DRAFT.md from main with get_file_contents. Read only: if any step
   would need a write tool, stop and tell me which one.

2. ISSUE-DRAFT.md is a forwarded request for pre-orders in the Bookstore API. Don't write the issue
   yet. Read it with src/Bookstore.Domain/Book.cs and src/Bookstore.Api/Endpoints/BookEndpoints.cs,
   then list, numbered, every decision a product owner must make before each rule can be tested,
   including any the draft doesn't mention (the error shape, which responses show the flag). Stop
   after the list.

3. Decisions: "today" is the UTC date from TimeProvider; published today is not a pre-order; moving a
   stocked book into the future is rejected with 400 on Stock, not auto-zeroed; no special pricing or
   coupons; every book response includes isPreOrder. Write the issue body: a user story, those
   definitions, and numbered Given/When/Then criteria with today fixed at 2026-10-16 and concrete
   prices, plus an out-of-scope list. Save it to docs/issues/pre-orders.md.

4. Open a GitHub issue in <owner>/<repo> with the title and body from docs/issues/pre-orders.md and
   the labels enhancement and ai-assisted. Before calling issue_write, show me exactly what you'll
   send. After it's created, read it back with issue_read and put its number in the table at the top
   of the file.

5. Write two review instruction files for this repository. .github/copilot-instructions.md: what a
   reviewer must always check here (acceptance criteria have tests, decimal money rounded
   AwayFromZero, no DateTime.Now/Today/UtcNow in application code, rules shared by create and update,
   the CancellationToken on every EF Core call, new ! operators, weakened tests, new packages,
   secrets) and what not to comment on (formatting; controllers, AutoMapper, MediatR, Newtonsoft).
   .github/instructions/domain.instructions.md with applyTo "src/Bookstore.Domain/**/*.cs": no
   framework dependencies, DateOnly dates passed in, decimal money, guard clauses, XML docs that state
   edge cases. Keep every bullet short and checkable.

6. Issue #<number> is specified in docs/issues/pre-orders.md. Propose an implementation plan, no
   edits: the pre-order rule in Bookstore.Domain taking today as a DateOnly; one stock rule in
   ValidateAsync used by both create and update; IsPreOrder on BookDto in every book response;
   TimeProvider registered in Program.cs and injected into the handlers; tests with a clock fixed at
   2026-10-16, one per criterion, mapped by number.

7. The plan is approved. Implement it on the current branch, then run dotnet build and
   dotnet test --solution Bookstore.slnx and fix what fails until the build has 0 warnings and every
   test passes. Make one commit whose message ends with "(#<number>)". Don't push.

8. Write PR-DESCRIPTION.md for this branch: a title, "Fixes #<number>" on its own line, what changed,
   how to verify, and an "AI assistance" section that says which steps the agent did. Commit it with
   "(#<number>)" at the end of the message, push the branch, then open a pull request to main in
   <owner>/<repo> through the GitHub MCP server with that file as the body. Show me the title and
   body before you create it.

9. Request a Copilot code review on pull request #<pr-number> in <owner>/<repo>, then stop. I'll read
   the comments myself.

10. (fresh session) You didn't write this branch; review it as a strict reviewer. Read
    docs/issues/pre-orders.md, .github/copilot-instructions.md, and
    .github/instructions/domain.instructions.md, then git diff main...HEAD. Go criterion by
    criterion: name the test that covers each one, or say that none does. Then list rule violations.
    Give file and line for every finding. Read only: don't edit files.
```

## What review caught

The review rubric (the two instruction files plus the acceptance criteria) is aimed at three defects
that are easy for an agent to produce on this story. Each one has a test in the solution that fails if
it comes back:

- **The machine clock.** Using `DateTime.Today` or `DateTime.UtcNow` in the endpoint makes the date
  tests depend on the day they run, and flip near midnight UTC. The repository instructions name this
  rule. The fix is an injected `TimeProvider`; tests use `FixedTimeProvider`.
- **A rule in one place.** Enforcing the stock rule on create only lets an update move a stocked book
  into the future (criterion 3). The rule lives in the shared `ValidateAsync`, and
  `Moving_a_stocked_book_into_the_future_is_rejected_and_changes_nothing` covers the update path.
- **An off-by-one at the boundary.** `PublishedOn >= today` makes a book published today a pre-order,
  contradicting criterion 5. Only the boundary test exposes it: changing `>` to `>=` in the
  validation fails `A_book_published_today_is_not_a_pre_order_and_can_be_stocked` and nothing else.
- **Checked and fine:** no new packages; quotes never depended on stock, so criterion 4 needed a test,
  not code; every EF Core call passes the `CancellationToken`; neither MCP file holds a token or a
  repository name.

## Edited by hand

- **The issue.** Two acceptance criteria were rewritten to use concrete dates instead of "a future
  date", so each maps to a single test. The edit went into `docs/issues/pre-orders.md` and the issue on
  GitHub, so the two still match.
- **The validation message.** The date was removed from the error text to avoid a culture-sensitive
  date format in an API message.
- **Test helper.** `IsbnFor` uses a different ISBN prefix from the seeded catalog, so new books can
  never collide with seed data.

## Verify

```powershell
dotnet build
dotnet test --solution Bookstore.slnx
# Build succeeded: 0 Warning(s), 0 Error(s)
# Test run summary: Passed!  total: 28 (19 existing + 3 domain + 6 API)
```
