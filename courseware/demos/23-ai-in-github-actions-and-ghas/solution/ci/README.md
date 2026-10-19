# ToolsApp CI: gates plus advisory AI jobs

`.github/workflows/ci.yml` runs deterministic gates first and advisory AI jobs after them. The AI jobs
use the documented headless mode of Claude Code (`claude -p`). Try changes on a test repository or a
test pull request before you rely on them, and re-check the CLI flags when you bump its version.

## What runs when

| Job | Runs on | Blocks a merge? | What it does |
|-----|---------|-----------------|--------------|
| `build` (Build and test) | every run | Yes, as a required status check | Restore (`--locked-mode` when lock files exist), build with `-warnaserror`, `dotnet test --solution`, `dotnet format --verify-no-changes`; uploads `build-logs` even on failure |
| `codeql` (CodeQL) | every run, beside `build` | Yes, through "Require code scanning results" | CodeQL for C# with `build-mode: none`; results go to Security > Code scanning |
| `dependency-review` (Dependency review) | pull requests | Yes, as a required status check | Fails when the pull request adds a package with a known vulnerability |
| `pr-summary` | pull requests, after `build` | No (`continue-on-error`) | Pipes the title, description, and diff to `claude -p` with no tools; a separate step posts the result with the base branch's posting script |
| `failure-triage` | when `build` fails | No | Pipes the log tails to `claude -p` with the `Read` tool, confined to the checkout; uploads `ai-triage`, and comments on pull requests |
| `release-notes` | pushes to `main`, after `build` and `codeql` pass | No | Pipes the commits since the last tag to `claude -p`; uploads `release-notes-draft` for a person to edit |

## One-time setup

1. **Repository.** Create it from the lab folder, private, in the organization that provides your
   seats and GitHub Advanced Security:
   `gh repo create <your-org>/toolsapp-demo23 --private --source . --push`.
2. **The API key.** Create `ANTHROPIC_API_KEY` in a dedicated Claude Console workspace for CI and set
   that workspace's spend limit. Store it as an **organization secret** shared with this repository, or
   as a **repository secret**: Settings > Secrets and variables > Actions > New repository secret, or
   `gh secret set ANTHROPIC_API_KEY`. The workflow maps it into the agent steps only.
3. **Workflow permissions.** Settings > Actions > General > Workflow permissions: keep the default,
   **Read repository contents and packages permissions**. The workflow asks for more, job by job.
4. **Code security.** Private repositories need **GitHub Code Security** (and Secret Protection for
   secret scanning) enabled: Settings > Advanced Security. Public repositories get these features free.
   This workflow is CodeQL's **advanced setup**, so leave **default setup** off: use one or the other,
   never both. The dependency graph must be on for `dependency-review`.
