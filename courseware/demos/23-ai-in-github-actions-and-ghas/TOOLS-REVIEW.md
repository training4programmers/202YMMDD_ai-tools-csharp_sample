# Demo 23: Tools Review

A quick reference to the **AI-tool features this demo introduces**: running Claude Code and GitHub
Copilot CLI headless in a GitHub Actions job, restricting what an unattended agent can do, handing it
credentials safely, posting its output to a pull request, and the GitHub Advanced Security features it
runs beside (CodeQL, Copilot Autofix, dependency review). The concepts are tool-neutral and come from
Module 13. CLI flags change between releases, so the vendors' docs are the authority; pin the CLI
version you tested.

---

## Headless mode

Both CLIs run a single prompt to completion and print the answer, which is what a workflow step needs.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Command** | `copilot -p "<prompt>" -s --no-ask-user` | `claude -p "<prompt>"` (`claude --bare -p` in CI) |
| **Input** | In the prompt, or a file the agent may read; piped input is ignored when `-p` is used | Piped on stdin (`Get-Content input.md \| claude -p ...`) |
| **Output** | `-s` prints only the answer; `--output-format json` emits JSON Lines | `--output-format text`, `json` (includes `total_cost_usd`), or `stream-json` |
| **Model** | `--model`, or the plan's default | `--model sonnet` (alias), or a full model name |
| **Install in a job** | `npm install --global @github/copilot@<version>`; set `COPILOT_AUTO_UPDATE=false` | `npm install --global @anthropic-ai/claude-code@<version>` |
| **As an action** | None needed; the CLI runs in a `run:` step | `anthropics/claude-code-action@v1`: with a `prompt` input it runs in automation mode, with caps in `claude_args` |
| **Caps** | `--max-ai-credits <credits>` (minimum 30): a soft cap, checked after each model response, so one response can overshoot it (`copilot help limits`) | `--max-turns` and `--max-budget-usd` |
| **Watch out** | Add `timeout-minutes` and the organization's AI credit budget too | `--max-turns` exits with an error at the cap; mark the job `continue-on-error: true` |

```powershell
Get-Content -Raw pr-input.md |
  claude --bare -p (Get-Content -Raw ci/prompts/pr-summary.md) `
    --tools "" --model sonnet --max-turns 4 --max-budget-usd 0.50 `
    --permission-mode dontAsk --no-session-persistence
```

The action is the shortest path when you want Claude to act on the pull request itself (it can comment
and push with its own token). For text that a person reads, the CLI in a `run:` step keeps the token out
of the agent's reach, which is why the demo uses it.

