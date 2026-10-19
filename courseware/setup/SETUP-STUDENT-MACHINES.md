# Setup Instructions for AI-Assisted Development with C#

Install everything below **before Day 1**. Your instructor will confirm the setup in the first session.
This is a **tool-agnostic** course: your organization chooses the AI tools, usually GitHub Copilot,
Claude Code, or both. Install the ones you have access to, and check with your instructor if you aren't
sure.

Versions and menus for AI tools change often. When a step here doesn't match what you see, the vendor's
own install page is the authority. Each section links to it.

## Required: the whole course runs on these

1. **Windows 10/11** (the course is Windows-first; macOS/Linux work for everything except the Visual
   Studio and .NET Framework labs).
2. **[.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)**: install the latest SDK in the
   **10.0.4xx** feature band (10.0.402 at the time of writing), or in **10.0.1xx** (10.0.113) if your
   company standardizes on it. Those are the two bands Microsoft still services; 10.0.2xx and 10.0.3xx
   no longer get updates. The labs' `global.json` uses whichever 10.0 SDK is newest on your machine.
   When .NET 11 ships (expected November 2026), you may install it alongside, but the labs target .NET 10.
3. **[Visual Studio 2026](https://visualstudio.microsoft.com/)** **version 18.5 or later** (Community,
   Professional, or Enterprise). Install the latest update (18.11 at the time of writing): Copilot's
   custom agents need 18.4, and agent skills and the Debugger Agent need 18.5. Check under
   **Help > About Microsoft Visual Studio**, and update from **Help > Check for Updates**. Include these
   workloads:
   - **ASP.NET and web development**
   - **.NET desktop development**: this includes the .NET Framework 4.8 targeting pack used in the
     legacy-modernization labs.

   GitHub Copilot is built into Visual Studio 2026; sign in with the GitHub account your organization
   licensed.
