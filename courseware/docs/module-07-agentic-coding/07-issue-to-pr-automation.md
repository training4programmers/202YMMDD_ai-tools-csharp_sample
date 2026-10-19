# Issue to Pull Request: Cloud Agents on GitHub

Everything so far has run on your machine while you watched. A **cloud agent** runs somewhere else: you
hand it a GitHub issue, it works on its own branch on a GitHub Actions runner, and some minutes later a pull
request appears for you to review. You are no longer supervising each step; you are writing the
specification up front and reviewing the result at the end, exactly as you would with a remote teammate.

This chapter covers writing issues an agent can actually finish, the two cloud agents in this course (the
**GitHub Copilot cloud agent** and the **Claude Code GitHub Action**) and their workflow files, how to
review an agent's pull request, and the repository guardrails that keep delegation safe. It is the topic of
[Bonus Demo 04](../../demos/bonus/04-issue-to-pr-automation/) and
[Bonus Activity 04](../../activities/bonus/04-issue-to-pr-automation/).

---

## What changes when the agent runs in the cloud

| | Local agent | Cloud agent |
|---|---|---|
| Where it runs | Your machine | A GitHub-hosted runner |
| Who watches | You, live | Nobody; you review afterwards |
| Where the task lives | A prompt or `TASK.md` | The GitHub issue |
| What you get back | Changes in your working folder | A branch and a pull request |
| How you correct it | Interrupt and redirect | Review comments on the pull request |
| Cost | AI usage | AI usage **plus** Actions minutes |

Because nobody can answer its questions mid-run, a cloud agent is only as good as the issue it starts
from. That makes writing the issue the main skill.

---

## Writing an agent-ready issue

An **agent-ready issue** contains everything needed to reach a reviewable pull request without asking a
question. The structure that works:

1. **Goal**: what should change and why, in two or three sentences.
2. **Contract**: the exact behavior. For an endpoint: the route, parameters and limits, a sample response,
   and every error status.
3. **Acceptance criteria**: a checklist, each item verifiable, including the tests you expect and the
   commands that must pass.
4. **Constraints and out of scope**: no new packages, don't change existing endpoints or tests.
5. **Pointers**: files to read first and existing code to copy the pattern from.

Here is the contract section of the issue in Bonus Demo 04:

```markdown
`GET /api/colors/{id}/tints?count=3`

- `count` is optional, defaults to `5`, and must be between `1` and `10`.
- Tint `k` of `count` moves each RGB channel `k / (count + 1)` of the way to 255, rounded to the nearest
  whole number with midpoints rounded away from zero. The base color and pure white are not included.
- Tints are ordered from darkest to lightest, as upper-case `#RRGGBB`.
- Unknown color id: `404 Not Found`. `count` outside 1 to 10: `400` validation problem keyed `count`.
```

Every sentence there removes a decision the agent would otherwise make on its own: the default, the range,
the rounding rule, the order, and the error codes.

Signs an issue isn't ready:

- It describes a symptom, not a desired behavior ("export is weird").
- It needs knowledge that isn't in the repository ("like we discussed on Tuesday").
- It is more than one change ("add tints and clean up the color endpoints").
- Its only acceptance criterion is "it works".

### An issue template

An issue form keeps every task in that shape. GitHub reads YAML forms from `.github/ISSUE_TEMPLATE/`:

```yaml
name: Agent-ready task
description: A small, fully specified change that a coding agent (or a new teammate) can pick up without asking.
title: "[Task]: "
labels: ["agent-ready"]
body:
  - type: textarea
    id: goal
    attributes:
      label: Goal
      description: What should change and why, in two or three sentences.
    validations:
      required: true
  - type: textarea
    id: acceptance
    attributes:
      label: Acceptance criteria
      description: A checklist the pull request must meet, including the tests you expect.
    validations:
      required: true
