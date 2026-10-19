# Demo 23: AI in GitHub Actions and GHAS (ToolsApp / Colors)

Instructor demo paired with [Activity 23](../../activities/23-ai-in-github-actions-and-ghas/). It turns
a build-and-test workflow into deterministic gates (warnings as errors, tests, format, CodeQL, dependency
review) followed by advisory AI jobs (a pull request summary, failure triage, and release notes), and
uses an AI-written SQL injection to show the gates, the triage, Copilot Autofix, and the agent-fix
fallback working together.

## Concept

- **Gates decide; AI advises.** Build, tests, analyzers, and GitHub Advanced Security block merges through
  a ruleset. AI jobs run on their own conditions, with `continue-on-error: true` and `timeout-minutes`,
  and are never required checks.
- **Headless agents in a job.** `claude -p` (and `copilot -p`, in the activity) runs one prompt to
  completion and prints the answer. The job decides what the agent can read, which tools it has, and
  where its text goes.
- **Pull request text is untrusted.** The title and description go through `env:` into a file, never
  into a `run:` script as `${{ github.event.* }}` (script injection). The agent gets no tools or read-only
  ones, `--bare` ignores the pull request's own `CLAUDE.md` and `.mcp.json`, and the token that posts
  comments lives in a separate step, which runs the posting script from the base branch.
- **Least privilege by default.** Top-level `permissions: contents: read`; `pull-requests: write` only on
  the jobs that comment, `security-events: write` only on CodeQL; each secret mapped into one step.
- **GitHub Advanced Security as a gate.** CodeQL for C# needs no build (`build-mode: none`); the ruleset's
  **Require code scanning results** blocks merges on new alerts. **Copilot Autofix** suggests a fix for
  the alert; an agent fix from the IDE, with tests that attack the bug, is the fallback.

![A GitHub Actions workflow: a build job (setup-dotnet 10.0.x, restore, build, test, dotnet format check), then the codeql and dependency-review gates (CodeQL build-mode none with security-events: write, dependency review on pull requests), then three advisory AI jobs: pr-summary on pull_request, whose comment a separate step posts with gh pr comment and pull-requests: write; failure-triage on if: failure(), which uploads a triage artifact; and release-notes on push to main, a draft artifact a person edits and approves. Notes: repository or organization secrets mapped with env: only into the step that runs the agent, and guardrails: pull request text is untrusted, no write tools or token for the agent, --max-turns, timeout-minutes, continue-on-error](../../diagrams/png/13-workflow-agent-jobs.png)

*Figure: Gates first, then advisory AI jobs. Secrets and write scopes reach only the steps that need them.*

## How to run