4. **[Visual Studio Code](https://code.visualstudio.com/)** with the extensions listed below.
5. **[PowerShell 7](https://learn.microsoft.com/powershell/scripting/install/installing-powershell-on-windows)**
   (`winget install Microsoft.PowerShell`). Every terminal example uses PowerShell 7.
6. **[Git for Windows](https://git-scm.com/download/win)** and the
   **[GitHub CLI](https://cli.github.com/)** (`winget install GitHub.cli`), signed in with `gh auth login`.
7. **[Node.js LTS](https://nodejs.org/)**, **version 22 or later** (`winget install OpenJS.NodeJS.LTS`).
   Some AI tooling runs on Node: the Azure DevOps MCP server (`npx @azure-devops/mcp`) and the npm
   installs of the AI CLIs (Claude Code's npm package requires Node.js 22 or later).
8. **Your organization's AI tools.** Install the ones your organization uses:

   | Tool | Install | Sign in / check |
   |------|---------|-----------------|
   | GitHub Copilot in Visual Studio 2026 | built in | sign in with your GitHub account (top-right of VS) |
   | GitHub Copilot in VS Code | the **GitHub Copilot Chat** extension (`github.copilot-chat`; see below) | sign in from the Accounts menu |
   | [GitHub Copilot CLI](https://docs.github.com/copilot) | `winget install GitHub.Copilot` | run `copilot`, then `/login` |
   | [Claude Code](https://code.claude.com/docs/en/setup) | `irm https://claude.ai/install.ps1 \| iex` (or `winget install Anthropic.ClaudeCode`) | `claude --version`, then `claude` and follow the login prompt; `claude doctor` checks the install |
   | Claude Code in VS Code | the **Claude Code** extension (`anthropic.claude-code`) | sign in from the extension |

   Trials and free tiers are fine where the vendor offers them. Note that Claude Code is not part of the
   free Claude plan. The Claude Code labs need **v2.1.295 or later** (the hook settings use
   `onFailure`); `claude --version` shows yours, and `claude update` upgrades it.
9. **Access to Azure DevOps** for the Day 5 delivery-lifecycle labs: an organization and project your
   instructor provides (or your company's), with permission to create work items, branches, pull
   requests, and pipelines.
10. **The [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli-windows)**, signed in. The
    Azure DevOps MCP server in Activity 22 signs in by reusing your Azure CLI session (`-a azcli`), unless
    your instructor chooses browser sign-in or a personal access token instead. Install it, open a **new**
    terminal, and sign in with the account that has your Azure DevOps access:

    ```powershell
    winget install --exact --id Microsoft.AzureCLI
    az login --allow-no-subscriptions   # the flag lets accounts with no Azure subscription sign in
    ```

## Required Visual Studio Code extensions

| Extension | ID | Why |
|-----------|----|-----|
| C# Dev Kit | `ms-dotnettools.csdevkit` | Solution explorer, test explorer, debugging for .NET |
| C# | `ms-dotnettools.csharp` | Language support (installed with the Dev Kit) |
| GitHub Copilot Chat | `github.copilot-chat` | Copilot completions, chat, and agent mode (if your org uses Copilot) |
| Claude Code | `anthropic.claude-code` | Claude Code inside VS Code (if your org uses Claude Code) |
| REST Client | `humao.rest-client` | Send the requests in each lab's `.http` file (Visual Studio 2026 runs `.http` files natively) |

> The C# Dev Kit is free for individuals and small teams; larger commercial teams need a Visual Studio
> subscription. Check your organization's licensing.

## Verify the install

Open **PowerShell 7** and run:

```powershell
dotnet --list-sdks            # a 10.0.4xx (or 10.0.1xx) SDK is listed
git --version
gh auth status                # logged in to github.com
node --version                # v22 or later
code --list-extensions        # includes ms-dotnettools.csdevkit
az account show               # signed in (shows your tenant)
copilot --version             # if your org uses GitHub Copilot CLI
claude --version              # if your org uses Claude Code
```

All of them should print a version (or a logged-in status) without errors.

## Getting the course files

Clone the course repository (or download it as a ZIP from your instructor's link) to a **short path**,
such as `C:\course`. Some lab paths are deep, and short paths avoid Windows path-length limits:

```powershell
git clone <course-repo-url> C:\course
git config --global core.longpaths true      # once, for safety with deep paths
```

Replace `<course-repo-url>` with the URL of the course repository your instructor shares: the GitHub
page this guide came from (its green **Code** button copies the URL). Each delivery of the course has
its own repository, so there's no fixed URL to put here.

**If you downloaded a ZIP**, Windows marks every extracted file as "downloaded from the internet", and
with the `RemoteSigned` execution policy PowerShell then refuses to run the course's scripts
(`start-lab.ps1`, and the `.ps1` hooks some labs ship). Clear the mark once, after extracting:

```powershell
Get-ChildItem C:\course -Recurse | Unblock-File
```

## How to start a lab

**Labs never run inside the course folder.** AI tools read instruction files (`CLAUDE.md`, `AGENTS.md`,
`.github/copilot-instructions.md`) from the folders around your code. The course repo has its own
instruction files for maintaining the courseware, and you don't want those in your lab sessions. So
every demo and activity starts with a helper script. It copies the lab's starting code to `~\labs`,
makes it a git repository, and commits a baseline:

```powershell
cd C:\course
.\courseware\utils\start-lab.ps1 -Activity 3          # core activity 03
.\courseware\utils\start-lab.ps1 -Activity 2 -Bonus   # bonus activity 02
cd ~\labs\activity-03-prompting-for-modern-csharp
dotnet build
dotnet test --solution Bookstore.slnx
```

Keep `~\labs` (or whatever `-LabsRoot` you choose) **short**. Build output nests deeply, and some native
libraries (SQLite's, used by every lab's tests) fail to load from very long paths on Windows with
"filename or extension is too long". If your profile folder path is long, use `-LabsRoot C:\labs`.

Then open the lab folder in VS Code (`code .`) or open its `.slnx` in Visual Studio 2026. While you work:

- `git diff` shows exactly what you and your assistant changed.
- `git restore . ; git clean -fd` throws your changes away and returns to the baseline.
- Re-running the script with `-Force` gives you a fresh copy.
- `-Solution` copies the reference solution, so you can compare. It's one acceptable result, not the only
  one.

## Warm the NuGet cache (recommended)

The first build of each lab restores packages from nuget.org. To make Day 1 faster, start one lab and
build it once before class. The last command runs Activity 01's token counter, a .NET 10 file-based app
(`tools/tokens.cs`), which restores the two `Microsoft.ML.Tokenizers` packages it declares:

```powershell
.\courseware\utils\start-lab.ps1 -Activity 1
cd ~\labs\activity-01-how-ai-writes-code
dotnet build
dotnet test --solution Bookstore.slnx
dotnet run tools/tokens.cs -- src     # prints token counts for the .cs files under src
```

## Permissions

- You need rights to install software (or have IT preinstall it), and to run PowerShell scripts.
  `Set-ExecutionPolicy -Scope CurrentUser RemoteSigned` allows local scripts.
- Outbound HTTPS to nuget.org, github.com, your AI vendors' endpoints, and dev.azure.com must be allowed.
  Corporate proxies sometimes block AI tools, so test sign-in before Day 1.
- Don't use a personal AI account for company code unless your organization's policy allows it. Module 2
  covers why.