```

The full template in the bonus pair also has Contract, Constraints, and Pointers fields.

---

## The GitHub Copilot cloud agent

The **Copilot cloud agent** (formerly called the "Copilot coding agent") is available on paid Copilot
plans: on by default for Pro and Pro+, and enabled by an administrator for Business and Enterprise. It
works only with repositories hosted on GitHub.

### Assigning work

1. Open the issue and, under **Assignees**, choose **Copilot**. You can add extra instructions and pick the
   base branch, a custom agent, and a model.
2. Copilot works in the background on a `copilot/` branch and opens a pull request, normally as a draft,
   then requests your review.
3. To ask for changes, comment on the pull request and mention `@copilot`; it pushes new commits to the
   same branch.

You can also start cloud agent tasks from the agents panel on GitHub, from VS Code (the **Cloud** session
target) or Visual Studio's cloud agent. Module 13 follows the same flow across the lifecycle, from planning in
[GitHub Issues and Projects](../module-13-ai-across-the-delivery-lifecycle-with-github/03-github-issues-and-projects-with-ai.md)
to [traceability and review](../module-13-ai-across-the-delivery-lifecycle-with-github/05-issue-to-pull-request.md).

Each session works in one repository and one branch, produces one pull request, and has a maximum run
time of 59 minutes. It reads the repository's instruction files, including
`.github/copilot-instructions.md`, path-specific instruction files, and `AGENTS.md`, and it can use the
repository's skills, custom agents, and hooks (chapter 05).

### `copilot-setup-steps.yml`: preparing the environment

By default the agent starts on a fresh Ubuntu runner and has to discover how to build your project. A setup
workflow does that work up front, so the agent spends its session on the task and not on installing an SDK:

```yaml
name: "Copilot Setup Steps"

on:
  workflow_dispatch:
  push:
    paths:
      - .github/workflows/copilot-setup-steps.yml
  pull_request:
    paths:
      - .github/workflows/copilot-setup-steps.yml

jobs:
  copilot-setup-steps:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    permissions:
      contents: read
    steps:
      - name: Check out the code
        uses: actions/checkout@v7

      - name: Install the .NET 10 SDK
        uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 10.0.x

      - name: Restore and build
        run: |
          dotnet restore ToolsApp.slnx
          dotnet build ToolsApp.slnx --no-restore
```

The rules are strict (see
[Customizing the development environment](https://docs.github.com/en/copilot/how-tos/use-copilot-agents/coding-agent/customize-the-agent-environment)),
so check them when the agent seems to ignore your setup:

- The file must be `.github/workflows/copilot-setup-steps.yml` on the **default branch**.
- It must contain a single job named exactly **`copilot-setup-steps`**.
- Only `steps`, `permissions`, `runs-on`, `services`, `snapshot`, and `timeout-minutes` (at most 59) are
  honored; anything else is ignored.
- Runners can be Ubuntu x64 or Windows x64, but the agent's built-in firewall doesn't work on Windows or
  self-hosted runners.
- The `push` and `pull_request` triggers make it run as an ordinary workflow whenever it changes, so you
  can see that the setup works before the agent depends on it.

### Built-in protections

The cloud agent can push only to its own `copilot/` branch (or to the pull request branch you asked it to
work on), its pull requests must be reviewed and merged by a person, and by default the repository's
Actions workflows don't run on its pull request until someone with write access approves them. Its
sessions consume AI credits, and its runner time counts against your Actions minutes.

---

## The Claude Code GitHub Action

Anthropic's equivalent is a GitHub Action, `anthropics/claude-code-action@v1`, that runs Claude Code inside
your own workflow ([Claude Code GitHub Actions](https://code.claude.com/docs/en/github-actions)). Mention `@claude` in an issue or pull request comment and Claude responds there:
implementing the issue, answering a question, or pushing fixes to a pull request.

### Setting it up

The quick way is to run `/install-github-app` inside Claude Code in the repository. It needs the GitHub
CLI (`gh`) signed in, admin access to the repository, and a github.com remote. It installs the Claude
GitHub App, stores a secret (`ANTHROPIC_API_KEY` for an API key, or `CLAUDE_CODE_OAUTH_TOKEN` for a Claude
subscription), and opens a pull request with the workflow file.

To do it by hand: install the Claude GitHub App on the repository, add one of the secrets, and add a
workflow such as this one from the bonus pair:

```yaml
name: Claude Code

on:
  issues:
    types: [opened]
  issue_comment:
    types: [created]
  pull_request_review_comment:
    types: [created]

concurrency:
  group: claude-${{ github.event.issue.number || github.event.pull_request.number }}
  cancel-in-progress: false

jobs:
  claude:
    if: >-
      (github.event_name == 'issues' &&
        (contains(github.event.issue.body, '@claude') || contains(github.event.issue.title, '@claude'))) ||
      (github.event_name != 'issues' && contains(github.event.comment.body, '@claude'))
    runs-on: ubuntu-latest
    timeout-minutes: 30
    permissions:
      contents: write
      pull-requests: write
      issues: write
      id-token: write
      actions: read
    steps:
      - uses: actions/checkout@v7
        with:
          fetch-depth: 1
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 10.0.x
      - uses: anthropics/claude-code-action@v1
        with:
          anthropic_api_key: ${{ secrets.ANTHROPIC_API_KEY }}
          claude_args: >-
            --max-turns 30
            --allowedTools "Bash(dotnet restore *),Bash(dotnet build *),Bash(dotnet test *),Bash(dotnet format *)"
