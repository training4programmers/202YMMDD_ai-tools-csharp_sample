# Bookstore CI: gates plus an advisory AI risk review

`.github/workflows/ci.yml` runs deterministic gates first, then an advisory risk review on pull requests
and a triage when the build fails. The AI jobs use the documented headless modes of Claude Code
(`claude -p`) and GitHub Copilot CLI (`copilot -p`). Try changes on a test pull request first, and
re-check the CLI flags when you bump a version.

## Jobs

| Job | Runs on | Blocks a merge? | What it does |
|-----|---------|-----------------|--------------|
| `build` (Build and test) | every run | Yes, as a required status check | Restore (`--locked-mode` when lock files exist), build with `-warnaserror`, test, format check; uploads `build-logs` even on failure |
| `codeql` (CodeQL) | every run, beside `build` | Yes, through "Require code scanning results" | CodeQL for C# with `build-mode: none` |
| `dependency-review` (Dependency review) | pull requests | Yes, as a required status check | Fails when the pull request adds a vulnerable package |
| `risk-review` | pull requests that change `src/` | No | One agent reviews the diff for pricing, stock, and test risks, and uploads `ai-risk-review` |
| `post-risk-review` | when `risk-review` produced a review, even if `build` failed | No | Posts the review as a pull request comment; runs the base branch's script, never a model |
| `failure-triage` | when `build` fails | No | One agent reads the log tails and suggests a cause; uploads `ai-triage` |
| `post-failure-triage` | pull requests, when `failure-triage` produced a note | No | Posts the triage as a pull request comment; runs the base branch's script, never a model |

## Choosing the agent

