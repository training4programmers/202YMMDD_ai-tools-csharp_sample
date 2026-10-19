# Demo 22: Tools Review

A quick reference to the **AI-tool features this demo introduces**: connecting an agent to GitHub
through the GitHub MCP server, controlling which of its tools run without asking, creating issues and
linking them to branches and pull requests from the IDE, steering Copilot code review with instruction
files, and the agent-review fallback when Copilot code review isn't available. The concepts are
tool-neutral and come from Module 13; the GitHub Copilot and Claude Code specifics below are a
snapshot at the time of writing. Menus and settings move often, so the vendors' docs are the authority.

---

## Adding the GitHub MCP server

GitHub hosts the **remote** server at `https://api.githubcopilot.com/mcp/`, so there's nothing to
install. Each tool keeps its configuration in a different file, so a repository that serves both tools
carries two small files, and neither holds a token.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **File** | `.vscode/mcp.json`, top-level key `"servers"`; read by VS Code and by Visual Studio 2026 | `.mcp.json` at the repository root, top-level key `"mcpServers"` (project scope), or your local scope |
| **Server entry** | `"type": "http"` and the URL | `"type": "http"` and the URL |
| **Sign-in** | OAuth with your GitHub account, when the server first starts | A fine-grained personal access token in an `Authorization: Bearer ${GITHUB_PAT}` header, expanded from the environment |
| **Toolsets** | The `X-MCP-Toolsets` header | The same header |
| **Check it** | **MCP: List Servers** and the tool picker (VS Code); the agent-mode **Tools** picker (Visual Studio) | `/mcp` in a session; `claude mcp list` |
| **Watch out** | Visual Studio disables newly discovered tools until you enable them | A project-scoped server asks each person for approval the first time it starts |

```json
"headers": {
  "Authorization": "Bearer ${GITHUB_PAT}",
  "X-MCP-Toolsets": "context,issues,pull_requests,repos,copilot"
}
```

Naming toolsets **replaces the defaults** (`context`, `repos`, `issues`, `pull_requests`, `users`), so
the demo lists `context` to keep `get_me`, and adds `copilot` only for `request_copilot_review`.
`X-MCP-Readonly: true` (or a URL ending in `/readonly`) loads read tools only, which suits a session
that only refines issues.

> **GitHub Copilot:** the Copilot CLI ships with the GitHub MCP server built in, signed in as you. In
> VS Code, if you'd rather use a token than OAuth, put it in an `inputs` entry with `"password": true`
> and reference it as `${input:<id>}` in the header, never as a literal.
>
> **Claude Code:** `claude mcp add --transport http github https://api.githubcopilot.com/mcp/ -H
> "Authorization: Bearer $env:GITHUB_PAT"` works too, but PowerShell expands the variable before
> Claude Code sees it, so the token itself lands in your local configuration. The committed `.mcp.json`
> keeps only `${GITHUB_PAT}`. Store the token once as a user environment variable, typed with
> `Read-Host -MaskInput` so it stays out of your shell history, then open a new terminal (and restart
> VS Code):
>
> ```powershell
> $token = Read-Host 'Paste the token' -MaskInput
> [Environment]::SetEnvironmentVariable('GITHUB_PAT', $token, 'User')
> ```
>
> For the class, make it a fine-grained token with the class organization as resource owner, **All
> repositories** (the organization holds only lab repositories), **Contents**, **Issues**, and **Pull
> requests** read and write, and a 7-day expiry.

**The local fallback.** If your network blocks the remote server, or you work against GitHub
Enterprise Server, run the same server locally from the `ghcr.io/github/github-mcp-server` Docker image
over stdio, with the token in `GITHUB_PERSONAL_ACCESS_TOKEN` and `--toolsets` (and `--read-only`) as
flags; `--gh-host` points it at another GitHub host.

---

## Deciding which tools run without asking

The server acts as you, so the client's approval settings are the last line of defense. The rule is
simple: auto-approve reads, ask first for the writes the task uses, and deny the writes it never needs,
including the repository-level ones (creating, deleting, and forking repositories). GitHub's tool
names don't share a write suffix (`issue_write`, but also `create_branch`, `create_pull_request`,
`add_issue_comment`, and `request_copilot_review`), so check the tool list your client shows for the
toolsets you load.

> **GitHub Copilot:** In Visual Studio 2026, the **Confirm** dropdown can remember an approval for the
> session, the solution, or always; approve write tools one call at a time, and leave the denied tools
> switched off in the tool picker. In VS Code, use the **Manual permissions** level for this demo and
> untick `merge_pull_request`, `push_files`, `create_or_update_file`, `delete_file`,
> `create_repository`, `delete_repository`, `fork_repository`, `assign_copilot_to_issue`, and
> `create_pull_request_with_copilot` in the tool picker.
>
> **Claude Code:** permission rules in `.claude/settings.json` match MCP tools as
> `mcp__<server>__<tool>`. The demo's file puts the read tools under `allow`; the write tools the task
> uses (issues, comments, the branch, the pull request, reviews), plus `git push`, under `ask`; and
> under `deny`, the ones it never needs: `merge_pull_request` (a person merges), the three tools that
> commit files through the API (code should arrive through `git push`, after your local build and
> tests), the repository-level `create_repository`, `delete_repository`, and `fork_repository`, and
> `assign_copilot_to_issue` and `create_pull_request_with_copilot` (the cloud agent is Bonus Demo 04's
> subject). Anything not listed asks by default, so the `ask` list documents intent and the `deny` list
> is the hard limit.