```

What each piece does:

- With no `prompt` input, the action runs in **interactive mode**: it waits for the trigger phrase
  (`@claude` by default, changeable with `trigger_phrase`) and replies on the issue or pull request. With a
  `prompt`, it runs in **automation mode** on any event, such as a schedule.
- `claude_args` passes Claude Code command-line options: `--max-turns` caps the work per request,
  `--allowedTools` lets it run the verify loop without asking, and `--model` picks a model.
- `id-token: write` is required for the action's default GitHub App authentication; `actions: read` lets
  Claude read CI results.
- Only users with **write access** can trigger it, and bot accounts are rejected unless listed in
  `allowed_bots`, which stops agents triggering each other in a loop.
- `concurrency` and `timeout-minutes` keep a burst of comments from starting several paid runs.
- Claude reads the repository's `CLAUDE.md`, so the same instructions apply in the cloud as on your machine.

The action can also authenticate through Microsoft Foundry, Amazon Bedrock, or Google Cloud instead of an
Anthropic key (`use_foundry`, `use_bedrock`, `use_vertex`). Separately, Claude and Codex are available as
partner agents inside GitHub itself (in public preview at the time of writing), which an organization
administrator must enable.

---

## Reviewing the agent's pull request

Review an agent's pull request exactly as you would a new teammate's. Its summary is a claim; the diff and
the checks are the evidence.

1. **Check it against the issue.** Go through the acceptance criteria one by one.
2. **Read the whole diff.** Look for files the issue didn't mention, new packages, and changes to existing
   tests.
3. **Check the tests.** Do they test the contract, including the error cases, or only the happy path? Did
   any assertion get weaker?
4. **Run it.** `gh pr checkout <number>`, then `dotnet build` and `dotnet test --solution ToolsApp.slnx`,
   and try the `.http` request.
5. **Look for C# pitfalls** ([module 12](../module-12-csharp-pitfalls-and-verification/)): culture-sensitive formatting, missing cancellation tokens, `double`
   for money, swallowed exceptions.
6. **Ask for changes in comments**, specific and checkable: "Use decimal arithmetic for the rounding; the
   issue requires midpoints away from zero. Add the count=11 case." Then mention `@copilot` or `@claude`.

---

## Guardrails for delegated work

Delegation is safe when the repository, not goodwill, enforces the rules.

- **Branch protection or rulesets on the default branch**: require a pull request, at least one human
  approving review, and passing status checks. Copilot code review comments don't count toward required
  approvals.
- **A CI workflow that runs the verify loop** (build with warnings as errors, tests, format) as a required
  check, so "green" means the same thing for people and agents.
- **CODEOWNERS for automation files**: `.github/workflows/`, `.github/agents/`, `.github/hooks/`,
  `.claude/`, and the instruction files change what agents do, so route them to a maintainer.
- **Least-privilege workflow permissions**: grant only what each job needs, and keep secrets scoped to the
  workflows that use them.
- **Cost controls**: `--max-turns`, `timeout-minutes`, and `concurrency` for the action; budgets and the
  AI credits paid-usage policy for Copilot.
- **Small issues.** An agent pull request that touches four files gets a real review; one that touches
  forty gets a rubber stamp.

---

## Summary

- A **cloud agent** turns a GitHub issue into a branch and a pull request on a GitHub-hosted runner; you
  specify up front and **review afterwards**.
- An **agent-ready issue** has a goal, an exact contract, checkable acceptance criteria, constraints, and
  pointers; an **issue form** keeps every task in that shape.
- Assign an issue to the **Copilot cloud agent** from **Assignees**, and
  prepare its environment with **`copilot-setup-steps.yml`**: default branch, one job named
  `copilot-setup-steps`, .NET 10 installed with `actions/setup-dotnet`.
- The **Claude Code GitHub Action** (`anthropics/claude-code-action@v1`) responds to **`@claude`**; set it
  up with **`/install-github-app`** or by hand, and limit it with `claude_args`, timeouts, and concurrency.
- Review an agent's pull request **like any other**: acceptance criteria, the whole diff, the tests, a local
  run, and specific review comments.
- **Branch protection, required checks, CODEOWNERS, and least-privilege permissions** make delegation safe.
