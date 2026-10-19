# Bonus Activity 04: Solution Notes

> This is **one acceptable result**, produced with an AI assistant and then reviewed by a person. Your
> prompts, your assistant's output, and your final code will differ. That's expected, as long as you
> meet the activity's Requirements.

## How this result was produced

- `ISSUE.md` written by hand from `REQUEST.md`, then checked against `SeedData.cs`.
- The workflows and the issue form were drafted with Claude Code (accept-edits mode) and reviewed against
  the GitHub and Claude Code documentation.
- The feature itself was delegated: the issue was assigned to the GitHub Copilot cloud agent, which opened
  a draft pull request after one session; one review round with `@copilot` followed. The same issue was
  also run through the Claude Code GitHub Action with `@claude` to compare; both results were similar.

## Prompts used

```text
Write .github/workflows/copilot-setup-steps.yml for this repo: one job named copilot-setup-steps on
ubuntu-latest, contents: read, checkout, actions/setup-dotnet with 10.0.x, then dotnet restore and build
Bookstore.slnx. Trigger it on workflow_dispatch and on push and pull_request to its own path.
```

```text
Write .github/workflows/claude.yml for the Claude Code GitHub Action (anthropics/claude-code-action@v1) in
interactive mode: respond to @claude in new issues, issue comments, and PR review comments. Use the
ANTHROPIC_API_KEY secret, the permissions the action needs, a 30-minute timeout, one run at a time per
issue or PR, --max-turns 30, and allow it to run dotnet restore, build, test, and format.
```

```text
Write .github/ISSUE_TEMPLATE/agent-task.yml: an issue form with required Goal, Contract, and Acceptance
criteria fields, plus optional Constraints and Pointers, and the label agent-ready.
```

Cloud agent note added when assigning the issue to Copilot:

```text
Follow AGENTS.md. Run dotnet build and dotnet test --solution Bookstore.slnx before finishing, and list
how each acceptance criterion is met in the pull request description.
```

Review comment on the pull request:

```text
@copilot The out-of-stock filter only runs when includeOutOfStock is omitted; ?includeOutOfStock=false
returns out-of-stock books. Treat anything other than true as false, and add a test that book 3 with
includeOutOfStock=false returns an empty list.
```

## What review caught

- **The `false` case.** The agent's first version checked `includeOutOfStock is null` instead of
  `!= true`, so an explicit `false` behaved like `true`. The acceptance criteria covered the default and
  `true`, not an explicit `false`, which is why it slipped through; the review comment added the case.
- **The book itself in the results.** An early commit listed the requested book too; the agent's own
  "never includes the book itself" test (from the issue's contract) caught it before the pull request was
  ready.
- **A first draft of `claude.yml`** used the `@beta` action and `direct_prompt`, which belong to the old
  version. Replaced with `@v1`, interactive mode (no `prompt`), and `claude_args`.
- **`copilot-setup-steps.yml` job name.** The drafted job was called `setup`; Copilot only picks up a job
  named `copilot-setup-steps`.

## Edited by hand

- `ISSUE.md`: the "Pointers" section, after noticing that `AuthorEndpoints.GetAuthorBooksAsync` already
  lists an author's books.

## Verify

```powershell
dotnet build
dotnet test --solution Bookstore.slnx
```