The demo's workflow runs Claude Code only, because one agent keeps the trail short; Activity 23's
solution adds `copilot -p` steps chosen by a repository variable. If you have no Anthropic API key,
leave the secret out: the AI jobs skip with a warning and the gates still run, and you walk through the
recorded outcome instead (the `-Solution` copy's workflow and prompts, and the *illustrative* triage
note in the demo's README).

---

## Least capability for an unattended agent

Nobody approves tool calls in a workflow, so decide the agent's capabilities up front. For summaries and
release notes it needs none; for triage, read access to the checkout is enough.

> **GitHub Copilot:** in `-p` runs, anything not pre-approved with `--allow-tool` is denied. Run it from a
> folder that holds only its inputs (file reads stay inside it), add `--deny-tool='shell'`,
> `--deny-tool='write'`, and `--deny-tool='url'`, and turn off the built-in GitHub MCP server with
> `--disable-builtin-mcps`. `--no-custom-instructions` ignores `AGENTS.md` and instruction files. Avoid
> `--allow-all-tools`, `--allow-all`, and `--yolo` outside a sandbox.
>
> **Claude Code:** `--tools ""` removes every built-in tool; `--tools "Read"` leaves only file reads.
> With `--permission-mode dontAsk`, reads inside the working directory (the checkout) run without
> approval, and anything that would prompt, including a read outside it, is denied. Don't add
> `--allowedTools "Read"`: a bare `Read` rule matches every file read, so injected log text could make
> the agent open `/proc/self/environ`, where `ANTHROPIC_API_KEY` lives, and the note would carry it to
> the pull request. When a step also needs a shell command, allow only that command, for example
> `--allowedTools "Bash(git diff *)"`. `--bare` skips `CLAUDE.md`, hooks, skills, plugins, and
> `.mcp.json`, which on a pull request would come from the pull request itself.

---

## Credentials in a workflow

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Credential** | The run's own `GITHUB_TOKEN`, when the job grants `copilot-requests: write` (or a fine-grained PAT with the **Copilot Requests** permission in `COPILOT_GITHUB_TOKEN`) | `ANTHROPIC_API_KEY` from a Claude Console workspace |
| **Who pays** | The organization, in GitHub AI Credits, under its Copilot CLI policy | The Console workspace that owns the key (set a spend limit) |
| **Store it** | Nothing to store for the run's token | A repository or organization secret |
| **Hand it over** | `env: GITHUB_TOKEN: ${{ github.token }}` on the agent step only | `env: ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}` on the agent step only |

Secrets reach a step only where you map them, so map each one into the step that needs it, never into
a job's or the workflow's `env:`. Pull requests from forks get no secrets and a read-only token: skip the
AI step with a warning rather than reaching for `pull_request_target`. And check out with
`persist-credentials: false` in AI jobs, so the token isn't left in `.git/config`.

---

## Posting to the pull request

The agent writes text; `scripts/post-pr-comment.ps1` posts it with `gh pr comment`, updates its own
earlier comment (found by a marker) instead of adding one per push, and labels the comment as
AI-generated. It runs in a **separate step** with `GH_TOKEN: ${{ github.token }}`, on a job that grants
`pull-requests: write`:

```powershell
./base/scripts/post-pr-comment.ps1 -BodyPath pr-summary.md -PullRequest $env:PR_NUMBER -Marker ai-pr-summary
./scripts/post-pr-comment.ps1 -BodyPath sample.md -PullRequest 1 -Marker ai-pr-summary -WhatIf
```

In the workflow the script comes from a second checkout of the pull request's **base** commit
(`ref: ${{ github.event.pull_request.base.sha }}`, `path: base`, `persist-credentials: false`), so the
code that runs with the write token is the reviewed copy on the base branch, not whatever the pull
request changed under `scripts/`. That's defense in depth, not a wall: on a `pull_request` from a branch
in the same repository, the branch's own `ci.yml` runs with the repository's secrets, so anyone who can
push a branch is trusted with CI secrets. CODEOWNERS on `.github/workflows/` gates the merge, not the
run. The stronger option is an environment secret with required reviewers: a job can't read it until a
reviewer approves the run (on private repositories that needs GitHub Enterprise Cloud).

Post only what a successful run wrote. When a headless run fails (missing credentials, a turn or spend
cap), the error message goes to standard output, so it lands in the output file. The demo's agent steps
delete the file on a non-zero exit and set a `ready` step output only on success; the posting step's
`if:` checks it.

---

## CodeQL, Copilot Autofix, and dependency review

| Piece | Purpose |
|-------|---------|
| `github/codeql-action/init@v4` with `languages: csharp` and `build-mode: none` | Starts CodeQL without a build; the job needs `security-events: write` |
| `github/codeql-action/analyze@v4` | Runs the queries and uploads alerts to Security > Code scanning |
| `actions/dependency-review-action@v5` | Fails a pull request that adds a package with a known vulnerability |
| Ruleset: **Require code scanning results** | Blocks merging until CodeQL results exist and on new alerts at your severity threshold |

CodeQL **default setup** (Settings > Advanced Security) needs no YAML; the demo uses the **advanced setup**
in `ci.yml`. Pick one per repository.

> **GitHub Copilot:** **Copilot Autofix** suggests a fix for a code scanning alert, on the alert page
> (**Generate fix**) and on pull requests that introduce one. It's free on public repositories and needs
> GitHub Code Security on private ones. **Agentic autofix**, which opens a validated fix pull request
> through the Copilot cloud agent, is generally available for Code Security customers on GitHub
> Enterprise Cloud and uses AI credits. On other plans, use agent mode to fix the alert from the IDE.
>
> **Claude Code:** fix the alert in a session: give it the alert (or read it with the GitHub MCP
> server's `code_security` toolset), require tests that attack the bug, and forbid suppressions.

---

## Summary

- **Headless mode:** `claude --bare -p` (input on stdin) and `copilot -p -s --no-ask-user`; pin the CLI
  versions and install them with `actions/setup-node` and npm. `anthropics/claude-code-action@v1` runs in
  automation mode when you give it a `prompt`.
- **Least capability:** `--tools ""` or `--tools "Read"` with `--permission-mode dontAsk` for Claude Code,
  and no `--allowedTools "Read"`; a scratch folder, `--deny-tool`, `--disable-builtin-mcps`, and
  `--no-custom-instructions` for Copilot CLI.
- **Caps:** `--max-turns` and `--max-budget-usd` for Claude Code; `--max-ai-credits` (a soft cap,
  minimum 30) for Copilot CLI; `timeout-minutes` on every job.
- **Credentials:** `ANTHROPIC_API_KEY` as a repository or organization secret, mapped into one step;
  Copilot CLI uses the run's `GITHUB_TOKEN` with `copilot-requests: write`, billed to the organization.
- **Posting:** a separate step with `GH_TOKEN` and `gh pr comment`, running the script from a checkout
  of the base commit; only the jobs that comment get `pull-requests: write`. Post only when the agent
  step **succeeded**, because a failed run's error text is on stdout.
- **GHAS:** CodeQL with `build-mode: none`, dependency review on pull requests, and a ruleset that
  requires both. **Copilot Autofix** suggests the fix; an agent fix with attacking tests is the
  fallback.
