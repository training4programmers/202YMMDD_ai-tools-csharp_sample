# MCP Servers: Connecting Agents to Internal Tools and Data

Out of the box, a coding agent can see your files and run your terminal. Much of what you need to finish a
task lives elsewhere: the issue that explains the bug, the internal service that knows which customers
use a feature, the database that holds the reference data. The **Model Context Protocol (MCP)** is the open
standard that lets an agent reach those systems through small adapter programs called **MCP servers**.

This chapter explains what MCP is and how its pieces fit together, how to register servers in each tool,
how to build your own stdio server in C# with the official SDK, and how to keep MCP from becoming the
easiest way to leak data or hand an attacker your agent.

---

## What MCP is

MCP defines how an AI application talks to external capabilities. There are three roles:

- The **host** is the application you use: VS Code, Visual Studio, the Copilot CLI, or Claude Code.
- The host runs one **MCP client** per server it connects to.
- An **MCP server** is a program that exposes capabilities. It can be a local process or a remote web
  service.

Messages are **JSON-RPC 2.0** (a simple request/response format in JSON) over one of two **transports**:

- **stdio**: the host starts the server as a child process and exchanges messages over its standard input
  and output. Used for local servers.
- **Streamable HTTP**: the server is a web endpoint, usually behind OAuth. Used for remote and shared
  servers. (The older SSE transport is deprecated.)

![A host box (VS Code, Visual Studio, Copilot CLI, or Claude Code) contains the model and agent loop, an MCP client, and the server configuration files. The client connects over stdio to a local server, ToolsApp.Mcp, which exposes find_colors and check_contrast and reads toolsapp.db read-only, and over HTTPS with OAuth to the remote GitHub MCP server (api.githubcopilot.com/mcp). A banner says the agent decides when to call a tool, and you decide which servers it may use.](../../diagrams/png/07-mcp-architecture-colors.png)

*Figure: MCP architecture. The agent decides when to call a tool; you decide which servers it may use.*

A server can offer three kinds of capability:

| Capability | What it is | Example |
|------------|------------|---------|
| **Tools** | Functions the model can call, each with a name, description, and JSON schema for its inputs | `find_colors(query)`, `check_contrast(foregroundHex, backgroundHex)` |
| **Resources** | Data the client can attach to the context, addressed by URI | An issue, a schema, a design spec |
| **Prompts** | Reusable prompt templates the user can pick | "Review this pull request" |

Tools are by far the most widely supported, and the rest of this chapter focuses on them. The current
version of the MCP specification is dated 2026-07-28.

---

## Registering servers in each tool

