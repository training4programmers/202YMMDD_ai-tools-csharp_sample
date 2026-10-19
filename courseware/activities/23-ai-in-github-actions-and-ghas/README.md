# Activity 23: Put an Agent in the Workflow

## Objective

Turn the Bookstore's build-and-test GitHub Actions workflow into deterministic gates (build with warnings
as errors, tests, formatting, CodeQL, and dependency review) and two advisory AI jobs: a **risk review**
of every pull request that changes application code, focused on the pricing and stock rules, and a
**failure triage** when the build breaks. The review must work with either Claude Code or GitHub Copilot
CLI, chosen by a repository variable. You'll design for the fact that pull request text is untrusted
input, keep every secret in the one step that needs it, grant write scopes only to the jobs that need
them, and put cost controls on every agent run. `TASK.md` in the lab folder is the specification.

![A GitHub Actions workflow: a build job (setup-dotnet 10.0.x, restore, build, test, dotnet format check), then the codeql and dependency-review gates (CodeQL build-mode none with security-events: write, dependency review on pull requests), then three advisory AI jobs: pr-summary on pull_request, whose comment a separate step posts with gh pr comment and pull-requests: write; failure-triage on if: failure(), which uploads a triage artifact; and release-notes on push to main, a draft artifact a person edits and approves. Notes: repository or organization secrets mapped with env: only into the step that runs the agent, and guardrails: pull request text is untrusted, no write tools or token for the agent, --max-turns, timeout-minutes, continue-on-error](../../diagrams/png/13-workflow-agent-jobs.png)

*Figure: The demo's workflow shape. Your Bookstore version replaces the summary with a risk review, adds a Copilot CLI option, and leaves release notes as the stretch goal.*

## Prerequisites

- .NET 10 SDK, Git, the GitHub CLI (`gh`), PowerShell 7, and Visual Studio 2026 or VS Code with C# Dev
  Kit