```json
{
  "permissions": {
    "allow": ["mcp__github__get_me", "mcp__github__issue_read", "mcp__github__list_issues"],
    "ask": ["mcp__github__issue_write", "mcp__github__create_pull_request"],
    "deny": ["mcp__github__merge_pull_request", "mcp__github__push_files"]
  }
}
```

(An excerpt; the solution's file lists every read, write, and denied tool.)

---

## Issues and links from the IDE

With the issue, pull request, and repository toolsets loaded, the same session that writes the code can
create the issue and the pull request, and the links follow from a few conventions. Name the repository
in the prompt, and ask to see the title and body before any write.

| | GitHub Copilot | Claude Code |
|---|---|---|
| **Where** | Agent mode in VS Code or Visual Studio 2026 | A session in the lab folder |
| **Plan first** | The Plan agent (VS Code, VS 2026), then Agent | Plan mode (Shift+Tab, or `/plan`), then normal mode |
| **Create** | `issue_write` (labels included), `create_pull_request` | The same tools, prefixed `mcp__github__` |
| **Branch** | `gh issue develop <number> --checkout` creates a branch the issue's **Development** panel lists | Same |
| **Commits** | `(#<number>)` at the end of the message shows the commit in the issue's timeline | Same |
| **Pull request** | `Fixes #<number>` in the description links it in the **Development** panel and closes the issue on merge to the default branch | Same |

Labels must exist before an agent can apply them reliably: create them once with `gh label create`.

---

## Copilot code review on GitHub

Copilot joins a pull request as a reviewer named **Copilot** and leaves comments, often with suggested
changes you can commit from the pull request. Its review is a comment, not an approval, so it doesn't
satisfy a required approval. Request it from **Reviewers** > **Copilot**, or have the agent call
`request_copilot_review`. To make it automatic, add a branch ruleset with **Automatically request
Copilot code review**; **Review new pushes** makes it review again after each push. It needs a paid
Copilot plan, uses GitHub AI Credits, and on private repositories each review also uses GitHub Actions
minutes. A separate setting that lets Copilot approve pull requests is a public preview, off by
default: leave it off.

| | Instruction files it reads |
|---|---|
| **Repository** | `.github/copilot-instructions.md`, and `AGENTS.md` |
| **Path** | `.github/instructions/*.instructions.md`, matched to changed files by `applyTo` |
| **Opt a file out** | `excludeAgent: "code-review"` in a path file's front matter |
| **Which branch** | The pull request's **head** branch, so a pull request that changes the instructions is reviewed with the new ones |

The same files steer GitHub Copilot chat in Visual Studio 2026 and VS Code, so the demo's API rules
shape both the code Copilot writes and the review it gives.

---

## The fallback: an agent review in a fresh session

When Copilot code review isn't available (no paid Copilot plan, or a policy that turns it off), review the
diff with an agent that didn't write it. A fresh session doesn't share the implementing session's
assumptions.

> **GitHub Copilot:** start a new chat (VS Code) or thread (Visual Studio 2026) and ask for a review of
> `git diff main...HEAD` against the instruction files and the issue copy; `.github/` instructions load
> automatically, but name them in the prompt anyway so the review uses them as its rubric.
>
> **Claude Code:** `/clear` (or a new terminal session), then the same prompt. Claude Code doesn't load
> `.github/copilot-instructions.md` on its own, so the prompt must name it. Ask for findings with file
> and line, and no edits.

---

## Summary

- The **remote GitHub MCP server** (`https://api.githubcopilot.com/mcp/`) works with VS Code, Visual
  Studio 2026, the Copilot CLI, and Claude Code: `.vscode/mcp.json` (`"servers"`, OAuth) or `.mcp.json`
  (`"mcpServers"`, a token from `${GITHUB_PAT}`). `X-MCP-Toolsets` limits it to the **toolsets** you
  need; list `context` for `get_me`. The local Docker image is the fallback.
- **Auto-approve read tools, ask first for the writes the task uses, deny the ones it never needs**
  (merging, committing files through the API, the repository-level tools, and handing work to the
  cloud agent): Visual Studio's **Confirm** options and tool picker, VS Code's prompts and tool picker,
  Claude Code's `allow`/`ask`/`deny` rules on `mcp__github__*`.
- One agent session can **create the issue and the pull request**; `gh issue develop`, `(#<number>)`,
  and `Fixes #<number>` do the linking, and the issue's **Development** panel shows it.
- **Copilot code review** is requested in **Reviewers** (or by `request_copilot_review`), automated with
  a **branch ruleset**, steered by `.github/` instruction files read from the **head branch**, and only
  ever comments.
- The **fallback** is a **fresh agent session** reviewing the diff against the same instruction files.
