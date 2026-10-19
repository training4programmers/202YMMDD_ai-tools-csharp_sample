# Connecting Agents to GitHub

An agent that can read your code but not your issues works half blind. It doesn't see the acceptance
criteria, the discussion on the issue, the open pull requests, or the workflow run that just failed. You
end up copying and pasting between the browser and the chat, and the agent works from whatever you
remembered to paste.

The **Model Context Protocol (MCP)** fixes that. MCP is an open standard that lets an AI client (Copilot
agent mode, Claude Code, Copilot CLI) call **tools** that an **MCP server** exposes: "read issue 42",
"list pull requests", "create a pull request". [Module 07](../module-07-agentic-coding/06-mcp-servers.md)
covers MCP in general and shows how to write your own server in C#. This chapter covers the server
GitHub ships for its own platform, the **GitHub MCP server**: the remote and local ways to run it, how
to narrow it with toolsets and read-only mode, how each client signs in, and how to keep its power
proportional to the task.

---

## One Server, Two Ways to Run It

GitHub publishes one MCP server, open source as `github/github-mcp-server`, that you can use in two
ways: a **remote** server GitHub hosts for you, and a **local** server you run on your machine.

![MCP clients on the left (VS Code, Visual Studio 2026, Copilot CLI, and Claude Code) connect either to the remote GitHub MCP server at api.githubcopilot.com/mcp, signing in with OAuth or a personal access token and choosing toolsets with the X-MCP-Toolsets header and read-only mode with X-MCP-Readonly, or to the local server, the ghcr.io/github/github-mcp-server container over stdio with a GITHUB_PERSONAL_ACCESS_TOKEN and the --toolsets and --read-only flags. Both act on GitHub issues, pull requests, repositories, Projects, Actions, and code security.](../../diagrams/png/13-github-mcp.png)

*Figure: One server, two ways to run it. Both act as you, against the same GitHub data; they differ in where they run and how they sign in.*