- One AI tool, signed in: GitHub Copilot (Agent mode) or Claude Code, to help you write the workflow
- **To run the workflow for real (optional):** a private repository in your instructor's GitHub class
  organization, which provides Copilot seats, Actions, GitHub Code Security, and an `ANTHROPIC_API_KEY`
  organization secret. Fallback: a public repository in your own account (Advanced Security features
  are free there; don't add an API key to it, so the AI jobs skip with a warning). Without either, you
  complete and verify everything offline (step 7)
- For the Copilot CLI variant in a real run: a Copilot seat in that organization, with Copilot CLI
  allowed by its policy (usage is billed to the organization in AI credits)
- Module 13 chapters 06–08, and [Demo 23](../../demos/23-ai-in-github-actions-and-ghas/)

## Skills Practiced

- Structuring a workflow as gates first, advisory AI jobs second, with `needs:` and `if:`
- Running Claude Code (`claude -p`) and Copilot CLI (`copilot -p`) headless with least capability
- Choosing the agent per run with a repository variable (`vars.AI_AGENT`)
- Keeping pull request text out of scripts (`env:`, never `${{ github.event.* }}` in `run:`)
- Granting token scopes per job, and mapping each secret into exactly one step
- Adding cost controls: conditions, input caps, model tier, turn, spend, and time limits
- Verifying a workflow offline, then gating merges with a ruleset and code scanning results

## Instructions

Start the lab from the course repo root. This copies `begin/` to a folder outside the repo, makes it a
git repository, and commits a baseline, so `git diff` always shows exactly what you and your assistant
changed:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 23
cd ~\labs\activity-23-ai-in-github-actions-and-ghas
```

Open the folder (or `Bookstore.slnx`) in Visual Studio 2026 or VS Code. To start over, re-run the
command with `-Force`. The reference in `solution/` is **one acceptable result**; yours will differ.

> **In class / take-home.** In class (about 30 minutes): steps 1 to 3, then step 5 (the failure
> triage), then step 7's `check-workflow.cs` and `-WhatIf` checks on what you have. Take-home: step 4
> (the risk review and the agent choice), step 6, step 7's attacker review, step 8, and the stretch
> goal.

1. **Read the specification.** Read `TASK.md` and the current `.github/workflows/ci.yml`. Confirm
   `dotnet build` and `dotnet test --solution Bookstore.slnx` pass, and that `dotnet build -warnaserror`
   passes too.

   <details>
   <summary>Guided help: step 1</summary>

   Run these from the lab folder, not from the course repo:

   ```powershell
   dotnet build -warnaserror
   # Build succeeded.
   #     0 Warning(s)
   #     0 Error(s)

   dotnet test --solution Bookstore.slnx
   # Test run summary: Passed!
   #   total: 19

   dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
   # OK   .github/workflows/ci.yml parses. Jobs: build
   #      job build
   # All checks passed.
   ```

   **Look for:** in `TASK.md`, which items are gates (1 to 3) and which are advisory (4 and 5), and the
   constraints you'll check at the end. In `ci.yml`, the single `build` job and the top-level
   `permissions: contents: read`, which stays exactly as it is.

   </details>

2. **Plan with your assistant.** Ask for a job-by-job plan before any YAML: each job's `if:` and `needs:`,
   its steps, its `permissions:`, which secrets and tokens each step can see, and its cost controls.
   Check the plan against every gate and constraint in `TASK.md`, and fix the plan before you let it
   write.

   <details>
   <summary>Guided help: step 2</summary>

   Stay read-only, so nothing changes yet.

   > **GitHub Copilot:** Ask mode in VS 2026 or VS Code; attach `TASK.md` and `.github/workflows/ci.yml`
   > with `#file`.
   >
   > **Claude Code:** plan mode (Shift+Tab until the footer shows it), or start with
   > `claude --permission-mode plan`.

   ```text
   Read TASK.md and .github/workflows/ci.yml. Plan a GitHub Actions workflow for the Bookstore that
   meets every gate and constraint in TASK.md. For each job give its if: and needs:, its permissions
   block, its steps, which secrets and tokens each step can see, and its cost controls. Map every
   TASK.md requirement to a job or step. Don't write any files yet.
   ```

   **Check:** every `TASK.md` item maps to a job or step; the top level stays `contents: read`;
   `security-events: write` appears only on the CodeQL job; `pull-requests: write` only on a job that
   posts a comment; `ANTHROPIC_API_KEY` only on agent steps.

   **If it doesn't:** reply "List the steps that can see ANTHROPIC_API_KEY and the jobs whose token can
   write. Move each secret into the one step that runs the agent, and each write scope onto the job
   that needs it."

   </details>

3. **Build the gates.** Implement the `build`, `codeql`, and `dependency-review` jobs. The build and test
   output must survive a failed step so the triage job can read it.

   <details>
   <summary>Guided help: step 3</summary>

   Switch to an agent mode (Copilot Agent mode, or accept the plan in Claude Code).

   ```text
   Implement the build, codeql, and dependency-review jobs from the plan. Use shell: pwsh,
   actions/setup-dotnet with 10.0.x, dotnet test --solution Bookstore.slnx, and CodeQL for C# with
   build-mode: none. Tee the build and test output into logs/ and upload it with
   actions/upload-artifact even when a step fails. Keep the top-level permissions at contents: read.
   ```

   ```powershell
   dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
   # OK   .github/workflows/ci.yml parses. Jobs: build, codeql, dependency-review
   # All checks passed.
   ```

   **Check:** `build` uses `-warnaserror` and `--verify-no-changes`; the upload step has
   `if: ${{ !cancelled() }}` (or `always()`); `codeql` grants `security-events: write` on the job, not at
   the top; `dependency-review` has `if: ${{ github.event_name == 'pull_request' }}`.

   **If it doesn't:** a step-level condition is the usual miss. Reply "The log upload must run when the
   build or test step fails. Add if: ${{ !cancelled() }} to that step."

   </details>

4. **Build the risk review.** On pull requests that change `src/`, collect the diff (capped) and pass the
   pull request's title and description as data, run the agent with no tools or read-only access, and post
   its answer to the pull request from a **separate** step with the given `scripts/post-pr-comment.ps1`.
   Keep the prompt in its own file under `ci/prompts/`. Make the agent selectable with the `AI_AGENT`
   repository variable: `claude` or `copilot`.

   <details>
   <summary>Guided help: step 4</summary>

   Agent mode. This is the step where most security mistakes happen, so read the YAML it writes.

   ```text
   Add the risk-review job for pull requests. Skip it when git diff --name-only against the base
   branch shows nothing under src/. Write the pull request title and body (passed through env:) and
   the capped diff of src and tests into one file; never put ${{ github.event.* }} in a run: script.
   Put the prompt in ci/prompts/risk-review.md: risks to PriceCalculator, Quote, Book.RemoveStock,
   decimal money and rounding, weakened tests, and new packages. The AI_AGENT repository variable picks
   the agent: claude (the default) runs claude -p with the file on stdin, --bare, and --tools "";
   copilot runs copilot -p from a scratch folder holding only the file and the prompt, with no shell,
   writes, or URLs, an AI credit cap (--max-ai-credits), and the job's copilot-requests: write. Upload
   the review as an artifact, and post it from a separate job that has pull-requests: write, checks
   out the pull request's base commit, runs only scripts/post-pr-comment.ps1, and still runs when the
   build failed.
   ```

   **Check:** `PR_TITLE` and `PR_BODY` appear only under an `env:`; the checkout has `fetch-depth: 0`;
   the Claude step has `--max-turns` and `--max-budget-usd`, and the Copilot step `--max-ai-credits`;
   both agent steps' `if:` read `vars.AI_AGENT`; the job has `continue-on-error: true` and
   `timeout-minutes`; the posting job checks out `github.event.pull_request.base.sha`, and its `if:`
   starts with `!cancelled()`.

   **If it doesn't:** if the job that runs Copilot also has `pull-requests: write`, its `GITHUB_TOKEN` can
   comment for a steered model. Reply "Keep this job at contents: read and copilot-requests: write,
   upload review.md as an artifact, and post it from a separate job that has pull-requests: write,
   checks out the base commit, and runs only scripts/post-pr-comment.ps1." If the posting job's `if:`
   has no status function, it gets an implicit `success()` and is skipped whenever `build` failed: ask
   for `if: ${{ !cancelled() && needs.risk-review.outputs.ready == 'true' }}`.

   </details>

5. **Build the failure triage.** When the `build` job fails, give the agent that `AI_AGENT` picks the end
   of the logs, upload its answer as an artifact, and comment on the pull request on pull request runs.

   <details>
   <summary>Guided help: step 5</summary>

   Agent mode.

   ```text
   Add the failure-triage job: when build fails, download build-logs and write the last 300 lines of
   each log to a file. The AI_AGENT repository variable picks the agent: claude (the default) gets the
   file on stdin with --bare, --tools "Read", --permission-mode dontAsk, and no --allowedTools rule;
   copilot runs copilot -p from a scratch folder holding only the file and the prompt, with no shell,
   writes, URLs, or built-in MCP servers, an AI credit cap, and the job's copilot-requests: write.
   Cap turns, spend, and time. Upload the answer as an ai-triage artifact. On pull requests, post it
   from a separate job that has pull-requests: write, checks out the base commit, runs only
   scripts/post-pr-comment.ps1, and still runs although build failed. Put the prompt in
   ci/prompts/failure-triage.md, and tell the model never to suggest skipping tests or suppressing
   warnings as the fix.
   ```

   **Check:** the job has `needs: build` and `if: ${{ failure() }}`; it uses `actions/download-artifact`
   for `build-logs`; both agent steps' `if:` read `vars.AI_AGENT`; Claude gets `--tools "Read"` and no
   `--allowedTools "Read"`; the Copilot step has `--max-ai-credits`; the job's token has
   `copilot-requests: write` but not `pull-requests: write`; the upload runs even if the agent step
   fails; the posting job runs only on `pull_request` events, checks out the base commit, and its `if:`
   starts with `!cancelled()`.

   **If it doesn't:** if the Claude step has `--allowedTools "Read"`, remove it: `dontAsk` already lets
   it read inside the checkout, and a bare `Read` rule allows every file read, including
   `/proc/self/environ`, where the API key lives. For the output, ask "What lands in triage.md when the
   agent fails, for example with no API key? Delete it on a non-zero exit, set a ready output only on
   success, and make the posting job's if: check that output."

   </details>

6. **Write the operator notes.** Add `ci/README.md` with the one-time setup (the secret, the `AI_AGENT`
   variable, the Copilot CLI policy, Code Security, the ruleset), the settings and their cost role, where
   the output goes, and the security model.

   <details>
   <summary>Guided help: step 6</summary>

   Let the assistant draft it, then edit it yourself: this is the file a teammate reads first.

   ```text
   Draft ci/README.md for the teammate who sets this up: the one-time setup (the ANTHROPIC_API_KEY
   secret, the AI_AGENT variable, the Copilot CLI policy, Code Security, and the ruleset after the
   first run), each setting in the env: block and its cost role, where the output goes, and the
   security model. Use only facts that are in ci.yml.
   ```

   **Check:** it says CodeQL default setup stays off (the workflow is the advanced setup), that the
   ruleset comes after the first run so the check names exist, and how to switch agents
   (`gh variable set AI_AGENT --body copilot`).

   **If it doesn't:** reply "Compare every claim in ci/README.md with ci.yml and list anything the
   README says that ci.yml doesn't do."

   </details>

7. **Verify offline.** Run `tools/check-workflow.cs` on your workflow; it must report "All checks
   passed." Dry-run the comment-posting script with `-WhatIf`. Then review your own workflow as an
   attacker who can open a pull request: what could the pull request's text or changes make a step or the
   agent do, and what could leak?

   <details>
   <summary>Guided help: step 7</summary>

   ```powershell
   dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
   # OK   .github/workflows/ci.yml parses. Jobs: build, codeql, dependency-review, risk-review, ...
   # All checks passed.

   ./scripts/post-pr-comment.ps1 -BodyPath TASK.md -PullRequest 1 -Marker ai-risk-review -WhatIf
   # gh pr comment 1 --repo <owner>/<repo> --body-file <temp file>
   # ...
   # What if: Performing the operation "post or update the ai-risk-review comment" on target "pull request #1 in <owner>/<repo>".
   ```

   For the attacker review, go back to a read-only mode (Ask mode, or plan mode):

   ```text
   Review the workflow as an attacker who can open a pull request, from a fork or from a branch in
   this repository. What could the title, the description, the code, or a change to ci.yml make a
   step or the agent do, and what could leak? For each job, list the token's scopes and the secrets
   each step can see. List each risk with the line that mitigates it, or say it's unmitigated.
   ```

   **Check:** no risk is "unmitigated" except the one every `pull_request` workflow has: anyone who can
   push a branch to the repository runs that branch's own `ci.yml` and scripts with the repository's
   secrets. Does the answer say that CODEOWNERS on `.github/workflows/` gates the merge, not the run?
   Does it name what's left: write access only for people you'd trust with the key, a low spend limit,
   posting jobs that run the base branch's script, and, stronger, an environment secret with required
   reviewers?

   **If it doesn't:** paste any `FAIL` line from the checker to the assistant and ask it to fix that
   finding, then run the checker again. If the review calls CODEOWNERS a fix for that risk, ask it
   when CODEOWNERS applies: at merge, after the run has already used the secrets.

   </details>

8. **Run it (if you have a repository).** Create the private repository in the class organization, push
   to `main`, and let that first run finish. Only then add the ruleset on `main`: required status checks
   and code scanning results, which need the check names from a finished run. (Once the ruleset requires
   a pull request, direct pushes to `main` are rejected.) Open a pull request that changes a pricing rule
   in `PriceCalculator`, and read the risk review it posts. Then set `AI_AGENT` to `copilot`, push again,
   and compare. Break the build on a branch and read the triage.

   <details>
   <summary>Guided help: step 8</summary>

   ```powershell
   gh repo create <class-org>/bookstore-activity23 --private --source . --push
   gh run watch                    # the first run on main: gates only, all green
   # Then add the ruleset: Settings > Rules > Rulesets (see ci/README.md, step 6)

   git switch -c tax-rate
   # In src/Bookstore.Domain/PriceCalculator.cs, change TaxRate from 0.06m to 0.07m
   git commit -am "Raise the sales tax rate to 7%"
   git push -u origin tax-rate
   gh pr create --fill
   gh pr checks --watch

   gh variable set AI_AGENT --body copilot
   git commit --allow-empty -m "Review again with Copilot CLI"
   git push
   ```

   **Check:** the pull request gets a comment headed **Risk level** whose **Pricing and stock rules**
   section names the tax change. `PriceCalculatorTests` expects 6% tax, so the build fails too, and an
   **AI failure triage** comment appears; its evidence should name that test. Both comments are posted
   even though the build failed. The second push, reviewed and triaged by Copilot CLI, updates the
   same two comments instead of adding new ones.

   **If it doesn't:** a warning that `ANTHROPIC_API_KEY` is not available means the organization secret
   isn't shared with your repository: ask your instructor, or add it with `gh secret set`. In a class
   that uses only GitHub Copilot, there's no key: run `gh variable set AI_AGENT --body copilot` before
   you open the pull request, and both AI jobs use Copilot CLI from the first push. If **Post the risk
   review** or **Post the failure triage** shows as skipped while its agent job produced a note, its
   `if:` is missing `!cancelled()` (step 4). If a `copilot -p` step fails, check its log for an
   authorization or policy error: the organization's Copilot CLI policy must be on, and you need a
   Copilot seat there.

   </details>

<details>
<summary>Prompting hints</summary>

- Give the assistant `TASK.md` as the spec, and ask it to map each requirement to a job or step in its
  plan, so you can see what it skipped.
- Say which GitHub Actions facts matter: `${{ }}` in a `run:` script is pasted in before the shell runs;
  secrets reach a step only where you map them; fork pull requests get no secrets and a read-only token;
  CodeQL for C# uses `build-mode: none`.
- Ask it to justify every tool it gives the agent and every permission scope it grants a job. If the
  input arrives on stdin or in a file, how many tools does the agent actually need?
- For the Copilot CLI step, ask what the token in its environment can do, and what else runs in the same
  job.
- Ask what lands in the output file when an agent run fails (a missing key, a turn cap), and whether the
  posting step would then post that error as the review.
- Ask for an explicit list of which steps can see `ANTHROPIC_API_KEY` and which jobs' tokens can write.
  Exactly one step per job for the key is the target.
- For the attacker review, ask the assistant to role-play a contributor who controls the pull request's
  title, description, code, and the workflow file on their branch.
</details>

## Requirements

- `.github/workflows/ci.yml` has `build`, `codeql`, and `dependency-review` jobs as `TASK.md` describes:
  Release build with warnings as errors, `dotnet test --solution Bookstore.slnx`,
  `dotnet format --verify-no-changes`, logs uploaded even on failure, CodeQL with `build-mode: none`,
  and dependency review on pull requests.
- The top-level `permissions` is `contents: read`; write scopes appear only on the jobs that need them;
  the workflow doesn't use `pull_request_target`.
- The risk review runs only on pull requests, skips pull requests with no changes under `src/`, caps the
  diff, and posts its comment from a step that doesn't run an agent.
- The `AI_AGENT` repository variable runs the review and the triage with Claude Code (`claude -p`) or
  Copilot CLI (`copilot -p`), and the unused agent's steps don't run.
- Failure triage runs only when `build` fails and uploads an artifact.
- The pull request's title and description reach the agent only through `env:` and a file; no
  `${{ github.event.* }}` or `${{ github.head_ref }}` appears inside any `run:` script.
- Each agent runs with no tools or read-only access and ignores repository instruction files (`--bare`,
  or `--no-custom-instructions` from a folder outside the checkout).
- `ANTHROPIC_API_KEY` is mapped into one step per job, never at job or workflow level; the posting steps
  get `GH_TOKEN` and run no agent. No secret values are in the repository.
- Every agent job has `continue-on-error: true`, `timeout-minutes`, and a model choice; Claude Code steps
  also have `--max-turns` and `--max-budget-usd`, and Copilot CLI steps `--max-ai-credits`.
- Prompts live in `ci/prompts/`, and `ci/README.md` documents setup, cost controls, where the output
  goes, and the security model.
- `tools/check-workflow.cs` reports "All checks passed." for your workflow; the application code and tests
  are unchanged, and `dotnet test --solution Bookstore.slnx` passes.

## Expected behavior

```powershell
dotnet build -warnaserror
# Build succeeded.
#     0 Warning(s)
#     0 Error(s)

dotnet test --solution Bookstore.slnx
# Test run summary: Passed!
#   total: 19, failed: 0

dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
# OK   .github/workflows/ci.yml parses. Jobs: build, codeql, dependency-review, risk-review, ...
#      job build
#      ...
#      job failure-triage  needs build  if ${{ failure() }}
# All checks passed.

./scripts/post-pr-comment.ps1 -BodyPath TASK.md -PullRequest 1 -Marker ai-risk-review -WhatIf
# gh pr comment 1 --repo <owner>/<repo> --body-file <temp file>
# ...
# What if: Performing the operation "post or update the ai-risk-review comment" on target "pull request #1 in <owner>/<repo>".
```

With a real repository, a pull request that changes `PriceCalculator` gets a comment headed **Risk
level** with a **Pricing and stock rules** section, ending with the "AI-generated and not verified"
footer; a pull request that changes only docs gets no review.

## Stretch goal

Add a third advisory job that drafts release notes on pushes to `main` from the commits since the last
tag, uploads them as an artifact, and never runs on pull requests. Then add `--output-format json` to
one Claude Code step and write the run's `total_cost_usd` estimate to the job summary
(`$env:GITHUB_STEP_SUMMARY`). (Leave `solution/` as is; this is extra practice.)

## Reference

- Concepts: [Module 13: AI Across the Delivery Lifecycle with GitHub](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/),
  especially [Agents in GitHub Actions](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/06-agents-in-github-actions.md),
  [GitHub Advanced Security as a Quality Gate](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/07-github-advanced-security.md),
  [Cost Visibility](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/08-cost-visibility.md), and
  [Where AI Fits in the Delivery Lifecycle](../../docs/module-13-ai-across-the-delivery-lifecycle-with-github/01-where-ai-fits-in-the-lifecycle.md)
- Worked example: [matching demo](../../demos/23-ai-in-github-actions-and-ghas/)
