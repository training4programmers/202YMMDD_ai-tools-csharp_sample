# Activity 23: Solution Notes

> This is **one acceptable result**, produced with an AI assistant and then reviewed by a person. Your
> prompts, your assistant's output, and your final code will differ. That's expected, as long as you
> meet the activity's Requirements.

## How this result was produced

- **Tool and mode:** an agent session (Claude Code) in plan mode for the job design, then normal mode
  for the edits. The same prompts work in GitHub Copilot agent mode.
- **Model tier:** a frontier tier, because workflow YAML has many easy-to-miss details (expression
  contexts, token scopes, step outputs, conditions that skip silently).
- **Iterations:** a plan, the gates, the two AI jobs, a self-review as an attacker (prompt 5), and fixes
  by hand.
- **Verification without GitHub:** `tools/check-workflow.cs` on the workflow; every `run:` block
  extracted and parsed with the PowerShell parser; the diff-collection step run against a scratch git
  repository (a docs-only change skips the review; a `PriceCalculator` change produces `pr.diff`, with a
  hostile title kept as plain data); and `scripts/post-pr-comment.ps1` dry-run with `-WhatIf`. Running
  the AI jobs for real needs a GitHub repository with the secret, Code Security, and the ruleset.

## Prompts used

```text
1. Read TASK.md and .github/workflows/ci.yml. Plan a GitHub Actions workflow for the Bookstore that
   meets every gate and constraint in TASK.md. For each job give its if: and needs:, its
   permissions block, its steps, which secrets and tokens each step can see, and its cost controls.
   Map every TASK.md requirement to a job or step. Don't write any files yet.

2. Implement the build, codeql, and dependency-review jobs from the plan. Use shell: pwsh,
   actions/setup-dotnet with 10.0.x, dotnet test --solution Bookstore.slnx, and CodeQL for C# with
   build-mode: none. Tee the build and test output into logs/ and upload it with
   actions/upload-artifact even when a step fails. Keep the top-level permissions at contents: read.

3. Add the risk-review job for pull requests. Skip it when git diff --name-only against the base
   branch shows nothing under src/. Write the pull request title and body (through env:) and the
   capped diff of src and tests into one file; never put ${{ github.event.* }} in a run: script.
   The prompt goes in ci/prompts/risk-review.md. The AI_AGENT repository variable picks the agent:
   claude (the default) runs claude -p with the file on stdin, --bare, and --tools ""; copilot runs
   copilot -p from a scratch folder holding only the file and the prompt, with no shell, writes,
   or URLs, an AI credit cap (--max-ai-credits), and the job's copilot-requests: write. Upload the
   review as an artifact, and post it from a separate job that has pull-requests: write, checks out
   the pull request's base commit, runs only scripts/post-pr-comment.ps1, and still runs when the
   build failed.

4. Add the failure-triage job: when build fails, download build-logs and write the last 300 lines
   of each log to a file. The AI_AGENT repository variable picks the agent: claude (the default) gets
   the file on stdin with --bare, --tools "Read", --permission-mode dontAsk, and no --allowedTools
   rule; copilot runs copilot -p from a scratch folder holding only the file and the prompt, with no
   shell, writes, URLs, or built-in MCP servers, an AI credit cap, and the job's
   copilot-requests: write. Cap turns, spend, and time. Upload the answer as an ai-triage artifact.
   On pull requests, post it from a separate job that has pull-requests: write, checks out the base
   commit, runs only scripts/post-pr-comment.ps1, and still runs although build failed. Put the
   prompt in ci/prompts/failure-triage.md, and tell the model never to suggest skipping tests or
   suppressing warnings as the fix.

5. Review the workflow as an attacker who can open a pull request, from a fork or from a branch in
   this repository. What could the title, the description, the code, or a change to ci.yml make a
   step or the agent do, and what could leak? For each job, list the token's scopes and the secrets
   each step can see. List each risk with the line that mitigates it, or say it's unmitigated.

6. Draft ci/README.md for the teammate who sets this up: the one-time setup (the ANTHROPIC_API_KEY
   secret, the AI_AGENT variable, the Copilot CLI policy, Code Security, and the ruleset after the
   first run), each setting in the env: block and its cost role, where the output goes, and the
   security model. Use only facts that are in ci.yml.
```

## What review caught

Review of the generated YAML (prompt 5 plus a person reading every line) checks for the mistakes AI
assistants commonly make in GitHub Actions workflows. This result avoids each one:

- **The pull request title pasted into a script.** The first draft logged
  `echo "Reviewing: ${{ github.event.pull_request.title }}"` in the diff step. GitHub expands that
  expression into the script text before PowerShell runs, so a title such as `"; whoami #` becomes code
  with the job's token in reach. The title and body now arrive as `env:` values (`$env:PR_TITLE`), and
  `check-workflow.cs` flags the old form.