5. **Let one run finish.** Push to `main` and let the first run complete, so its check names exist.
6. **The ruleset.** Settings > Rules > Rulesets > New branch ruleset, targeting the default branch:
   - **Require a pull request before merging**, with **Required approvals** at 0 for this demo, so the
     author can merge their own pull request. A real team requires at least one approval.
   - **Require status checks to pass**: `Build and test`, and `Dependency review` once a pull request
     has run it (it runs only on pull requests, so its name isn't listed before that).
   - **Require code scanning results**: tool `CodeQL`; security alerts at **High or higher**, alerts at
     **Errors**.
   - Don't add the AI jobs as required checks: they're advisory, and they skip on many runs.

   From now on, changes reach `main` through pull requests.

## Settings in the workflow

| Setting | Default | Why |
|---------|---------|-----|
| `CLAUDE_CODE_VERSION` | `2.1.301` | Pin the CLI you tested; bump it deliberately |
| `AGENT_MODEL` | `sonnet` | A balanced-tier alias; summaries and triage rarely need the frontier tier |
| `AGENT_MAX_TURNS` | `4` | `claude -p` exits with an error when it hits the cap |
| `AGENT_MAX_BUDGET_USD` | `0.50` | Stops the run at an estimated spend; the bill can differ slightly |
| `timeout-minutes` (per job) | `10` for AI jobs | A hung agent can't hold a runner (and its minutes) for hours |
| Input cap (in the summary step) | 150,000 characters | A huge diff costs more and summarizes worse |

## Where the output goes

- **Pull request comments:** the summary (marker `ai-pr-summary`) and, when a pull request build
  fails, the triage (`ai-failure-triage`). Each later push updates the same comment.
- **Artifacts** on the run's summary page (or `gh run download <run-id> --name ai-triage`):
  `build-logs` (7 days), `ai-triage` (7 days; `triage-error.txt` when the agent itself failed), and
  `release-notes-draft` (30 days).
- **Code scanning alerts:** Security > Code scanning, and annotations on the pull request.

## Security model

Pull request text, code, comments, and commit messages are **untrusted input**. Anyone who can open a
pull request can write text that tries to steer the model ("ignore your instructions and..."), or a
title that tries to run as a shell command. The workflow is built so that neither works:

- **No script injection.** The title and description reach the script through `env:` and the agent
  as data in a file. No `${{ github.event.* }}` value is ever pasted into a `run:` script, where GitHub
  would expand it before the shell runs.
- **`pull_request`, not `pull_request_target`.** A pull request from a fork runs with a read-only token
  and no secrets, so it can't use either. `pull_request_target` would hand both to a run next to the
  pull request's code.
- **Least privilege.** The workflow's token is `contents: read`. Only `codeql` (`security-events: write`)
  and the two jobs that comment (`pull-requests: write`) get more, and only the posting steps receive
  the token. Checkouts in AI jobs use `persist-credentials: false`, so the token isn't left in
  `.git/config` for the agent to find.
- **The posting script comes from the base branch.** Before posting, each job checks out the pull
  request's base commit into `base/` (`ref: ${{ github.event.pull_request.base.sha }}`) and runs
  `./base/scripts/post-pr-comment.ps1`, so the code that holds the write token is the reviewed copy,
  not whatever the pull request changed under `scripts/`.
- **No tools, or read-only tools.** Summaries and release notes run with `--tools ""`; triage gets
  `--tools "Read"` with `--permission-mode dontAsk`: reads inside the working directory (the checkout)
  run without approval, and anything that would prompt, including a read outside it, is denied.
  There's deliberately no `--allowedTools "Read"`: a bare `Read` rule matches every file read, so
  injected log text could have the agent open `/proc/self/environ`, where `ANTHROPIC_API_KEY` lives,
  and the note would carry it to the pull request.
- **`--bare`.** The CLI skips `CLAUDE.md`, hooks, skills, plugins, and `.mcp.json`. On a pull request
  those files come from the pull request, so loading them would let its author configure the agent.
- **Secrets only where needed.** `ANTHROPIC_API_KEY` is mapped into each job's agent step and nowhere
  else; never into a job's or the workflow's `env:`.
- **Advisory and labeled.** Every AI job has `continue-on-error: true` and `timeout-minutes`; comments end
  with an "AI-generated and not verified" footer. A failed agent run posts nothing: its error text is
  deleted, and only a successful run sets the `ready` output the posting step checks.
- **The workflow file itself.** On a `pull_request` from a branch in this repository, the branch's own
  `ci.yml` and scripts run with the repository's secrets, so anyone who can push a branch is trusted
  with CI secrets: they could change the workflow to print the key. Fork pull requests get no secrets.
  CODEOWNERS on `.github/workflows/` gates the merge, not the run, so it doesn't stop this. Give write
  access only to people you'd trust with the key, keep its spend limit low, and rotate it if you suspect
  misuse. The stronger option is an environment secret with required reviewers: a job can't read it
  until a reviewer approves the run (on private repositories that needs GitHub Enterprise Cloud).

## Variants

- **`anthropics/claude-code-action@v1`.** With a `prompt` input it runs in automation mode; pass caps
  and tool rules in `claude_args` (for example `--max-turns 4 --allowedTools "Bash(git diff *)"`, which
  pre-approves that one command; never a bare `Read` rule, which pre-approves reads anywhere on the
  runner). It can post comments itself, which means the agent's run holds the GitHub token; this
  workflow keeps the token in a separate step.
- **GitHub Copilot CLI.** See Activity 23's solution for a `copilot -p` step. It authenticates with the
  run's `GITHUB_TOKEN` when the job grants `copilot-requests: write`, usage is billed to the
  organization in AI credits, and `--max-ai-credits` caps a run (a soft cap, minimum 30).

## Try the checks locally

```powershell
dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
"## Summary`nA test comment." | Set-Content sample.md
./scripts/post-pr-comment.ps1 -BodyPath sample.md -PullRequest 1 -Marker ai-pr-summary -WhatIf
```

## Troubleshooting

| Symptom | Likely cause |
|---------|--------------|
| A step logs "ANTHROPIC_API_KEY is not available to this run" | The secret doesn't exist, the organization secret isn't shared with this repository, or the pull request comes from a fork (no secrets) |
| Posting fails with `Resource not accessible by integration` (HTTP 403) | The job lacks `pull-requests: write`, or the run is a fork pull request, whose token is read-only |
| CodeQL upload fails because default setup is enabled | Turn off CodeQL default setup for this repository (Settings > Advanced Security) |
| CodeQL or dependency review says the feature isn't enabled | Enable GitHub Code Security on the private repository, and the dependency graph |
| A required check shows "Expected: waiting for status to be reported" | The check name in the ruleset doesn't match the job's `name`, or that job was skipped |
| A push to `main` is rejected by the ruleset | Expected once the ruleset is on: push a branch and open a pull request |
| `claude` exits with a max-turns error | Raise `AGENT_MAX_TURNS` a little, or shorten the input; don't remove the cap |