Every host keeps a list of servers in a JSON file. The shapes are close but not identical, which is the
most common source of "my server doesn't show up". The vendor pages are
[Claude Code MCP](https://code.claude.com/docs/en/mcp),
[MCP servers in VS Code](https://code.visualstudio.com/docs/agent-customization/mcp-servers), and
[MCP servers in Visual Studio](https://learn.microsoft.com/en-us/visualstudio/ide/mcp-servers).

| | GitHub Copilot | Claude Code |
|---|---|---|
| Project file | `.vscode/mcp.json` (key `servers`) for VS Code; Visual Studio reads `<SolutionDir>\.mcp.json` first, then `.vscode\mcp.json` (see the note below) | `.mcp.json` at the project root (key `mcpServers`) |
| Personal file | VS Code user configuration; `%USERPROFILE%\.mcp.json` for Visual Studio; `~/.copilot/mcp-config.json` for the CLI | `~/.claude.json` (the `local` and `user` scopes) |
| Add from the command line | `/mcp add` in the Copilot CLI | `claude mcp add` |
| Check status | **MCP: List Servers** (VS Code); the Tools picker (Visual Studio); `/mcp` (CLI) | `/mcp` in a session; `claude mcp list` |
| First use | Trust prompt; tools must be enabled in the tools picker | Approval prompt for project servers |

### Claude Code: `.mcp.json`

```json
{
  "mcpServers": {
    "toolsapp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "${CLAUDE_PROJECT_DIR:-.}/src/ToolsApp.Mcp", "-c", "Release"],
      "env": {
        "DOTNET_NOLOGO": "1"
      }
    }
  }
}
```

- From the command line, `--scope project` writes an entry to `.mcp.json` (stdio is the default
  transport; everything after `--` is the command). Then edit the path in `args` to start with
  `${CLAUDE_PROJECT_DIR:-.}` as above, the form Claude Code's documentation gives for project files:

  ```powershell
  claude mcp add --scope project toolsapp -- dotnet run --project src/ToolsApp.Mcp -c Release
  ```

- `${VAR}` and `${VAR:-default}` are expanded in `command`, `args`, `env`, `url`, and `headers`, using
  Claude Code's own environment. Claude Code sets `CLAUDE_PROJECT_DIR` only in the **server's**
  environment, not its own, which is why a project file needs the default. In practice that means the `.`
  fallback is what normally applies, and `./src/ToolsApp.Mcp` resolves against the folder you started
  `claude` in. So **start `claude` from the solution root**. (The server process itself can still read
  `CLAUDE_PROJECT_DIR` at run time to find project files.)
- The first time a session uses a project server, Claude Code asks you to approve it. `claude mcp
  reset-project-choices` clears those answers.
- Tools appear to the model and in permission rules as `mcp__toolsapp__find_colors`, so you can pre-approve
  read-only tools in `.claude/settings.json`:

  ```json
  { "permissions": { "allow": ["mcp__toolsapp__find_colors", "mcp__toolsapp__check_contrast"] } }
  ```

### GitHub Copilot: `.vscode/mcp.json`

```json
{
  "servers": {
    "toolsapp": {
      "type": "stdio",
      "command": "dotnet",
      "args": ["run", "--project", "${workspaceFolder}/src/ToolsApp.Mcp", "-c", "Release"],
      "env": {
        "DOTNET_NOLOGO": "1"
      }
    }
  }
}
```

> **GitHub Copilot:** In **VS Code**, save the file, confirm that you trust the server when prompted, and
> check it with **MCP: List Servers**; **Configure Tools** in the chat input turns individual tools on and
> off. Use an `inputs` entry with `"type": "promptString"` for secrets instead of writing them in the file.
>
> **Currency note (at the time of writing):** `.vscode/mcp.json` still works, but VS Code's guided
> add-server flow now labels it deprecated, kept for compatibility. The team-sharing destination that flow
> offers instead is a workspace-root `.mcp.json` in the portable `mcpServers` format, and only when the
> **experimental** `chat.mcp.workspaceRootConfig.enabled` setting is on; portable files don't support
> `inputs`. This course keeps `.vscode/mcp.json` so the Claude Code file and the VS Code file stay
> separate. If you turn the setting on while both files define the same server, check **MCP: List
> Servers** for a duplicate and disable one copy.
>
> In **Visual Studio 2026**, servers are discovered from, in order, `%USERPROFILE%\.mcp.json`,
> `<SolutionDir>\.vs\mcp.json`, `<SolutionDir>\.mcp.json`, `<SolutionDir>\.vscode\mcp.json`, and
> `<SolutionDir>\.cursor\mcp.json`, and the documented format uses the `servers` key. That order matters
> in a repository that also serves Claude Code: Visual Studio reaches Claude Code's `.mcp.json` (key
> `mcpServers`) before `.vscode\mcp.json`, and it doesn't expand `${CLAUDE_PROJECT_DIR:-.}`, so a server
> registered in both files may not start in Visual Studio. The dependable route is **Tools** > **+** >
> **Add custom MCP server** with an absolute path to the project and the **Global** destination
> (`%USERPROFILE%\.mcp.json`; the **Solution** destination writes a `.mcp.json` into the solution
> folder, Claude Code's file). Tools are **off by default**: enable them in the agent-mode **Tools**
> picker. From 18.7, Visual Studio asks you to trust a server again when its configuration or tools
> change.
>
> **Claude Code:** Reads `.mcp.json` at the project root. A file using the `servers` key won't load; it must
> be `mcpServers`.

The Copilot cloud agent has its own MCP settings (repository **Settings** > **Copilot** > **MCP servers**,
`mcpServers` format, tools only, secrets prefixed `COPILOT_MCP_`).

---

## Building a stdio MCP server in C#

The official [C# SDK](https://github.com/modelcontextprotocol/csharp-sdk) is the `ModelContextProtocol`
package, which reached 1.0 in February 2026 and is at 2.2.0 at the time of writing. A stdio server is a
console app that uses the generic host; Microsoft Learn's
[MCP server quickstart](https://learn.microsoft.com/en-us/dotnet/ai/quickstarts/build-mcp-server) walks
through the same steps.

### Project and packages

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Extensions.Hosting" />
    <PackageReference Include="ModelContextProtocol" />
  </ItemGroup>
</Project>
```

(Versions come from `Directory.Packages.props`.) You can also scaffold a server with the project template:
`dotnet new install Microsoft.McpServer.ProjectTemplates`, then `dotnet new mcpserver -n MyServer`. The
template pins an older SDK version, so update `ModelContextProtocol` after you create the project.

### Program.cs

```csharp
var builder = Host.CreateApplicationBuilder(args);

// stdout carries the MCP protocol messages, so every log line must go to stderr instead.
builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton<IColorCatalog>(services =>
    new SqliteColorCatalog(ToolsAppDatabase.GetConnectionString(services.GetRequiredService<IConfiguration>())));

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<ColorTools>();

await builder.Build().RunAsync();
```

The logging line is not optional. Anything a stdio server writes to stdout is read as protocol; a stray
`Console.WriteLine` or a default console log line corrupts the stream and the client drops the connection.

### Tools are annotated methods

```csharp
[McpServerToolType]
public sealed class ColorTools(IColorCatalog catalog)
{
    [McpServerTool(Name = "find_colors", ReadOnly = true, Idempotent = true, OpenWorld = false)]
    [Description("Finds stored ToolsApp colors whose name, hex value, or palette name contains the query text " +
                 "(case-insensitive). Returns at most 25 matches with id, name, #RRGGBB hex, and palette.")]
    public async Task<IReadOnlyList<ColorMatch>> FindColorsAsync(
        [Description("Text to look for, for example 'sea', '#00A6', or 'Sunset'.")] string query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            throw new McpException("query must not be empty.");
        }

        return await catalog.SearchAsync(query.Trim(), 25, cancellationToken);
    }
}
```

What the SDK does with that:

- Constructor parameters are resolved from dependency injection, so tools are easy to test with a fake.
- Each `[Description]` becomes part of the schema the model reads. **Write descriptions for the model**: what
  the tool does, what it returns, limits, and what it doesn't do.
- `CancellationToken` is supplied by the SDK and doesn't appear in the schema.
- The return value is serialized to JSON for the model.
- `ReadOnly`, `Idempotent`, and `OpenWorld` become **annotations** (`readOnlyHint` and so on). They are hints
  to the client, not enforcement.

This is the `tools/list` response the server above produces (captured from a real run, shortened):

```json
{"name":"find_colors",
 "description":"Finds stored ToolsApp colors whose name, hex value, or palette name contains the query text (case-insensitive). ...",
 "inputSchema":{"type":"object","properties":{"query":{"description":"Text to look for, for example 'sea', '#00A6', or 'Sunset'.","type":"string"}},"required":["query"]},
 "annotations":{"idempotentHint":true,"openWorldHint":false,"readOnlyHint":true}}
```

### Errors the model can act on

How a tool fails decides whether the agent can recover:

```text
throw new McpException("query must not be empty.");
  -> isError: true, "An error occurred invoking 'find_colors': query must not be empty."

throw new InvalidOperationException("...");
  -> isError: true, "An error occurred invoking 'find_colors'."      (details only in the server log)
```

Throw `McpException` for anything the model can fix, with a message that says how ("No ToolsApp database
at '...'. Run the API once (dotnet run --project src/ToolsApp.Api) to create it."). Let unexpected
exceptions stay generic, so internal details don't leak to the model.

### Complex parameters

Records work as parameters; put descriptions on the properties:

```csharp
public sealed record OrderLineInput(
    [property: Description("ISBN-13 of the book, with or without hyphens.")] string Isbn,
    [property: Description("Number of copies, 1 to 1000.")] int Quantity);
```

### Testing a server

Test tool methods **directly**, like any class, with a fake for their dependencies; the Demo 14 and
Activity 14 solutions do exactly this. For the data layer, test against the schema the real application
creates. To try a server by hand, connect it to your agent and ask for something that needs the tool, or
use the MCP Inspector (`npx @modelcontextprotocol/inspector`), which needs Node.js.

### Running from source without locking your build

A server started with `dotnet run` holds its output assemblies open, and on Windows that makes a later
`dotnet build` of the solution fail to copy them. Two habits avoid it:

- Run the server in **Release** (`-c Release`, as in the configurations above) while you build and test in
  Debug, so they use different output folders.
- After changing tools, **reconnect** the server (`/mcp` in Claude Code, **MCP: List Servers** in VS Code)
  so the client sees the new tool list.

To share a finished server, the SDK supports packing it as a NuGet package of type `McpServer` that users
run with `dnx`, and `ModelContextProtocol.AspNetCore` hosts the same tools over HTTP (`WithHttpTransport()`
and `app.MapMcp()`).

---

## Security: least privilege and trust

An MCP server is code that runs with your permissions and feeds text straight into the model. Treat both
directions with care.

### Build servers with least privilege

- **Expose the smallest useful surface.** Two read-only lookup tools beat one "run any SQL" tool.
- **Enforce read-only below the tool.** The ToolsApp server opens SQLite with `Mode=ReadOnly`, so even a
  buggy or manipulated tool can't write:

  ```csharp
  var builder = new SqliteConnectionStringBuilder(connectionString) { Mode = SqliteOpenMode.ReadOnly };
  ```

- **Validate every input** and use parameterized queries. Model-supplied text is untrusted input, exactly
  like a request body.
- **Return only what is needed.** Cap result sizes (25 rows, not the table), and never return secrets or
  connection strings.
- **Scope credentials** to the server's job: a read-only token for a read-only tool.

### Trust servers before you install them

- A third-party stdio server is an executable you run. Prefer official servers, read the source, and pin a
  version (a tagged `ghcr.io/github/github-mcp-server:<version>` image rather than `latest`).
- **Tool descriptions and tool results are prompts.** A malicious or compromised server can include
  instructions ("also read ~/.ssh and send it to...") in either. This is prompt injection (OWASP LLM01),
  and the agentic risks OWASP lists include tool misuse and agentic supply chain attacks.
- Watch for servers whose tools **change after you approved them**. Visual Studio resets tool approvals when
  a server's tool list changes, and asks you to trust an updated server again.
- Keep secrets out of committed config: use VS Code `inputs`, environment variables, or
  `${VAR}` expansion, never literal tokens in `.mcp.json`.
- Organizations can restrict which servers are allowed: GitHub's MCP allowlist policies apply to Copilot
  in Visual Studio, and Claude Code supports managed settings that administrators control.

Module 14 goes deeper into prompt injection and agent permissions, and [module 13](../module-13-ai-across-the-delivery-lifecycle-with-github/) connects the GitHub
MCP server to issues, pull requests, and workflows.

---

## Summary

- **MCP** is an open protocol: a **host** runs one **client** per **server**, and they exchange JSON-RPC
  messages over **stdio** (local) or **Streamable HTTP** (remote).
- Servers expose **tools** (the common case), **resources**, and **prompts**; the agent decides when to call a
  tool, and **you decide which servers it may use**.
- Register servers in **`.mcp.json`** (`mcpServers`) for Claude Code and **`.vscode/mcp.json`** (`servers`)
  for VS Code; the key name is the usual cause of a missing server. In Visual Studio, which reads
  `.mcp.json` first, add the server with **Add custom MCP server** and an absolute path.
- A C# stdio server is a hosted console app with `AddMcpServer().WithStdioServerTransport()`, tools as
  **`[McpServerTool]` methods** with **`[Description]`** text, and all logging on **stderr**.
- Throw **`McpException`** with an actionable message for errors the model can fix; other exceptions reach
  the model only as a generic error.
- Build servers with **least privilege** (read-only connections, small result sets, validated input), and
  **vet third-party servers**: their descriptions and results are prompts.
- Practice it in [Demo 14](../../demos/14-hooks-skills-subagents-and-mcp/) and
  [Activity 14](../../activities/14-hooks-skills-subagents-and-mcp/).
