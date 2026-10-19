# Cost Visibility

Once AI is part of the delivery lifecycle, it no longer shows up as one line on one bill. Copilot seats
and **AI credits** bill through GitHub, and so do the **Actions minutes** every cloud agent, code review,
and AI workflow job runs on. GitHub Code Security has its own AI credits for agentic autofix and AI Scan.
A Claude API key used in a workflow bills to a **Claude Console** organization. Each has its own unit,
its own report, and its own answer to the question finance will ask first: *which team spent this?*

This chapter maps where each AI cost of the lifecycle appears, how to attribute it to a team with cost
centers and Console workspaces, how to put budgets and alerts on it, and the controls to build into
agent jobs. Module 02 covered
plans and personal usage; Module 15
covers whether the spend is paying off.

---

## The Units

| Unit | What consumes it in this module | Notes |
|------|----------------------------------|-------|
| **GitHub AI Credits** (Copilot) | Chat and agent mode, the Copilot CLI (including `copilot -p` in workflows), the **Copilot cloud agent**, **Copilot code review** | Metered by tokens at each model's rate. **Code completions and next edit suggestions are included** on paid plans and don't use credits |
| **Code Security AI Credits** | **Agentic autofix** and **AI Scan for pull requests** | A separate SKU, shown as *Advanced Security* in enterprise billing, with its own budgets |
| **GitHub Actions minutes** | Every job on a GitHub-hosted runner: your CI and AI jobs, the cloud agent's sessions, the Claude Code GitHub Action, agentic autofix, and **Copilot code review on private repositories** | Free for standard runners on public repositories; private repositories use the plan's included minutes, then pay per minute. Windows and larger runners cost more per minute than standard Linux ones |
| **GHAS licenses** | Code Security and Secret Protection on private repositories | Licensed per active committer; free on public repositories |
| **Claude API tokens** | `claude -p` and `anthropics/claude-code-action@v1` with an `ANTHROPIC_API_KEY` | Billed per token to the Console organization that owns the key |