From the course repo root, copy the starting point to a lab folder outside the repo (so the repo's own
AGENTS.md/CLAUDE.md don't leak into the session) and open it. Copy the recorded outcome too, because
Step 3 and the *Short on time* path take files from it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 23 -Open
.\courseware\utils\start-lab.ps1 -Demo 23 -Solution
cd ~\labs\demo-23-ai-in-github-actions-and-ghas
dotnet build
dotnet test --solution ToolsApp.slnx
```

`dotnet build` reports one warning, `EF1002`, on purpose: `ColorSearchEndpoints.cs` is an AI-written
endpoint that builds SQL from an interpolated string.

Before class (for Steps 4 to 6; Steps 1 to 3 need only the lab folder):

- **A private repository in the class organization**, created from the lab folder:
  `gh repo create <class-org>/toolsapp-demo23 --private --source . --push`. That first run (build and
  test only) passes, which is Step 1's point.
- **GitHub Code Security** enabled on it (the class organization enables it for private repositories),
  with CodeQL **default setup off**: the workflow is the advanced setup, and you can't run both.
- **The `ANTHROPIC_API_KEY` organization secret** shared with the repository (or a repository secret),
  from a Claude Console workspace with a low spend limit. The demo's AI jobs run Claude Code only.
  *No Anthropic key?* Leave the secret out: the AI jobs skip with a warning, the gates still run as the
  trail describes, and you show the AI side from the recorded outcome instead (Step 4's *illustrative*
  triage note, and the `-Solution` copy's workflow and prompts).
- **No ruleset on `main` yet.** Step 4 pushes straight to `main`, and you add the ruleset in Step 5,
  once the check names exist.
- **One agent, signed in** for Steps 2 and 5: GitHub Copilot agent mode or Claude Code, frontier tier.
- **Fallbacks.** No class organization: a public repository in your own account (Advanced Security is
  free there; don't add the secret, so the AI jobs skip with a warning and the gates still run). No
  GitHub at all: Steps 1 to 3 are fully offline.
- Budget about 30 minutes, split around a break, with the *Short on time* list at the top of the Demo
  trail (the full trail takes about 40); a full run takes several minutes, so start Step 4's run before
  you explain it.

If the live run goes sideways, the `-Solution` copy (`~\labs\demo-23-ai-in-github-actions-and-ghas-solution`)
holds the recorded outcome: the full `.github/workflows/ci.yml`, `scripts/post-pr-comment.ps1`, the
prompts in `ci/prompts/`, the operator notes in `ci/README.md`, and the fixed search endpoint with its
tests.

## Demo trail

> **Short on time (about 30 minutes, split around a break).** Before Step 1, copy `.github/`, `ci/`, and
> `scripts/` from the `-Solution` copy into the lab, commit, and push to `main`: that's Step 4's run, and
> it fails at **Build and test** (`EF1002`), runs the triage, and raises the CodeQL alert while you work.
> Part 1 (about 20 minutes): Steps 1 to 3, reviewing Step 2's draft against the solution's file without
> committing it. Part 2, after the break (about 10 minutes): Step 4 on the finished run, then Step 5 as a
> walk-through of the ruleset, the alert's Autofix suggestion, and the fixed endpoint and its tests in
> the solution. Step 6 is optional.

**Code to open (in order):**

1. `.github/workflows/ci.yml`: build and test only. It passes today, even though the code has a SQL
   injection.
2. `src/ToolsApp.Api/Endpoints/ColorSearchEndpoints.cs`: the AI-written search endpoint, marked "not yet
   reviewed", with `FromSqlRaw($"... LIKE '%{name}%'")`.
3. `tools/check-workflow.cs`: a .NET 10 file-based app that checks a workflow for the mistakes this demo
   is about, because a workflow only runs on GitHub.
4. From the solution, after the demo: `.github/workflows/ci.yml`, `ci/prompts/*.md`,
   `scripts/post-pr-comment.ps1`, and `ci/README.md`.

**Step 1: The starting point.** Show the build warning, then the bug:

```powershell
dotnet build ToolsApp.slnx -warnaserror      # EF1002 is now an error
dotnet run --project src/ToolsApp.Api        # in a second terminal:
(Invoke-RestMethod 'http://localhost:5090/api/colors/search?name=sea').name
# Deep Sea
(Invoke-RestMethod ("http://localhost:5090/api/colors/search?name=" + [uri]::EscapeDataString("' OR 1=1 --"))).Count
# 9
```

The point: the current workflow only builds and tests, so its run on GitHub passed. Nothing in it would
stop this.

**Step 2: Have an agent write the gated workflow.** Prompt (Agent mode, or a Claude Code session in plan
mode, then normal mode):

```text
Extend .github/workflows/ci.yml for this .NET 10 solution, with shell: pwsh:
1. build: actions/setup-dotnet 10.0.x; restore with --locked-mode when packages.lock.json exists;
   build in Release with warnings as errors; dotnet test --solution ToolsApp.slnx; dotnet format
   --verify-no-changes; upload the build and test logs as an artifact even when a step fails.
2. codeql: CodeQL for C# with build-mode none. dependency-review: on pull requests.
3. pr-summary, on pull requests: run claude -p on the pull request's title, description, and diff,
   then post the summary as a comment with scripts/post-pr-comment.ps1 and gh.
4. failure-triage, when build fails: run claude -p on the end of the logs and upload the answer as
   an ai-triage artifact.
5. release-notes, on pushes to main: draft release notes from the commits since the last tag.
ANTHROPIC_API_KEY is an organization secret. Every AI job must be advisory and capped in turns,
spend, and time, must treat pull request text as untrusted, and must get only the permissions and
secrets it needs. Put the prompts in ci/prompts/.
```

The review beat, against the solution's version. Check the draft for:

- `${{ github.event.pull_request.title }}` (or `.body`, or `${{ github.head_ref }}`) inside a `run:`
  script: GitHub pastes the value into the script before the shell runs, so a pull request titled
  `"; curl ... #` runs code on your runner. It belongs in `env:`;
- `ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}` under the job's (or the workflow's) `env:`,
  where every step sees it, instead of the agent step's;
- `permissions: write-all` (or no `permissions:` block at all) instead of `contents: read` at the top
  and `pull-requests: write` on the jobs that comment;
- `on: pull_request_target` "so fork pull requests get the secret": it runs with secrets and a write
  token, and checking out the pull request's code there hands both to its author;
- `--dangerously-skip-permissions`, `--allowedTools "Bash"`, or no `--max-turns`;
- `--allowedTools "Read"` on the triage step: a bare `Read` rule pre-approves every file read, so log
  text that steers the model could have it read `/proc/self/environ` (where `ANTHROPIC_API_KEY` lives)
  into a note that gets posted. `--tools "Read"` with `--permission-mode dontAsk` already allows reads
  inside the checkout and denies the rest;
- no `fetch-depth: 0` before diffing against `origin/main` (a shallow clone has no base branch to diff);
- the posting step running the pull request's own `scripts/post-pr-comment.ps1` with the write token.
  The solution checks out the base commit (`ref: ${{ github.event.pull_request.base.sha }}`) for it, so
  a pull request that edits the script can't change what runs with `pull-requests: write`.

Modern models avoid some of these; if the live draft is clean, show this prepared, *illustrative*
fragment instead, and ask the room to find three problems:

```yaml
on: pull_request_target
permissions: write-all
jobs:
  pr-summary:
    runs-on: ubuntu-latest
    env:
      ANTHROPIC_API_KEY: ${{ secrets.ANTHROPIC_API_KEY }}
    steps:
      - uses: actions/checkout@v7
        with:
          ref: ${{ github.event.pull_request.head.sha }}
      - run: npm install --global @anthropic-ai/claude-code
      - run: |
          echo "Summarizing: ${{ github.event.pull_request.title }}"
          git diff origin/main | claude -p "Summarize this and post it to the PR" --dangerously-skip-permissions
```

Then compare with the solution's `ci.yml`, job by job.

**Step 3: Verify offline.** A workflow runs only on GitHub, but you can check most of it first. The
starter has no `scripts/` folder, so unless Step 2's draft created `scripts/post-pr-comment.ps1` under
that exact name, copy the reviewed script from the `-Solution` copy first (and point the draft's posting
step at it):

```powershell
if (-not (Test-Path scripts/post-pr-comment.ps1)) {
    Copy-Item -Recurse ~/labs/demo-23-ai-in-github-actions-and-ghas-solution/scripts .
}
dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
"## Summary`nTest." | Set-Content sample.md
./scripts/post-pr-comment.ps1 -BodyPath sample.md -PullRequest 1 -Marker ai-pr-summary -WhatIf
```

The checker lists the jobs with their `needs:` and `if:`, then one `FAIL` line per finding. To show it
catching the fragment above, save the fragment as `bad-ci.yml` in the lab folder (outside `.github/`)
and run the checker on it:

```powershell
dotnet run --file tools/check-workflow.cs -- bad-ci.yml
# OK   bad-ci.yml parses. Jobs: pr-summary
#      job pr-summary
# FAIL bad-ci.yml: top-level permissions: write-all lets every job write; use contents: read and grant write scopes per job
# FAIL bad-ci.yml: on: pull_request_target runs with secrets and a write token; use pull_request
# FAIL bad-ci.yml: job pr-summary, step #3: ${{ github.event.pull_request.title }} inside run: is script injection; pass it through env: instead
# FAIL bad-ci.yml: job pr-summary runs an agent without continue-on-error: true, so a model failure fails the run
# FAIL bad-ci.yml: job pr-summary runs an agent without timeout-minutes, so a hung run holds a runner for hours
# FAIL bad-ci.yml: jobs.pr-summary.env.ANTHROPIC_API_KEY: secret ANTHROPIC_API_KEY outside a step's env:; map it into the one step that needs it
# 6 problem(s) found.
```

Delete `bad-ci.yml` afterwards. The `-WhatIf` run prints the `gh pr comment` command the script would
run and the comment body, including the "AI-generated and not verified" footer, without calling
GitHub.

**Step 4: Run it, and watch the gates and the triage.** Commit and push the new workflow to `main`. The
**Build and test** job now fails: `-warnaserror` turns `EF1002` into an error. **AI failure triage** runs
because of `if: failure()`; open the run's summary page and download its `ai-triage` artifact. An
*illustrative* triage note:

```text
## Most likely cause
The build treats warnings as errors, and EF1002 fires in ColorSearchEndpoints.cs: FromSqlRaw is
called with an interpolated string, which inserts user input directly into SQL.
## Evidence
ColorSearchEndpoints.cs(22,14): error EF1002: Method 'FromSqlRaw' inserts interpolated strings ...
## Suggested next step
Replace FromSqlRaw with a parameterized query (FromSql, or a LINQ Where with EF.Functions.Like) and
add a test that searches for ' OR 1=1 --.
## Confidence
High: the error names the file, line, and API.
```

Meanwhile **CodeQL** ran beside the build (it needs no build), and Security > Code scanning shows a SQL
injection alert on the `FromSqlRaw` line (an *illustrative* title: "SQL query built from user-controlled
sources"). Point out what the agent could *not* do: it had the `Read` tool only, kept inside the
checkout by `dontAsk`, no shell, and no GitHub token, and its output is an artifact a person reads.
On a pull request, the comment is posted by a later step that runs the base branch's copy of
`scripts/post-pr-comment.ps1`.

**Step 5: Protect `main`, then fix it with an agent.** Now that the check names exist, add the ruleset
(Settings > Rules > Rulesets > New branch ruleset, default branch): **Require a pull request before
merging** with **Required approvals** at 0 (so you can merge your own pull request in Step 6; a real
team requires at least one), **Require status checks to pass** with `Build and test`, and **Require
code scanning results** with `CodeQL` at **High or higher**. (`Dependency review` runs only on pull requests, so add
it to the required checks after this step's pull request has run it once.) From here on, changes to
`main` go through a pull request. Open the alert and select **Generate fix**: Copilot Autofix proposes a patch and explains
it (free on public repositories; private ones need Code Security). Whether or not your plan shows it,
make the fix the fallback way, on a new branch:

```text
ColorSearchEndpoints.cs builds SQL from user input (EF1002, SQL injection). Fix it without changing
behavior: case-insensitive partial match on Name. Treat SQL and LIKE wildcard characters in the input
as plain text. Add tests that search for "sea", "DEEP", "' OR 1=1 --", "%", and "_". Run
dotnet build -warnaserror and dotnet test --solution ToolsApp.slnx. Don't suppress any analyzer.
```

Review beat: a `Contains` rewrite compiles, passes the injection test, and quietly makes the search
case-sensitive on SQLite (`instr`); the `DEEP` test catches it. The solution uses `EF.Functions.Like`
with an escaped pattern, sent as a parameter. Compare it with Autofix's suggestion if you have one.
Remove the "not yet reviewed" header once you've reviewed it. Push and open the pull request
(`gh pr create --fill`): the build passes, CodeQL reports the alert as fixed, dependency review passes,
and **AI pull request summary** posts a comment. Read the summary against the diff.

**Step 6: Release notes on `main`.** Merge the pull request. The run on `main` uploads
`release-notes-draft`; download it and edit it as the release manager would. Show where the cost
controls live (the `env:` block's `AGENT_MODEL`, `AGENT_MAX_TURNS`, `AGENT_MAX_BUDGET_USD`, plus each
job's `if:` and `timeout-minutes`) and where the spend shows up: the Console workspace for the API key,
and the organization's billing page for Actions minutes (chapter 08).

**Key teaching beat:** the cheapest gate caught the bug first. `-warnaserror` turned an analyzer warning
into a failed build in seconds, CodeQL confirmed it as a security alert, the AI triage explained it, and
an agent fixed it with tests that attack it. None of the AI jobs could block a merge, push code, or read
a secret or token they didn't need, which is what makes them safe to run on every pull request.

## Reference

- Concepts: [Module 13: AI Across the Delivery Lifecycle with GitHub](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/),
  especially [Agents in GitHub Actions](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/06-agents-in-github-actions.md),
  [GitHub Advanced Security as a Quality Gate](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/07-github-advanced-security.md),
  [Cost Visibility](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/08-cost-visibility.md), and
  [Where AI Fits in the Delivery Lifecycle](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/01-where-ai-fits-in-the-lifecycle.md)
- Tools: [Tools Review](TOOLS-REVIEW.md)
- Practice: [Activity 23](../../activities/23-ai-in-github-actions-and-ghas/)
