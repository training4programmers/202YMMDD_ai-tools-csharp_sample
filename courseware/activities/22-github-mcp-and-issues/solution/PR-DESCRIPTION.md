# Pull request: Pre-orders can be quoted but not stocked

> The reference pull request description, as opened through the GitHub MCP server. Replace
> `<issue-number>` with the number GitHub gave your issue.

## Title

```text
Pre-orders: books published after today can be quoted but not stocked
```

## Description

Fixes #<issue-number>

Acceptance criteria: `docs/issues/pre-orders.md`.

**What changed**

- `Book.IsPreOrderOn(DateOnly today)` in the domain: published after today means pre-order; published
  today does not.
- Create and update share one validation rule: a pre-order can't have `Stock > 0` (400, error on
  `Stock`). Moving a stocked book's date into the future is rejected the same way.
- `BookDto` gains `IsPreOrder`, computed for today in every book response (list, single, and an
  author's books).
- "Today" is the UTC date from an injected `TimeProvider` (`TimeProvider.System` in `Program.cs`).
  Tests replace it with a fixed clock.
- Quotes are unchanged: they never depended on stock, and criterion 4 now has a test that proves it.
- Tests: 3 domain tests (`PreOrderTests`) and 6 API tests (`PreOrderApiTests`), one or more per
  criterion, all against a clock fixed at 2026-10-16.

**Verify**

```powershell
dotnet build
dotnet test --solution Bookstore.slnx
```

**AI assistance:** the issue was refined and created with an agent through the GitHub MCP server;
implementation by an agent from a reviewed plan; reviewed by a person. See the review notes below.

## Links

- The branch `<issue-number>-pre-orders` was created from the issue with `gh issue develop`, and
  `Fixes #<issue-number>` in this description adds the pull request to the issue's **Development**
  panel. Merging into `main` closes the issue.
- Each commit message ends with `(#<issue-number>)`, so the commits appear in the issue's timeline.

## Review notes

Illustrative: the defects this story's review rubric targets. Your review will find its own.

| # | Finding | Source | Resolution |
|---|---------|--------|------------|
| 1 | The first attempt used `DateTime.Today` inside the endpoint, so the date tests depended on the day they ran. | Copilot review, repository instructions | Injected `TimeProvider`; tests use `FixedTimeProvider`. |
| 2 | The stock rule was added to create only; update could still move a stocked book into the future. | Copilot review, repository instructions | Moved the rule into the shared `ValidateAsync`; added the update test (criterion 3). |
| 3 | `IsPreOrder` used `>=`, which made a book published today a pre-order. | Fallback review, criterion 5 | Changed to `>` to match criterion 5; the boundary test fails if it regresses. |

**Approvals:** one human reviewer other than the author. Copilot's review is a comment, not an
approval, and a person merges.
