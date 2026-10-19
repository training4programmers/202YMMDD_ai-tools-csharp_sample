# Bonus Demo 04: Tools Review

This demo introduces the **cloud agents**: the **GitHub Copilot cloud agent** with its
**`copilot-setup-steps.yml`** environment file, and the **Claude Code GitHub Action** with its
`claude.yml` workflow, plus the **issue forms** and **repository rules** that make delegating to them
safe. The concepts are tool-neutral; the names and files are GitHub's and Anthropic's at the time of
writing, and both products change often, so the vendors' documentation is the authority.

---

## Delegating an issue

| | GitHub Copilot | Claude Code |
|---|---|---|
| Product | Copilot cloud agent | Claude Code GitHub Action (`anthropics/claude-code-action@v1`) |
| Start it | Issue > **Assignees** > **Copilot** (optional prompt, base branch, custom agent, model); also from VS Code, Visual Studio, or the agents panel | Mention `@claude` in an issue, issue comment, or pull request review comment |
| Where it runs | A GitHub Actions environment managed by GitHub | Your workflow's runner |
| Result | A `copilot/` branch and a (normally draft) pull request that requests your review | Replies on the issue or pull request, with commits pushed to a branch |
| Iterate | Comment on the pull request and mention `@copilot` | Comment again with `@claude` |
| Limits | One repository and one branch per task; 59 minutes; GitHub-hosted repositories only | Whatever the workflow allows: `--max-turns`, `timeout-minutes` |
| Billing | AI credits plus Actions minutes | API tokens (or your Claude subscription) plus Actions minutes |

---

## `copilot-setup-steps.yml`

A workflow that prepares the Copilot cloud agent's environment before it starts. Without it, the agent
spends part of its session working out how to install the .NET 10 SDK.

- Path: `.github/workflows/copilot-setup-steps.yml`, on the **default branch**.
- One job, named exactly **`copilot-setup-steps`**.
- Honored keys: `steps`, `permissions`, `runs-on`, `services`, `snapshot`, `timeout-minutes` (at most 59).
- Runners: Ubuntu x64 or Windows x64; the agent's firewall doesn't work on Windows or self-hosted runners.

```yaml
jobs:
  copilot-setup-steps:
    runs-on: ubuntu-latest
    timeout-minutes: 20
    permissions:
      contents: read
    steps:
      - uses: actions/checkout@v7
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: 10.0.x
      - run: dotnet build ToolsApp.slnx
```

Add `push` and `pull_request` triggers filtered to the file's own path, and it runs as a normal workflow
whenever you change it, so you can see that the setup works.

---

## The Claude Code GitHub Action

> **GitHub Copilot:** Not applicable; the Copilot cloud agent is built into GitHub and needs no workflow
> beyond `copilot-setup-steps.yml`.
>
> **Claude Code:** Run `/install-github-app` in Claude Code (github.com only; needs `gh` and repository
> admin rights) to install the Claude GitHub App, store the secret, and open a pull request with the
> workflow. Or do it by hand: install the app, add `ANTHROPIC_API_KEY` (API key) or
> `CLAUDE_CODE_OAUTH_TOKEN` (from `claude setup-token`, for a subscription), and add a workflow.

Inputs used in this demo's `claude.yml`:

| Input | Purpose |
|-------|---------|
| `anthropic_api_key` (or `claude_code_oauth_token`) | Authentication, from a repository secret |
| `claude_args` | Claude Code options: `--max-turns 30`, `--allowedTools "Bash(dotnet test *),..."`, `--model` |
| (no `prompt`) | Interactive mode: wait for `@claude`; with a `prompt`, it runs automatically on the event |

Also note the workflow `permissions` (`contents`, `pull-requests`, and `issues` write; `id-token: write` for
the app authentication; `actions: read` to see CI results), and that only users with write access can
trigger it.

---

## Issue forms for agent-ready tasks

An issue form (`.github/ISSUE_TEMPLATE/agent-task.yml`) turns the agent-ready structure into required
fields: Goal, Contract, Acceptance criteria, Constraints, Pointers. Both agents benefit, and so do human
contributors.

> **GitHub Copilot:** The form's text becomes the issue body that the cloud agent reads, together with the
> repository's instruction files.
>
> **Claude Code:** The action reads the issue and the repository's `CLAUDE.md`; keep `CLAUDE.md` concise
> because it is read on every run.

---

## Guardrails on the repository

The agent opens pull requests; the repository decides what can merge.

| Control | Setting |
|---------|---------|
| Require a pull request and a human approval | Ruleset or branch protection on the default branch |
| Require the build and tests | Required status check from your CI workflow |
| Route agent configuration to a maintainer | `CODEOWNERS` entries for `.github/workflows/`, `.github/agents/`, `.github/hooks/`, `.claude/` |
| Limit cost | `timeout-minutes`, `concurrency`, `--max-turns`; Copilot budgets and the AI credits paid-usage policy |

Copilot code review comments don't count toward required approvals, and by default workflows don't run on
the Copilot cloud agent's pull request until someone with write access approves them.

---

## Summary

- Assign an issue to the **Copilot cloud agent**, or mention **`@claude`**, and the result is a branch and a
  pull request.
- **`copilot-setup-steps.yml`** (default branch, one `copilot-setup-steps` job) installs .NET 10 before the
  agent starts.
- The **Claude Code GitHub Action** is configured by `claude.yml`; `/install-github-app` sets it up, and
  `claude_args` limits it.
- **Issue forms** keep tasks agent-ready; **rulesets, required checks, and CODEOWNERS** keep delegated work
  reviewable.
