# Pull request: Rename palettes without duplicate names

> The reference outcome for Demo 22: the pull request the agent opens through the GitHub MCP server,
> plus a review log. The review findings are *illustrative*: they're typical of what Copilot code
> review or the agent-review fallback flags on a first attempt at this story, and your run will differ.
> Issue numbers are placeholders: replace `<issue-number>` with yours.

## Title

```text
Add PATCH /api/palettes/{id} to rename palettes (case-insensitive unique names)
```

## Description

Fixes #<issue-number>

Renames a palette without creating duplicate names. The acceptance criteria are copied in
`docs/issues/palette-rename.md`.

**What changed**

- `PATCH /api/palettes/{id}` with `{ "name": "..." }` renames a palette and returns `200` with the
  `PaletteDto`, `404` for an unknown id, `409` (problem details) when another palette already has the
  name, ignoring case, and `400` for an empty or over-long name.
- The palette `Name` column now uses SQLite's `NOCASE` collation, so the existing unique index also
  rejects case-only duplicates. Comparisons in the handler use the column collation; no `ToLower()`.
- `PaletteRenameRequest` contract; `.http` samples; 10 new tests in `PaletteRenameApiTests`, one or
  more per acceptance criterion.

**How to verify**

```powershell
dotnet build
dotnet test --solution ToolsApp.slnx
```

**AI assistance:** the issue was refined and created by an agent through the GitHub MCP server, with
the product owner's decisions; the implementation is an agent's (Claude Code, plan first, then edits),
reviewed by a person. The review log is below.

## How this pull request is linked

- **Branch:** `<issue-number>-palette-rename`, created from the issue with `gh issue develop`, so it
  appears in the issue's **Development** panel before any code exists.
- **Commits:** the message ends with `(#<issue-number>)`, so each commit appears in the issue's
  timeline.
- **Pull request:** `Fixes #<issue-number>` in this description links the pull request in the issue's
  **Development** panel, and merging it into `main` (the default branch) closes the issue.

## Review log

Illustrative findings on the first push. Copilot code review or, where it isn't available, an agent
review of `git diff main...HEAD` against the same instruction files typically catches the first three;
the last two are the kind a person catches.

| # | Finding | Source | Resolution |
|---|---------|--------|------------|
| 1 | The duplicate check used `p.Name.ToLower() == name.ToLower()`, which works but bypasses the unique index, and the index itself was case-sensitive, so two racing requests could still create "Ocean" and "ocean". | Copilot review, path instructions | Added `UseCollation("NOCASE")` to the column; the check compares `p.Name == name`. |
| 2 | A duplicate returned `400` with a validation problem, but criterion 3 says `409`. | Copilot review, repository instructions | Returns `TypedResults.Problem(..., statusCode: 409)`; `.ProducesProblem(409)` added for OpenAPI. |
| 3 | No test for criterion 4 (changing the casing of a palette's own name). | Agent review | Added the test; it failed until the duplicate check excluded the palette being renamed. |
| 4 | Names weren't trimmed, so `"  Sunset "` slipped past the duplicate check. | Human review | `request.Name.Trim()` before the check and the save; covered by two tests. |
| 5 | `catch (DbUpdateException)` caught every update failure as a conflict. | Human review | Narrowed to `SqliteException` with error code 19 (constraint), with a comment on why it exists. |

The race path (finding 5) isn't covered by an automated test; it depends on the unique index, which
the case-insensitive 409 tests exercise indirectly.

**Approvals:** one human reviewer other than the author (GitHub doesn't let you approve your own pull
request). Copilot's review is a comment, not an approval, and a person merges.
