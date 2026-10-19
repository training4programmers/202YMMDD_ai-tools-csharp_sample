@AGENTS.md

## Working in Claude Code

- For anything bigger than a one-file change, start in plan mode and wait for the plan to be approved.
- After each change, run `dotnet build` and `dotnet test --solution Bookstore.slnx` yourself and read the
  output before you report back.
- Finish with a short summary: files changed, tests added, and anything you were unsure about.