Plans, allowances, and per-unit prices change often, so this course quotes them only in dated,
illustrative examples. Use the vendors' pages: [Plans for GitHub Copilot](https://docs.github.com/en/copilot/get-started/plans),
[Budgets and alerts](https://docs.github.com/en/billing/concepts/budgets-and-alerts), and
[Claude pricing](https://claude.com/pricing).

Notice how many features cost **two** units at once. A Copilot code review on a private repository uses
AI credits *and* Actions minutes. A cloud agent session uses AI credits and minutes. An AI job in your
workflow uses Claude API tokens (or AI credits, for Copilot CLI) and minutes. Budgets on one unit alone
leave the other uncapped.

---

## Where Each Cost Shows Up

| Usage | Billed to | Where you see it | Attribution |
|-------|-----------|------------------|-------------|
| Copilot seats, and AI credits from IDEs, the CLI, the cloud agent, and code review | The enterprise or organization that grants the seat | **Billing and licensing** in the enterprise or organization settings: usage, and per-user AI credit reports | By **cost center** (enterprise) or by organization |
| Copilot CLI in a workflow, with `GITHUB_TOKEN` and `copilot-requests: write` | The organization that owns the repository, under its "billed to the organization" CLI policy | The organization's AI credit usage | By organization or cost center |
| Copilot CLI with a personal token (`COPILOT_GITHUB_TOKEN`) | The token owner's Copilot entitlement | That person's usage | By person; avoid it for shared automation |
| Agentic autofix, AI Scan | Code Security AI credits | Enterprise billing, under *Advanced Security* | By organization or cost center |
| Actions minutes | The account that owns the repository | **Billing and licensing** usage, by repository and runner type | By **repository** (and cost center) |
| `claude -p` or the Claude action with an API key | The Claude Console organization | The Console's usage and cost pages, by **workspace** and API key | By **workspace** |
| Claude through Amazon Bedrock, Google Cloud, or Microsoft Foundry | That cloud account | The cloud's own billing tools | By the cloud's own resource structure |

Two practical consequences. First, the **Claude action's OAuth token** (`CLAUDE_CODE_OAUTH_TOKEN`) bills
to the subscription of the person who created it, so for a secret shared across repositories, use an
API key from a Console workspace instead. Second, the cheapest attribution is the one you design before
rollout: retrofitting cost centers and workspaces after six months of shared keys means six months of
spend nobody can split.

---

## Attribution: Cost Centers and Workspaces

### GitHub cost centers

A **cost center** groups resources so their spend can be reported, budgeted, and charged back together.
Cost centers belong to an **enterprise** account; an enterprise owner or billing manager creates them and
adds **users**, **organizations**, **repositories**, or enterprise teams. Where a charge lands depends on
the product:

- **License-based** products, such as Copilot seats and the AI credits their users spend, are charged to
  the cost center that holds the **user**. A user assigned directly wins over their organization's
  cost center.
- **Usage-based** products, such as **Actions minutes**, are charged to the cost center that holds the
  **repository** or **organization** where the usage happened.

So "the platform team's AI spend" is two assignments: the team's people (for their credits) and their
repositories (for the minutes their workflows and agents use). An enterprise billed through an Azure
subscription can also point a cost center at its own subscription.

Without an enterprise account, attribution is coarser: by organization, and by repository for Actions
minutes. A class or small company with one organization usually doesn't need more.

### Claude Console workspaces

In the Claude Console, a **workspace** groups API keys, and usage is reported per workspace and per key.
Give each team (or each purpose) its own workspace and keys: a "CI" workspace for the keys in workflow
secrets, separate from the workspace developers use interactively. Workspace usage is the attribution,
and a workspace spend limit is the cap (below).

### Who can see it, and who can change it

The people who can see a cost are often not the people who can change it. Agree on owners before
rollout:

| Cost | Who can see it | Who can change it |
|------|----------------|-------------------|
| Copilot seats and AI credits | Enterprise and organization owners, billing managers | The same admins, through policies, budgets, and cost centers |
| Copilot code review | Billing managers (credits and minutes) | Repository admins (the ruleset rule, **Review new pushes**), and authors (effort level) |
| Actions minutes | Billing managers; repository admins see their runs | Workflow owners (triggers, conditions, `timeout-minutes`, runner choice) |
| CI agent keys | Console admins | Workflow owners (conditions, caps, tiers) and Console admins (spend limits) |

---

## Budgets and Alerts on GitHub

A GitHub **budget** watches spending against an amount and alerts at **75%, 90%, and 100%**. For
**metered** products it can also **stop** usage at the limit; for license-based products it only
alerts. To create one in an organization or enterprise:

1. Open **Billing and licensing** (for an organization, it's under *Access* in the settings sidebar) >
   **Budgets and alerts** > **New budget**.
2. Choose the **budget type**: a **product-level** budget (for example, Actions), a **SKU-level** budget
   (for example, *Advanced Security* > *Code Security AI Credits* for agentic autofix and AI Scan), or a
   **bundled AI credits** budget across all AI-credit SKUs.
3. Choose the **scope**: enterprise, organization, or cost center (and, for the bundled AI credits type,
   users). The scope can't change later.
4. Enter the amount, and select **Stop usage when budget limit is reached** where it's offered and you
   want a hard cap. Without it, usage continues and you get an email.
5. Select **Receive budget threshold alerts** and choose the recipients. Owners and billing managers get
   them by default.

Three details matter in practice:

- **The most restrictive budget wins.** A low organization budget can block a cost center before that
  cost center's own budget is reached.
- **User-level budgets always stop usage**, and their alerts are less reliable, so watch the cost center
  or organization level as well.
- **The first billing cycle can overrun** a new budget, even with stop-at-limit selected.

For Copilot overage on Business and Enterprise plans, the **AI credits paid usage** policy decides
whether usage can continue past the included credits at all
(Module 02).

### The Claude Console

For API keys in workflows, the controls are in the Console: a **workspace spend limit** (set below the
organization's own limit) stops that workspace's usage when it's reached, and the usage and cost pages
show spend by workspace and key. Keep CI keys in their own workspace with a low limit, so a runaway
workflow or a leaked key is capped at that amount, and rotate a key the moment its spend looks wrong.

| Where | Control | Stops spend? |
|-------|---------|--------------|
| GitHub, metered products (Actions, AI credits, Code Security AI credits) | Budget with **Stop usage when budget limit is reached** | Yes |
| GitHub, license-based products (seats) | Budget | No, it alerts |
| GitHub Copilot overage | **AI credits paid usage** policy | Yes, beyond the included credits |
| Claude Console | **Workspace spend limit** | Yes, for that workspace |
| A single workflow run | `--max-turns`, `--max-budget-usd` (Claude Code), `--max-ai-credits` (Copilot CLI, a soft cap), `timeout-minutes` | Yes, for that run |

---

## Cost Controls for Agents in CI

Agent jobs in workflows ([chapter 06](06-agents-in-github-actions.md)) run without anyone watching the
meter. Build the controls into the YAML:

- **Run less:** condition each AI job (pull requests, failures, `main`), skip pull requests that don't
  touch application code, and use `concurrency` with `cancel-in-progress` so a burst of pushes runs once.
- **Send less:** cap the input, send log tails instead of whole logs, exclude lock files and generated
  code. Input tokens are most of the cost of a summary.
- **Choose the tier:** a balanced or fast tier is enough for summaries and triage; keep the frontier tier
  for work that needs it.
- **Cap each run:** `--max-turns` and `--max-budget-usd` for Claude Code; `--max-ai-credits <credits>`
  for Copilot CLI (at least 30, and a soft cap: a response already in progress finishes, so a run can go
  slightly over; public preview at the time of writing); and `timeout-minutes` on every job, which is
  the hard stop for all of them and the cap on their Actions minutes.
- **Cap the account:** a Console workspace with a spend limit for CI keys; GitHub budgets on AI credits
  and Actions minutes.
- **Measure per run:** with `--output-format json`, Claude Code reports a `total_cost_usd` estimate you
  can write to the job summary and track:

```powershell
$result = Get-Content -Raw pr-input.md |
    claude --bare -p (Get-Content -Raw ci/prompts/pr-summary.md) --tools "" --model sonnet `
      --max-turns 4 --max-budget-usd 0.50 --output-format json | ConvertFrom-Json
$result.result | Out-File pr-summary.md -Encoding utf8
"AI summary estimated cost: $($result.total_cost_usd) USD" >> $env:GITHUB_STEP_SUMMARY
```

The estimate is computed by the client and can differ from the bill; reconcile it monthly against the
Console.

---

## Estimating Before You Roll Out

Token-metered costs are easy to estimate with a little arithmetic, and the estimate is the number to put
in the budget. Take Demo 23's pull request summary *(illustrative numbers)*:

| Input | Typical size |
|-------|--------------|
| Prompt file | about 400 tokens |
| Title, description, and diff of a medium pull request (`--stat` plus the patch) | about 20,000 tokens |
| Summary returned | about 600 tokens |

The demo runs the balanced tier, `--model sonnet`, which is Claude Sonnet 5.5 at the time of writing, at
$2 per million input tokens and $10 per million output tokens *(check the
[current pricing page](https://platform.claude.com/docs/en/about-claude/pricing) before you rely on
it)*:

```text
input:  20,400 tokens x $2  / 1,000,000 = $0.041
output:    600 tokens x $10 / 1,000,000 = $0.006
                                 total  ~ $0.047 per summary
```

Now multiply by how often it runs. A team with 300 pull requests a month that pushes three times per
pull request triggers about 900 pull request runs, so about $42 a month for summaries, before triage and
release notes. Add the minutes: if the AI job takes two minutes on a standard Linux runner, that's about
1,800 minutes a month on a private repository, before the build, CodeQL, and Copilot code review's own
minutes. The same arithmetic shows where the money goes: input dominates, so **capping the input** is
the most effective control, and a frontier tier at two or more times the rate multiplies everything. For
Copilot, the conversion is the same idea in different units: usage is metered at each model's rate and
converted to AI credits.

Run the real thing on one repository for two weeks, log the per-run estimates and the minutes, and
replace the assumptions with measurements before you size budgets for the whole organization.

### Signals worth an alert

- **A sudden jump in the CI workspace's spend**: often a loop (an agent hitting `--max-turns` on every
  run), a huge generated diff, or a leaked key.
- **Actions minutes climbing on one repository**: a workflow that lost its conditions, a cloud agent
  that's assigned everything, or **Review new pushes** on a repository that pushes constantly.
- **Copilot code review usage per pull request rising**: someone raised the effort level, or the
  instruction files grew.
- **A budget alert at 75% in the first week**: the budget, or the workflow's conditions, need another
  look.

---

## A Monthly Cost Review

Fifteen minutes a month keeps AI spend boring:

1. **Copilot:** AI credit usage by cost center (or organization) and the top users; is paid usage on or
   off, and is that still the right call?
2. **Actions:** minutes by repository and runner type; which share comes from AI jobs, the cloud agent,
   and Copilot code review?
3. **Code Security AI credits:** agentic autofix and AI Scan usage against their SKU budget.
4. **Claude Console:** the CI workspace's spend, and cost per pull request summary from the logged
   estimates.
5. **Budgets:** did any alerts fire? Are thresholds and stop-at-limit choices still sensible?
6. **Value:** pair the numbers with the outcome measures from
   Module 15. Spend that
   nobody can connect to an outcome is the first to cut.

---

## Summary

- Lifecycle AI costs land in **GitHub billing** (Copilot seats and **AI credits**, **Code Security AI
  credits**, **Actions minutes**, GHAS licenses) and the **Claude Console** (API keys in workflows); many
  features use **two units at once**, such as credits plus minutes.
- **Copilot code review** on **private repositories** and every **cloud agent** session also use **Actions
  minutes**; **Copilot CLI** in a workflow with `copilot-requests: write` bills the **organization**.
- Attribute spend with **cost centers** (enterprise; **users** for license-based charges, **repositories**
  and **organizations** for usage-based ones) and **Console workspaces** per team or purpose; decide the
  scheme **before rollout**.
- **GitHub budgets** (product, SKU, or bundled AI credits) alert at **75%, 90%, and 100%** and can **stop
  usage** for metered products; the **AI credits paid usage** policy and **Console workspace spend
  limits** also stop spend.
- Agents in CI need **run less, send less, cheaper tier, per-run caps, account caps**, and a logged
  **per-run estimate**, reconciled monthly with the real bill alongside a short **monthly review**.