| | Remote server | Local server |
|---|---|---|
| **What it is** | A hosted endpoint at `https://api.githubcopilot.com/mcp/` | The container image `ghcr.io/github/github-mcp-server`, started by your client |
| **Transport** | Streamable HTTP | stdio (the client starts it as a child process) |
| **Needs** | Nothing to install | Docker Desktop (or the server's binary) |
| **Authentication** | OAuth in clients that support it; otherwise a personal access token (PAT) in an `Authorization` header | The server's own browser sign-in (OAuth) on first use, or a PAT in the `GITHUB_PERSONAL_ACCESS_TOKEN` environment variable, which takes precedence |
| **Scoping** | `X-MCP-Toolsets`, `X-MCP-Tools`, and `X-MCP-Readonly` headers, or URL forms | `--toolsets` and `--read-only` flags |
| **Updates** | GitHub updates it | You pull a new image tag |
| **Extras** | A few things exist only here: the `copilot_spaces` and `github_support_docs_search` toolsets, and the `create_pull_request_with_copilot` tool (handing a task to the cloud agent) | `--gh-host` points it at GitHub Enterprise Server or a GHE.com tenant |

**Teach and use the remote server first.** It has nothing to install, nothing to keep up to date, and
the IDEs sign in to it with your GitHub account. The remote server has been GA since September 2025.
Use the local container when your network blocks the hosted endpoint, when you're on GitHub Enterprise
Server, or when a lab needs a setup that works without the hosted service.

---

## Toolsets and Read-Only Mode

The server groups its tools into **toolsets**. The defaults cover everyday work: `context` (who you
are, including the `get_me` tool), `repos`, `issues`, `pull_requests`, and `users` at the time of
writing. Others you turn on when a task needs them include `actions`, `code_security`, `dependabot`,
`secret_protection`, `projects`, `labels`, `copilot`, `discussions`, and `orgs`.

| Toolset | Example tools | Kind |
|---------|---------------|------|
| `context` | `get_me` | Read |
| `issues` | `issue_read`, `list_issues`, `search_issues` | Read |
| | `issue_write` (create or update), `add_issue_comment`, `sub_issue_write` | Write |
| `pull_requests` | `pull_request_read`, `list_pull_requests`, `search_pull_requests` | Read |
| | `create_pull_request`, `update_pull_request`, `pull_request_review_write`, `merge_pull_request` | Write |
| `repos` | `get_file_contents`, `list_commits` | Read |
| | `create_branch`, `create_or_update_file`, `push_files`, `delete_file` | Write |
| | `create_repository`, `fork_repository`, `delete_repository` (repository-level) | Write |
| `copilot` | `request_copilot_review`, `assign_copilot_to_issue`; on the remote server also `create_pull_request_with_copilot` | Write |

Tool names change between versions, and clients add a prefix (Claude Code shows the server's tools as
`mcp__github__<tool>` when you name the server `github`), so read the tool list your client shows
rather than memorizing names.

### Choosing toolsets on the remote server

Two forms do the same job:

| To get | Header | URL |
|--------|--------|-----|
| Several toolsets | `X-MCP-Toolsets: context,issues,pull_requests,repos` | (use the header) |
| One toolset | `X-MCP-Toolsets: issues` | `https://api.githubcopilot.com/mcp/x/issues` |
| Defaults plus one more | `X-MCP-Toolsets: default,code_security` | (use the header) |
| Read-only, everything selected | `X-MCP-Readonly: true` | `https://api.githubcopilot.com/mcp/readonly` |
| One toolset, read-only | Both headers | `https://api.githubcopilot.com/mcp/x/issues/readonly` |

Two details catch people out. Naming toolsets **replaces** the defaults, so include `context` if you
want the agent to know who it's acting as, or write `default,...` to add to them. And read-only mode
removes the write tools from the list entirely; the agent can't call what it can't see, which is a
stronger guarantee than an approval prompt. A third header, `X-MCP-Tools`, adds individual tools by
name, for the occasional tool that isn't in a toolset by default.

On the **local** server, the `--toolsets` and `--read-only` flags (or their environment-variable
equivalents) do the same; the server's README has the current list.

---

## Authentication: How Each Client Signs In

| Client | Remote server | Local server |
|--------|---------------|--------------|
| **VS Code** | OAuth with your GitHub account, automatically | The server's browser sign-in, or a PAT prompted with an `inputs` entry |
| **Visual Studio 2026** | OAuth (the **Auth** CodeLens above the server in `mcp.json`), or a PAT header | The server's browser sign-in, or a PAT |
| **Copilot CLI** | Built in: the server ships with the CLI and uses its sign-in | Not needed |
| **Claude Code** | A fine-grained PAT in an `Authorization: Bearer` header (the documented path) | The server's browser sign-in, or a PAT |

The local server signs you in by itself: on github.com it opens a browser login the first time it starts
and keeps the token in memory only. In Docker, that needs the login's callback port published to
loopback (`127.0.0.1:8085` in the server's README examples). A PAT in `GITHUB_PERSONAL_ACCESS_TOKEN`
takes precedence over the browser sign-in, and GitHub Enterprise Server and GHE.com need an OAuth or
GitHub App of your own.

With the remote server, VS Code, Visual Studio 2026, and the Copilot CLI sign in with your GitHub
account and need no token. For Claude Code's
connection, use a **fine-grained personal access token**, not a classic one: GitHub **Settings** >
**Developer settings** > **Personal access tokens** > **Fine-grained tokens**. In class you create it on
Day 5, in step 2 of Demo 22 and Activity 22:

| Setting | In class | In your company |
|---------|----------|-----------------|
| **Resource owner** | The class organization | The organization that owns the repository |
| **Repository access** | **All repositories**: the class organization holds only lab repositories, and the token then covers the ones you create later | **Only select repositories**, with just the ones the task needs |
| **Permissions** | **Contents**, **Issues**, and **Pull requests**: read and write (**Metadata**: read is added automatically) | The same three, or fewer |
| **Expiration** | 7 days | The shortest your work allows |

If you're using the class fallback, a personal public repository, the resource owner is your own account
and the access is **Only select repositories** with just that repository. If an organization requires
approval for fine-grained tokens, an owner approves the token before it works (the class organization is
set not to require it), and on enterprises with managed user accounts, PATs are off unless an
enterprise owner allows them.

Keep the token out of files. Store it as a user environment variable named `GITHUB_PAT`, then open a new
terminal (and restart VS Code) so the variable is visible:

```powershell
$token = Read-Host 'Paste the token' -MaskInput; [Environment]::SetEnvironmentVariable('GITHUB_PAT', $token, 'User')
```

---

## Setting Up Each Client

### VS Code

The quickest way is the MCP registry: open the Extensions view, search `@mcp github`, select the GitHub
server, and choose **Install**. To share the configuration with your team, commit `.vscode/mcp.json`
instead. This version (Demo 22's) narrows the server to the toolsets an issue-to-pull-request session
needs; `copilot` is there only so the agent can request a Copilot code review, so drop it if you don't
need that:

```json
{
  "servers": {
    "github": {
      "type": "http",
      "url": "https://api.githubcopilot.com/mcp/",
      "headers": {
        "X-MCP-Toolsets": "context,issues,pull_requests,repos,copilot"
      }
    }
  }
}
```

There's no token in the file: VS Code signs in with OAuth the first time the server starts. Start it
from the file (or **MCP: List Servers** in the Command Palette), trust it when asked, pick **Agent** in
the chat view, and check the tool picker. On a Copilot Business or Enterprise seat, the organization's
**MCP servers in Copilot** policy must be enabled, or no MCP server loads at all.

### Visual Studio 2026

Visual Studio reads `.vscode\mcp.json` from the solution folder with the same `"servers"` key
(Module 07 lists every location it checks), so the file above works unchanged. To add the server through
the UI instead: in the chat pane switch to **Agent**, select the tools icon, then the **+** icon, and in
**Configure MCP server** enter `github` as the server ID, **HTTP/SSE** as the type, and
`https://api.githubcopilot.com/mcp/` as the URL. Visual Studio saves it to an `mcp.json` file; select
**Auth** in the CodeLens above the server to sign in. Newly discovered tools are **off** until you
enable them in the **Tools** picker, and Visual Studio asks for confirmation when a tool runs.

> **One folder, two formats.** Claude Code's project file is `.mcp.json` with the `"mcpServers"` key,
> while VS Code and Visual Studio use `"servers"`. Visual Studio also reads `<SolutionDir>\.mcp.json`
> and may warn about the other key. The arrangement that works for all three tools is the one the demos
> use: Visual Studio and VS Code configuration in `.vscode/mcp.json`, Claude Code configuration in
> `.mcp.json`.

### Claude Code

Put the server in `.mcp.json` at the repository root so the team shares it. Claude Code expands
`${GITHUB_PAT}` from its own environment when it starts, so the file holds only the variable's name:

```json
{
  "mcpServers": {
    "github": {
      "type": "http",
      "url": "https://api.githubcopilot.com/mcp/",
      "headers": {
        "Authorization": "Bearer ${GITHUB_PAT}",
        "X-MCP-Toolsets": "context,issues,pull_requests,repos,copilot"
      }
    }
  }
}
```

```powershell
# In a new terminal, after storing GITHUB_PAT (above):
claude
# then, in the session:
/mcp
```

Claude Code asks you to approve a project-scoped server the first time it starts, and `/mcp` shows its
status and tool count. To add the server for yourself only, from the command line:

```powershell
claude mcp add --transport http github https://api.githubcopilot.com/mcp/ -H "Authorization: Bearer $env:GITHUB_PAT"
claude mcp list
```

Notice that PowerShell expands `$env:GITHUB_PAT` before Claude Code sees it, so this form stores the
token itself in your user configuration (`~/.claude.json`, the **local** scope). That's acceptable on
your own machine; for anything shared, use the `.mcp.json` form above.

### GitHub Copilot CLI

The Copilot CLI ships with the GitHub MCP server already configured, signed in as you, with a default
subset of its tools. Run `/mcp` in a session to see it. Start the CLI with `--add-github-mcp-toolset
<name>` (repeatable) to add a toolset, or `--enable-all-github-mcp-tools` to load everything; the
narrower choice is usually the right one. `--disable-builtin-mcps` turns the built-in servers off.

### The local server, as a fallback

The local server runs as a container. A Claude Code entry that passes the token through from your
environment looks like this:

```json
{
  "mcpServers": {
    "github": {
      "type": "stdio",
      "command": "docker",
      "args": ["run", "-i", "--rm", "-e", "GITHUB_PERSONAL_ACCESS_TOKEN", "ghcr.io/github/github-mcp-server"],
      "env": {
        "GITHUB_PERSONAL_ACCESS_TOKEN": "${GITHUB_PAT}"
      }
    }
  }
}
```

In `.vscode/mcp.json`, use an `inputs` entry with `"type": "promptString"` and `"password": true` for
the token instead. To use the browser sign-in instead of a token, leave the token out and add the
callback-port settings (`-p 127.0.0.1:8085:8085` and `GITHUB_OAUTH_CALLBACK_PORT`) from the server's
README. A server is code you run (Module 07), so pin an image tag rather than taking
whatever was published last, and bump it deliberately.

---

## Least Privilege, in Layers

The GitHub MCP server **acts as you**. Whatever you can do on GitHub, an agent with the server can
attempt, at machine speed, steered by whatever text it last read, including the body of an issue a
stranger opened. Four layers keep that in proportion:

1. **The identity.** The server never exceeds the signed-in user's permissions. A lab account with
   access to one repository limits the damage far better than any setting.
2. **The token.** A fine-grained PAT limited to **one resource owner**, the repositories the task needs,
   and **three permissions** (Issues, Pull requests, Contents) can't touch other organizations, your
   settings, or your workflows, whatever the agent is talked into.
3. **The server's tool list.** Load only the toolsets the task needs, and use read-only mode for
   sessions that should only read. A refinement session needs `context` and `issues`; it doesn't need
   `actions` or `code_security`.
4. **Client approvals.** The write tools the task uses ask before they run; the ones it never needs,
   including the repository-level ones, are denied.

The fourth layer is configured per tool. In Claude Code, permission rules allow the read tools, ask for
the write tools the task uses, and deny the write tools it never needs. These are the MCP rules in
Demo 22's `.claude/settings.json`, for a session with the `context`, `issues`, `pull_requests`, `repos`,
and `copilot` toolsets:

```json
{
  "permissions": {
    "allow": [
      "mcp__github__get_me",
      "mcp__github__issue_read",
      "mcp__github__list_issues",
      "mcp__github__search_issues",
      "mcp__github__pull_request_read",
      "mcp__github__list_pull_requests",
      "mcp__github__search_pull_requests",
      "mcp__github__get_file_contents"
    ],
    "ask": [
      "mcp__github__issue_write",
      "mcp__github__sub_issue_write",
      "mcp__github__add_issue_comment",
      "mcp__github__update_issue_comment",
      "mcp__github__create_branch",
      "mcp__github__create_pull_request",
      "mcp__github__update_pull_request",
      "mcp__github__update_pull_request_branch",
      "mcp__github__pull_request_review_write",
      "mcp__github__add_comment_to_pending_review",
      "mcp__github__add_reply_to_pull_request_comment",
      "mcp__github__request_copilot_review"
    ],
    "deny": [
      "mcp__github__merge_pull_request",
      "mcp__github__push_files",
      "mcp__github__create_or_update_file",
      "mcp__github__delete_file",
      "mcp__github__create_repository",
      "mcp__github__delete_repository",
      "mcp__github__fork_repository",
      "mcp__github__assign_copilot_to_issue",
      "mcp__github__create_pull_request_with_copilot"
    ]
  }
}
```

The demo's file also puts `git push` under `ask`, so a person sees every push. Claude Code checks `deny`
first, then `ask`, then `allow`, so a tool in two lists gets the stricter answer. Write tools don't share
a naming pattern (`issue_write`, but also `push_files` and `merge_pull_request`), so go through the full
tool list for every toolset you load. The `repos` toolset brings `push_files`, `create_or_update_file`,
and `delete_file`, which commit straight to a branch through the API, around your local build and tests;
deny them, because code should arrive through `git push` after `dotnet test` passes. It also brings the
repository-level tools `create_repository`, `delete_repository`, and `fork_repository`, which no
issue-to-pull-request session needs, so deny those too. A person merges, so `merge_pull_request` is
denied as well. And the `copilot` toolset, loaded here only so the agent can call
`request_copilot_review`, also brings `assign_copilot_to_issue` and, on the remote server,
`create_pull_request_with_copilot`, both of which hand work to the cloud agent; deny them unless that's
the task.

> **GitHub Copilot:** In VS Code, the agent's permission level decides how often you're asked: keep
> **Manual permissions** for sessions with GitHub write tools, because **Allow all** runs tools without
> asking, and untick the tools the task never needs in the tool picker (the same list as the `deny`
> rules above). In Visual Studio, approve write
> tools one call at a time rather than for the session or solution. Organization admins control MCP use
> with the **MCP servers in Copilot** policy and can restrict which servers are allowed.
>
> **Claude Code:** `allow`, `ask`, and `deny` rules in `.claude/settings.json` match MCP tools by name,
> and `mcp__github__*` matches every tool from the server. Put the write tools the task uses under
> `ask`, `deny` the ones it never needs, and remember that a more permissive permission mode changes how often you're
> asked. Managed settings let administrators restrict which servers people may add
> (Module 14).

A good default: **start every GitHub session read-only** and turn on write tools for the task that needs
them. Reading the backlog, a pull request's review threads, or a failed workflow run is where most of the
value is anyway.

---

## Verify the Connection

Ask something only GitHub can answer, and check the answer in the browser:

```text
Using the GitHub MCP server: who am I signed in as, and what open issues and pull requests does
<owner>/<repo> have? Don't change anything.
```

The agent should call `get_me` first, then `list_issues` and `list_pull_requests`, without asking
(they're read tools). If `get_me` isn't available, you named toolsets without `context`.

| Symptom | Likely cause |
|---------|--------------|
| No GitHub tools in the picker | The server didn't start or isn't trusted: check `/mcp` (Claude Code and Copilot CLI) or **MCP: List Servers** (VS Code); on a Business or Enterprise seat, check the **MCP servers in Copilot** policy |
| `401 Unauthorized` (Claude Code) | The terminal that started `claude` can't see `GITHUB_PAT` (open a new terminal after storing it), or the token expired |
| Tools work but return nothing, or `404` for the repository | The token isn't scoped to that repository, the organization hasn't approved it yet, or the agent guessed the owner; name `<owner>/<repo>` in the prompt |
| `403` on a write | The token lacks that permission (for example **Issues: Read and write**), which may be exactly what you intended |
| A write tool is missing | You filtered it out with toolsets or read-only mode, which is often what you want |

---

## Summary

- The **GitHub MCP server** gives an agent your issues, pull requests, repositories, Projects, Actions,
  and code security as tools. Use the **remote** server (`https://api.githubcopilot.com/mcp/`, GA) first,
  and the **local** container (`ghcr.io/github/github-mcp-server`, stdio) as the fallback.
- **Toolsets** narrow what it exposes: the `X-MCP-Toolsets` header (several) or `/x/{toolset}` (one) on
  the remote server, `--toolsets` locally. Naming toolsets **replaces the defaults**, so include
  `context`. **Read-only** mode (`X-MCP-Readonly: true`, a `/readonly` URL, or `--read-only`) removes
  write tools entirely.
- **VS Code** signs in with **OAuth**; **Visual Studio 2026** uses OAuth or a PAT header and reads
  `.vscode/mcp.json`; **Copilot CLI** has the server **built in**; **Claude Code** uses a **fine-grained
  PAT** in an `Authorization: Bearer ${GITHUB_PAT}` header in `.mcp.json`, or `claude mcp add --transport
  http`.
- The server **acts as you**: limit the **identity**, scope the **token** to one resource owner and the
  **Issues, Pull requests, and Contents** permissions, load only the **toolsets** you need, put the
  **write tools the task uses on approval**, and **deny** the rest: API commits, merges, the
  repository-level tools, and handing work to the cloud agent.
- On Copilot Business and Enterprise, the **MCP servers in Copilot** policy must be on. Check the
  connection with a read-only prompt that calls **`get_me`**.