- **The API key at job level.** The draft put `ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}` under
  the `risk-review` job's `env:`, so every step saw it, including `npm install` (and any install script
  a package runs). It's now in the agent step's `env:` only.
- **A comment-capable token in the Copilot step.** The draft gave `risk-review` `pull-requests: write` so
  it could post. The Copilot step needs the run's `GITHUB_TOKEN`, so that token could then comment,
  and Copilot CLI's built-in GitHub MCP server would use it for whatever a steered model asked. The
  result splits the job: `risk-review` has `contents: read` and `copilot-requests: write` and uploads
  the review as an artifact; `post-risk-review` has `pull-requests: write` and runs only the script. The
  Copilot step also passes `--disable-builtin-mcps`. The triage got the same split once it gained a
  Copilot branch: `failure-triage` can't comment, and `post-failure-triage` posts.
- **The pull request's own posting script with the write token.** The posting job first checked out
  the pull request, so a pull request that edited `scripts/post-pr-comment.ps1` would have run its own
  version with `pull-requests: write`. The posting jobs now check out the base commit
  (`ref: ${{ github.event.pull_request.base.sha }}`, `persist-credentials: false`). That's defense in
  depth: on `pull_request` from a branch in this repository, the branch's own `ci.yml` still runs with
  the secrets, and CODEOWNERS gates the merge, not the run.
- **A posting job that never ran on a broken build.** `post-risk-review` had
  `if: ${{ needs.risk-review.outputs.ready == 'true' }}`. With no status function, that's an implicit
  `success()`, which is false when `build` failed anywhere up the `needs` chain, so step 8's pull
  request (which breaks a test) would have got no review. Both posting jobs now start with
  `!cancelled() &&`.
- **A `Read` allow rule.** The first triage step had `--tools "Read"` plus `--allowedTools "Read"`.
  With `--permission-mode dontAsk`, reads inside the checkout already run without approval and
  anything else is denied; a bare `Read` rule matches every file read, so injected log text could
  have the agent read `/proc/self/environ` (where `ANTHROPIC_API_KEY` lives) into the posted note. The
  rule is gone.
- **A Claude-only triage.** The first triage job always ran Claude Code, so a Copilot-only class got
  no triage. It now follows `AI_AGENT` like the review, and the Copilot step reads only a scratch
  folder with the log tails: adding the checkout with `--add-dir` would also load its skills and
  agents as trusted configuration.
- **`pull_request_target` "so fork pull requests get the key".** Asked about forks, the assistant
  suggested it. Rejected: it runs in the base repository's context with secrets and a write token, and
  checking out the pull request's code there hands both to its author. Fork pull requests now skip the
  review with a warning.
- **A shallow clone that skipped every review.** The draft diffed `origin/main...HEAD` after a default
  checkout (depth 1). `git diff` failed with `ambiguous argument`, the `src/` check saw no files, and
  the job reported "No changes under src/" on every pull request. `fetch-depth: 0` fixes it, and the
  base branch now comes from `GITHUB_BASE_REF` instead of a hard-coded `main`.
- **The token left on disk.** `actions/checkout` stores the token in `.git/config` by default, where an
  agent with file access could read it. Every AI job checks out with `persist-credentials: false`.
- **Posting an error as a review.** A failed headless run prints its error on stdout, so it landed in
  `review.md`. The agent step now deletes the file on a non-zero exit, and only a successful run sets
  the `ready` output that the upload and the posting job check.
- **Checked and fine:** every action pinned to a major version tag; CodeQL as advanced setup with
  default setup left off; `continue-on-error: true` and `timeout-minutes` on every AI job; the AI jobs
  aren't in the ruleset's required checks; a missing secret makes the agent step skip with a warning
  instead of failing; every Copilot CLI step has `--max-ai-credits` (`AGENT_MAX_AI_CREDITS`, the CLI's
  minimum of 30), the spend cap `TASK.md` asks for.

## Edited by hand

- **`git diff ... -- src tests`** in place of the full diff, so lock files and docs don't spend the
  input budget.
- **The `run` output.** The diff step sets it, and the Node.js, install, and agent steps check it, so a
  docs-only pull request skips cleanly instead of installing a CLI for nothing.
- **`ci/prompts/failure-triage.md`.** "You may read files in the repository" became "when the
  repository's files are available to you", because the Copilot step works from the logs alone.
- **`ci/README.md`**, drafted with prompt 6 and then rewritten by hand from the review notes; it's the
  part a teammate reads first.

## Verify

```powershell
dotnet build -warnaserror
# Build succeeded: 0 Warning(s), 0 Error(s)

dotnet test --solution Bookstore.slnx
# Test run summary: Passed!  total: 19 (no application changes in this activity)

dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
# All checks passed.

./scripts/post-pr-comment.ps1 -BodyPath TASK.md -PullRequest 1 -Marker ai-risk-review -WhatIf
```
