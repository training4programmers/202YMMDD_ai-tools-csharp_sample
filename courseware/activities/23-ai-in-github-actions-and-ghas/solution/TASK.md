# Task: put an agent in the Bookstore workflow

The Bookstore team wants every pull request to get a short, AI-written **risk review** before a person
reviews it, and every failed build to get a first-pass **triage**. Both must be advisory: they never
block a merge and never change code. The deterministic gates come first.

Start from `.github/workflows/ci.yml` (build and test only) and turn it into a gated GitHub Actions
workflow.

## Gates (these block merges, through a ruleset on `main`)

1. **`build` job:** install the .NET 10 SDK; restore, using `--locked-mode` when any
   `packages.lock.json` exists; build in Release with warnings treated as errors; run
   `dotnet test --solution Bookstore.slnx`; check formatting with `dotnet format --verify-no-changes`.
   Upload the build and test output as an artifact even when a step fails.
2. **`codeql` job:** GitHub Advanced Security code scanning: CodeQL for C#, without a build step.
3. **`dependency-review` job,** on pull requests: fail when the change adds a package with a known
   vulnerability.

## Advisory AI jobs (never block)

4. **Pull request risk review**, on pull requests only:
   - Skip it when the pull request changes nothing under `src/`.
   - The agent reads the diff, plus the pull request's title and description, and reports risks to
     pricing and stock rules (`PriceCalculator`, `Book.RemoveStock`, money as `decimal`, rounding),
     missing or weakened tests, and new packages.
   - Post the result as a pull request comment.
   - Support **two agents**, chosen by the `AI_AGENT` repository variable: `claude` (Claude Code,
     `claude -p`, the default when the variable isn't set) or `copilot` (GitHub Copilot CLI,
     `copilot -p`).
5. **Failure triage**, when the `build` job fails: the agent that `AI_AGENT` picks reads the end of the
   build and test logs and suggests a likely cause and a next step. Upload it as an artifact; on pull
   requests, also comment on the pull request, even though the build failed.

## Constraints

- No secret values in the repository. Claude Code's `ANTHROPIC_API_KEY` comes from a repository or
  organization secret. Copilot CLI uses the run's own `GITHUB_TOKEN`, allowed to make Copilot requests
  by the `copilot-requests: write` permission.
- The workflow's top-level `permissions` is `contents: read`. A write scope goes only on the job that
  needs it.
- Each secret is mapped into the one step that needs it (a step-level `env:`), never at job or workflow
  level. The step that posts comments is not the step that runs the agent.
- Treat pull request content as untrusted: no `${{ github.event.* }}` or `${{ github.head_ref }}` inside a
  `run:` script (pass values through `env:`), no `pull_request_target` trigger, the agent gets the fewest
  tools that work, and it doesn't load repository instruction files.
- Cost controls on every agent run: a model tier choice, a turn or time limit, a spend cap where the
  tool has one, and an input size cap.
- `tools/check-workflow.cs` reports no findings for the workflow.
- No changes to the application code or tests.

## Given

- `scripts/post-pr-comment.ps1` posts a Markdown file as a pull request comment with `gh pr comment`
  (or updates its earlier comment), using the `GH_TOKEN` you map into its step. Run it with `-WhatIf`
  to see what it would post without calling GitHub.
- `tools/check-workflow.cs` checks the workflow offline:
  `dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml`.