The `AI_AGENT` repository variable picks the agent for both AI jobs: `claude` (the default when the
variable isn't set) or `copilot`. Set it in Settings > Secrets and variables > Actions > Variables, or
with `gh variable set AI_AGENT --body copilot`. The next run picks it up; the other agent's steps are
skipped, so it never installs or authenticates.

| | Claude Code | GitHub Copilot CLI |
|---|---|---|
| **Install** | `npm install --global @anthropic-ai/claude-code@<version>` | `npm install --global @github/copilot@<version>` |
| **Credential** | `ANTHROPIC_API_KEY`, a repository or organization secret, mapped into the agent step | The run's `GITHUB_TOKEN` in the agent step; the job grants `copilot-requests: write` |
| **Who pays** | The Claude Console workspace that owns the key (set its spend limit) | The organization, in GitHub AI Credits |
| **Input** | Review: the pull request text and diff on stdin. Triage: the log tails on stdin | Review: `pr.diff` in a scratch folder that holds nothing else. Triage: `logs.txt` in another such folder |
| **Tools** | Review: `--tools ""`, none. Triage: `--tools "Read"` with `--permission-mode dontAsk`, so reads stay inside the checkout | Reads in that folder only: `--deny-tool` for `shell`, `write`, and `url`, `--disable-builtin-mcps`, `--disallow-temp-dir`; triage works from the logs alone |
| **Caps** | `--max-turns`, `--max-budget-usd`, model alias, job timeout, input cap | `--max-ai-credits` (a soft cap, minimum 30), job timeout, input cap; model via `COPILOT_MODEL` |
| **Repository instructions** | `--bare` skips `CLAUDE.md`, hooks, and `.mcp.json` | `--no-custom-instructions` skips `AGENTS.md` and instruction files |

## One-time setup

1. **Repository.** `gh repo create <your-org>/bookstore-activity23 --private --source . --push`, in the
   organization that provides your Copilot seats and GitHub Advanced Security.
2. **Claude Code.** An `ANTHROPIC_API_KEY` organization secret shared with the repository (your
   instructor's), or a repository secret: `gh secret set ANTHROPIC_API_KEY`.
3. **Copilot CLI.** The organization policy that allows Copilot CLI, billed to the organization, must be
   on (it is by default where Copilot CLI is enabled). Nothing to store: the workflow uses the run's
   token. Then `gh variable set AI_AGENT --body copilot` to switch. Without an Anthropic key (a
   Copilot-only class), set it before your first pull request, so both AI jobs use Copilot CLI.
4. **Workflow permissions.** Settings > Actions > General: keep **Read repository contents and packages
   permissions** as the default; jobs ask for more themselves.
5. **Code security.** Enable GitHub Code Security on the private repository (Settings > Advanced Security),
   leave CodeQL **default setup** off (this workflow is the advanced setup), and keep the dependency
   graph on.
6. **First run, then the ruleset.** Let the first run on `main` finish so the check names exist. Then
   add a branch ruleset on the default branch: **Require a pull request before merging**, **Require
   status checks to pass** (`Build and test`), and **Require code scanning results** (`CodeQL`, High or
   higher). Add `Dependency review` to the required checks after your first pull request has run it (it
   runs only on pull requests). Don't require the AI jobs. After this, changes reach `main` only through
   pull requests.

## Cost controls in this workflow

- **Run less:** the review runs only on pull requests that change `src/`; triage only when `build`
  fails; a new push cancels the pull request's older run (`concurrency`).
- **Send less:** the diff is limited to `src` and `tests` and capped at `MAX_DIFF_CHARACTERS`.
- **Pick the tier:** `CLAUDE_MODEL: sonnet` (balanced); `COPILOT_MODEL` empty uses the plan default.
- **Cap the run:** `--max-turns` and `--max-budget-usd` (Claude Code), `--max-ai-credits` (Copilot CLI;
  `AGENT_MAX_AI_CREDITS`, at the CLI's minimum of 30), and `timeout-minutes` on every job. The credit
  cap is soft: the CLI checks it after each model response, so one response can go past it.
- **Cap the account:** a spend limit on the Console workspace, and an AI credit budget in the
  organization's billing settings for Copilot.

## Where the output goes

- **Pull request comments:** the risk review (`ai-risk-review`) and the triage (`ai-failure-triage`,
  pull requests only). A later push updates the same comment.
- **Artifacts** on the run's summary page: `build-logs`, `ai-risk-review`, and `ai-triage`
  (`triage-error.txt` when the agent itself failed). Download with
  `gh run download <run-id> --name ai-triage`.

## Security notes

Pull request content is untrusted input that can try to steer the model, and a title or description
pasted into a script can run as code. So:

- The title and description reach scripts only through `env:` and the agent only as data in a file;
  no `${{ github.event.* }}` appears inside a `run:` script.
- The trigger is `pull_request`, never `pull_request_target`: fork pull requests get a read-only token
  and no secrets.
- The workflow's token is `contents: read`. `risk-review` and `failure-triage` add
  `copilot-requests: write` and nothing else, so the token a Copilot step holds can't comment, push, or
  approve. Only `post-risk-review` and `post-failure-triage` get `pull-requests: write`; they run a
  script and never a model.
- The posting jobs check out the pull request's **base** commit
  (`ref: ${{ github.event.pull_request.base.sha }}`), so the `scripts/post-pr-comment.ps1` that runs
  with the write token is the reviewed copy on the base branch, not one the pull request changed.
- The posting jobs' conditions start with `!cancelled()`. Without a status function, a job gets an
  implicit `success()`, which is false when `build` failed anywhere up its `needs` chain, so the review
  and the triage would never be posted on a broken pull request.
- `ANTHROPIC_API_KEY` is mapped into the agent step of each job and nowhere else. Checkouts use
  `persist-credentials: false`.
- The agent gets no tools or read-only ones, and ignores the repository's instruction files. Claude
  Code's triage has `--tools "Read"` with `--permission-mode dontAsk`: reads inside the working
  directory (the checkout) run without approval, and anything that would prompt, including a read
  outside it, is denied. There's no `--allowedTools "Read"` on purpose: a bare `Read` rule matches every
  file read, so injected log text could have the agent open `/proc/self/environ`, where the API key
  lives, and the note would be posted to the pull request. Copilot CLI reads only its scratch folder.
- Every AI job has `continue-on-error: true` and `timeout-minutes`. A failed agent run posts nothing:
  its output is deleted, and only a successful run sets the `ready` output that posting checks.
- On a `pull_request` from a branch in this repository, the branch's own `ci.yml` and scripts run with
  the repository's secrets, so anyone who can push a branch is trusted with CI secrets. Fork pull
  requests get no secrets. CODEOWNERS on `.github/workflows/` gates the merge, not the run. So give
  write access only to people you'd trust with the key, keep its spend limit low, and rotate it if you
  suspect misuse. The stronger option is an environment secret with required reviewers: a job can't
  read it until a reviewer approves the run (on private repositories that needs GitHub Enterprise
  Cloud).

## Try the checks locally

```powershell
dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
./scripts/post-pr-comment.ps1 -BodyPath TASK.md -PullRequest 1 -Marker ai-risk-review -WhatIf
```
