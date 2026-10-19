# Rename a palette without creating duplicate names

> Copy of the GitHub issue the agent created through the GitHub MCP server, kept in the repo so the
> acceptance criteria travel with the code. The issue on GitHub is the source of truth; if the two
> disagree, update this file in the same pull request that changes the behavior.

| Field | Value |
|-------|-------|
| Issue | `#<issue-number>` (assigned by GitHub) |
| Title | Rename a palette without creating duplicate names |
| Labels | `enhancement`, `api`, `ai-assisted` |
| Branch | `<issue-number>-palette-rename`, created from the issue with `gh issue develop` |
| Source | `ISSUE-DRAFT.md` (stand-up notes), refined with an AI assistant, reviewed by the PO |

## Story

As a designer using the ToolsApp API, I want to rename a palette, so that palette names keep
describing their colors as the collection grows, without two palettes ending up with names that differ
only by capitalization.

## Acceptance criteria

1. **Rename succeeds.**
   Given the palette "Ocean" exists with 3 colors,
   when I send `PATCH /api/palettes/{id}` with `{ "name": "Ocean Blues" }`,
   then the response is `200 OK` with `{ "id": {id}, "name": "Ocean Blues", "colorCount": 3 }`,
   and `GET /api/palettes` lists "Ocean Blues" and no longer lists "Ocean".

2. **Surrounding whitespace is ignored.**
   Given the palette "Ocean" exists,
   when I rename it to `"  Deep Ocean  "`,
   then the stored name is `"Deep Ocean"`.

3. **Duplicate names are rejected, ignoring case.**
   Given the palettes "Ocean" and "Sunset" exist,
   when I rename "Ocean" to `"Sunset"` or `"sunset"`,
   then the response is `409 Conflict` with a problem details body,
   and "Ocean" keeps its name.

4. **A palette may change the casing of its own name.**
   Given the palette "Ocean" exists,
   when I rename it to `"OCEAN"`,
   then the response is `200 OK` and the name is `"OCEAN"`.

5. **Unknown palettes are reported.**
   When I rename a palette id that doesn't exist,
   then the response is `404 Not Found`.

6. **Names are validated.**
   When the new name is empty, only whitespace, or longer than 60 characters,
   then the response is `400 Bad Request` with a validation problem for `Name`.

## Notes and decisions

- **Duplicate means case-insensitive, after trimming.** Inner spaces are significant: "Ocean Blues"
  and "OceanBlues" are different names.
- **The database enforces it too.** The palette name column uses SQLite's `NOCASE` collation, so the
  existing unique index rejects case-only duplicates even if two requests race.
- **Status codes:** 409 for a name that's taken (the request is valid, the state conflicts), 400 for a
  malformed name.
- **Out of scope:** renaming colors, a UI, and an audit trail of old names.

## Definition of done

- Every acceptance criterion has an automated test that fails if the rule is removed.
- `dotnet build` has no warnings; `dotnet test --solution ToolsApp.slnx` passes.
- The pull request says `Fixes #<issue-number>` in its description and has a human approval.
