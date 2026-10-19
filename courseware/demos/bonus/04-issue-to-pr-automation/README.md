# Bonus Demo 04: Issue-to-PR Automation (ToolsApp / Colors)

Instructor demo paired with [Bonus Activity 04](../../../activities/bonus/04-issue-to-pr-automation/). A
well-written GitHub issue goes to a cloud agent (the GitHub Copilot cloud agent, or Claude Code through
its GitHub Action), and the result comes back as a pull request that the instructor reviews like any
other.

## Concept

- A **cloud agent** runs the same agentic loop on a GitHub-hosted runner: it reads the issue, works on a
  branch, and opens a pull request. Nobody watches it work, so the **issue is the specification**.
- An **agent-ready issue** has a goal, an exact contract, checkable acceptance criteria, constraints, and
  pointers; an **issue form** keeps every task in that shape.
- **`copilot-setup-steps.yml`** prepares the Copilot cloud agent's environment (.NET 10 SDK, restore, build)
  so it spends its session on the task.
- The **Claude Code GitHub Action** responds to `@claude` in issues and pull requests, configured by
  `claude.yml`.
- The pull request gets a **normal review**, protected by branch rules and required checks.

![The agentic loop: a goal with acceptance criteria feeds Plan, then Act, Observe, and a Done decision; failures loop back to Plan, and all-green work goes to human review.](../../../diagrams/png/07-agentic-loop.png)

*Figure: A cloud agent runs the same loop on a GitHub runner; the human review step is the pull request.*

## How to run

From the course repo root, copy the starting point to a lab folder outside the repo (so the repo's own
AGENTS.md/CLAUDE.md don't leak into the session) and open it:

```powershell
.\courseware\utils\start-lab.ps1 -Demo 04 -Bonus -Open
cd ~\labs\bonus-demo-04-issue-to-pr-automation
dotnet build
dotnet test --solution ToolsApp.slnx
```

This demo needs your own GitHub account and repository:

- **Copilot cloud agent**: a paid Copilot plan (on by default for Pro and Pro+; a Business or Enterprise
  administrator must enable it). It works only with repositories hosted on GitHub.
- **Claude Code GitHub Action**: admin rights on the repository, the Claude GitHub App, and an
  `ANTHROPIC_API_KEY` (or `CLAUDE_CODE_OAUTH_TOKEN`) repository secret. `/install-github-app` in Claude Code
  sets these up and needs the GitHub CLI (`gh`) signed in.
- Copy the recorded outcome too, because step 2 takes the workflow files from it:
  `.\courseware\utils\start-lab.ps1 -Demo 04 -Bonus -Solution`.

A cloud agent run takes 10 to 30 minutes. Start it before a break, or assign the issue before class and
walk through the finished pull request. If the run goes sideways, the solution copy is the recorded result
of the agent's pull request.

## Demo trail

**Code to open (in order):**

1. `ISSUE.md`: the goal, the exact tints rule (`k / (count + 1)`, midpoints away from zero), the default and
   range of `count`, every error status, the acceptance criteria, and the pointers. Ask the audience what
   an agent would have to guess if any line were missing.
2. `AGENTS.md` and `CLAUDE.md`: both cloud agents read the repository's instruction files.
3. `src/ToolsApp.Api/Endpoints/ColorEndpoints.cs`: `GetContrastAsync`, the pattern the issue points to.

**Step 1: Push the lab to your own repository.**

```powershell
gh repo create <your-account>/toolsapp-agent-demo --private --source . --push
```

**Step 2: Add the agent configuration on the default branch.** Copy the workflow files and the issue
form from the solution copy, then commit and push:

```powershell
$sol = "$HOME\labs\bonus-demo-04-issue-to-pr-automation-solution"
Copy-Item "$sol\.github\workflows" .github\ -Recurse -Force
Copy-Item "$sol\.github\ISSUE_TEMPLATE" .github\ -Recurse -Force
git add .github
git commit -m "Add cloud agent setup, Claude workflow, and agent-task issue form"
git push
```

Walk through `.github/workflows/copilot-setup-steps.yml` (default branch only, the job must be named
`copilot-setup-steps`, .NET 10 with `actions/setup-dotnet`) and show it running in the **Actions** tab,
because it triggers on changes to itself. Then `claude.yml`: the `@claude` condition, the permissions,
`--max-turns`, the allowed `dotnet` commands, and `concurrency`. Add the secret (or run
`/install-github-app` from Claude Code in this folder):

```powershell
gh secret set ANTHROPIC_API_KEY      # paste the key when prompted; never commit it
```

**Step 3: Create the issue.** Either use the new **Agent-ready task** form on GitHub, or create it from the
file:

```powershell
gh issue create --title "Add tints for a stored color" --body-file ISSUE.md
```

**Step 4: Delegate it.** For Copilot, open the issue and choose **Assignees** > **Copilot**; optionally add
a note such as "Follow AGENTS.md; run dotnet test before finishing." For Claude, comment on the issue:

```text
@claude Implement this issue. Follow AGENTS.md, run dotnet build and dotnet test --solution ToolsApp.slnx
before you finish, and open a pull request that lists how each acceptance criterion is met.
```

Show where to follow progress: the Copilot session log linked from the pull request, or the Actions run
and the comment Claude keeps updating.

**Step 5: Review the pull request like any other.** Go through the acceptance criteria one by one, read the
whole diff, and run it locally:

```powershell
gh pr checkout <number>
dotnet build
dotnet test --solution ToolsApp.slnx
```

Typical review points: does it use decimal or integer arithmetic for the rounding as the issue requires,
does it test `count=0`, `count=11`, and the default of five, and did it add the `.http` request? Request
one change in a review comment (mention `@copilot` or `@claude`), for example: "Add a test that count=1 on
#FF0000 gives #FF8080; 127.5 must round up." Watch the agent push a follow-up commit.

**Step 6: Show the guardrails.** In the repository settings, show a ruleset (or branch protection) on the
default branch that requires a pull request, one approving review, and a passing build-and-test check.
The agent can open pull requests; only a person can merge them.

**Key teaching beat:** With a cloud agent you trade live supervision for a better specification and a
better review. Everything the agent needed was in the issue and the repository, so the quality of the pull
request was decided before it started; the review decides whether it ships.

## Reference

- Concepts: [Module 07: Agentic Coding](../../../docs/module-07-agentic-coding/):
  [Issue to Pull Request: Cloud Agents on GitHub](../../../docs/module-07-agentic-coding/07-issue-to-pr-automation.md)
- Tools: [Tools Review](TOOLS-REVIEW.md)
- Practice: [Bonus Activity 04](../../../activities/bonus/04-issue-to-pr-automation/)
