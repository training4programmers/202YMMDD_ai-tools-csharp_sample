# GitHub Advanced Security as a Quality Gate

AI assistants let a team write more code, faster, and some of it will have the security mistakes from
Module 01: SQL built from interpolated strings, a disabled certificate check, a secret pasted into
`appsettings.json`. Review catches some of them. Automated security gates catch them on every pull
request, whether or not the reviewer was paying attention. On GitHub those gates come from **GitHub
Advanced Security (GHAS)**: code scanning with CodeQL, dependency review and Dependabot, and secret
scanning with push protection.

This chapter covers each one as a **quality gate** for a .NET 10 solution: how to set up CodeQL for C#,
how to make its results block a merge, how to handle AI fixes for alerts (**Copilot Autofix**, the newer
**agentic autofix**, and an agent-drafted fix as the fallback), and how Dependabot, dependency review, and
secret scanning fit around them. The security and governance view of the same tools (policy, secrets
hygiene, supply-chain vetting) is in Module 14.

---

## Products and What They Cover

GHAS is sold as two products. Both are **free on public repositories**; private and internal
repositories in an organization need the licenses, which an organization owner enables for all
repositories or a chosen few. In a repository, the switches live under **Settings** > **Advanced
Security** (in the sidebar's *Security and quality* section at the time of writing; older pages call it
*Code security*).

| Product | Includes | Catches |
|---------|----------|---------|
| **GitHub Code Security** | **Code scanning** with CodeQL, **Copilot Autofix**, security campaigns, **dependency review**, and premium Dependabot features | Vulnerable code patterns; newly added vulnerable packages |
| **GitHub Secret Protection** | **Secret scanning**, **push protection**, custom patterns, delegated bypass | Credentials committed to the repository, before or after the push |

Dependabot alerts and security updates, the dependency graph, and push protection for your own pushes to
public repositories are available without either license. Results appear on the repository's
**Security** tab, and as annotations and checks on pull requests.

---

## Code Scanning with CodeQL

**CodeQL** analyzes code as data: it builds a database of the codebase and runs queries that find
vulnerability patterns, such as user input that flows into a SQL string through three methods and two
files. There are two ways to run it, and a repository uses **one or the other, never both**.

### Default setup: no YAML

**Default setup** is the quickest start. Under **Settings** > **Advanced Security**, find **CodeQL
analysis**, choose **Set up** > **Default**, review the settings, and select **Enable CodeQL**. It
detects the languages (C# included), scans pushes and pull requests, and adds a weekly schedule. Start
here unless you need more control.

### Advanced setup: a CodeQL job in your workflow

**Advanced setup** puts CodeQL in your own workflow, so you choose when it runs and how it fits with your
other checks. For C#, it no longer needs a build:

```yaml
codeql:
  name: CodeQL
  runs-on: ubuntu-latest
  timeout-minutes: 20
  permissions:
    contents: read
    actions: read            # private repositories: read the workflow run
    security-events: write   # upload the results to code scanning
  steps:
    - uses: actions/checkout@v7

    - uses: github/codeql-action/init@v4
      with:
        languages: csharp
        build-mode: none

    - uses: github/codeql-action/analyze@v4
      with:
        category: /language:csharp
```

With **`build-mode: none`**, CodeQL extracts C# from the source without compiling it, so the job is
faster and runs even when the build is broken (Demo 23 runs it beside the build job, not after it). The
alternatives are `autobuild`, which tries to build the solution for you, and `manual`, where you put your
own `dotnet build` step between `init` and `analyze`. AI assistants trained on older examples often
reach for a separate `autobuild` step or an old action version; for a .NET 10 solution, `build-mode: none`
on `init@v4` is the simple, current answer.

If an upload fails with a message about default setup, turn default setup off for that repository: the
two can't both run.

### Reading an alert

Each alert on **Security** > **Code scanning** has a rule (for example, SQL query built from user-controlled
sources), a severity, the location, and, for data-flow queries, the **path** from the source (an HTTP
query parameter) to the sink (the SQL string). Read the path before you read the fix: it tells you
whether the problem is where the alert points or three calls earlier. On a pull request, new alerts
appear as annotations on the changed lines and in the code scanning check. Closing an alert takes a
fix that the next scan confirms, or a **dismissal** with a reason (false positive, used in tests, won't
fix) by someone with permission to dismiss; a dismissal is a decision a person signs, never something an
agent should propose to make a gate pass.

---

## Merge Protection: Making Alerts a Gate

Scanning is only a gate if a failing result stops a merge. Put these rules in one **branch ruleset** on
the default branch (**Settings** > **Rules** > **Rulesets** > **New branch ruleset**):

- **Require a pull request before merging**, with at least one **required approval**.
- **Require status checks to pass**, naming your deterministic jobs (in Demo 23, `Build and test` and
  `Dependency review`). Let one run finish first, so the check names exist to pick from.
- **Require code scanning results**: add **CodeQL** as the tool, then set two thresholds. **Security
  alerts** (None, Critical, High or higher, Medium or higher, All) blocks on security findings at that
  severity; **Alerts** (None, Errors, Errors and Warnings, All) blocks on other code scanning findings.
  Demo 23 uses **High or higher** and **Errors**.
- **Require review from Code Owners** for the files that need a specific reviewer
  (Module 14).

Don't add advisory checks to that list. **AI Scan for pull requests** (GA since October 2026) is a good
example: it adds AI findings for languages CodeQL doesn't cover (such as Shell, Terraform, and
Dockerfiles), marked with an "AI" indicator, and by design it can't fail the code scanning check or
block a merge. For C#, CodeQL is the gate.

GHAS is one layer. The cheapest gates run earlier, and Demo 23 shows them catching the same bug at
different points:

| Layer | Catches | When | Cost to run |
|-------|---------|------|-------------|
| Compiler and analyzers with warnings as errors | `EF1002` (SQL from an interpolated string), obsolete APIs, nullable warnings | `dotnet build -warnaserror` | Seconds |
| NuGet Audit | Known-vulnerable packages | `dotnet restore` | Seconds |
| Push protection | Secrets | `git push` | Instant |
| CodeQL | Data-flow vulnerabilities across methods and files | Every workflow run (or default setup's schedule) | Minutes |
| Dependency review | Newly added vulnerable packages, as a failed check | Pull requests | Seconds to minutes |

Demo 23's starting point contains an AI-written search endpoint *(illustrative of a common mistake)*:

```csharp
// Generated by an AI assistant for review: not yet reviewed.
var colors = await db.Colors
    .FromSqlRaw($"SELECT * FROM Colors WHERE Name LIKE '%{name}%'")
    .Include(c => c.Palette)
    .AsNoTracking()
    .OrderBy(c => c.Name)
    .ToListAsync(cancellationToken);
// warning EF1002: Method 'FromSqlRaw' inserts interpolated strings directly into the SQL ...
```

Calling it with `?name=' OR 1=1 --` returns every color. The workflow's `dotnet build -warnaserror`
turns `EF1002` into an error before anyone reads the diff, and CodeQL's SQL injection query raises an
alert that the ruleset blocks on. Analyzers catch the obvious single-line case; data-flow queries catch
the cases an analyzer can't see, where the string is built in one method and executed in another.

---

## Copilot Autofix

**Copilot Autofix** proposes a fix for a code scanning alert: a short explanation and a suggested code
change. It's GA, free on public repositories, and part of GitHub Code Security on private ones. It shows
up in two places:

- **In pull requests**, as a suggestion on a new CodeQL alert. Review it like any suggested change, and
  commit it only if it fixes the cause and keeps the behavior (see *Reviewing an AI-proposed security
  fix*, below).
- **On existing alerts**, with **Generate fix** on the alert page, which proposes a change you can
  commit to a new branch and open as a pull request.

### Agentic autofix (GitHub Enterprise Cloud)

**Agentic autofix**, GA since October 15, 2026, goes further. It replaces **Generate fix** with an
**Assign to Copilot** button: the Copilot cloud agent explores the relevant files, proposes a fix, reruns
CodeQL to check that the alert closes, iterates if it doesn't, and opens a **draft pull request** for you
to review. You can select up to 25 alerts in the alerts list to fix together in one pull request.

The conditions matter: it's for **GitHub Code Security or GHAS customers on GitHub Enterprise Cloud**;
an enterprise owner enables the **Agentic autofix** policy, then an organization or repository admin
turns it on; a Copilot billing method must be set up; it works only on alerts found on the **default
branch**; and it consumes **AI credits** from the Code Security budget plus **Actions minutes**
([chapter 08](08-cost-visibility.md)). Its pull request goes through the same ruleset as any other.

### Fallback: an agent drafts the fix from the IDE

On other plans, or for an alert on a branch, any agent can do the core of what agentic autofix does:
read the alert, change the code, prove the fix. Load the GitHub MCP server's **`code_security`** toolset
(for example `X-MCP-Toolsets: default,code_security`, and a token with **Code scanning alerts: Read**) so
the agent can read the alert itself, or paste the alert's rule, location, and data-flow path into the
prompt.

```text
Read the open code scanning alerts for <owner>/<repo> through the GitHub MCP server. For the SQL
injection alert in ColorSearchEndpoints.cs: explain the data flow, then fix it without changing the
endpoint's behavior (case-insensitive partial match on Name). Add tests that send SQL and LIKE
wildcard characters and expect them to be treated as text. Run dotnet build -warnaserror and
dotnet test --solution ToolsApp.slnx. Don't suppress analyzers or dismiss the alert.
```

The reviewed fix from Demo 23 sends the pattern as a parameter and escapes `LIKE` wildcards, so `%` and
`_` in a search are literal:

```csharp
var pattern = $"%{EscapeLike(name)}%";      // a C# string, sent as a SQL parameter

var colors = await db.Colors
    .Include(c => c.Palette)
    .AsNoTracking()
    .Where(c => EF.Functions.Like(c.Name, pattern, LikeEscape))
    .OrderBy(c => c.Name)
    .ToListAsync(cancellationToken);
// SQL: WHERE "c"."Name" LIKE @pattern ESCAPE '\'
```

Then push a branch, open a pull request (with `Fixes` pointing at the issue, if one tracks the alert),
and let the next scan close the alert. A fix isn't done until the scanner agrees.

> **GitHub Copilot:** Use Copilot Autofix in the pull request or **Generate fix** on the alert; on
> GitHub Enterprise Cloud with agentic autofix enabled, **Assign to Copilot**. Otherwise, use agent mode
> in Visual Studio 2026 or VS Code with the prompt above.
>
> **Claude Code:** Run the prompt above in a session with the GitHub MCP server and the `code_security`
> toolset (or paste the alert), in plan mode first, with `git push` under `ask`.

---

## Reviewing an AI-Proposed Security Fix

Security fixes are where a plausible patch is most dangerous. Whether it came from Autofix, agentic
autofix, or your own agent, check:

- **It fixes the cause, not the symptom.** Parameterization fixes SQL injection; hand-escaping quotes
  doesn't. Turning off certificate validation doesn't fix a certificate error.
- **Behavior is preserved.** The rewrite above keeps the case-insensitive partial match. A first attempt
  with `Contains` would have quietly made the search case-sensitive on SQLite.
- **There's a test that attacks it.** A test that sends `' OR 1=1 --` and expects an empty result fails
  on the vulnerable code and passes on the fix.
- **Nothing was silenced.** No `#pragma warning disable EF1002`, no `<NoWarn>`, no lowered analysis level,
  no alert dismissed as a "false positive" to make the gate pass.
- **The scanner agrees** on the next run.

---

## Dependabot and Dependency Review

Assistants add packages casually, and sometimes the package or the version is wrong
([Module 12](../module-12-csharp-pitfalls-and-verification/01-hallucinated-nuget-packages-and-api-versions.md)
covers hallucinated and typosquatted packages). Three GitHub features watch the dependencies you have.

- **Dependabot alerts** tell you when a package you already use gets a known vulnerability, and
  **Dependabot security updates** open a pull request that bumps it to a fixed version.
- **Dependabot version updates** open pull requests to keep packages current on a schedule, from
  `.github/dependabot.yml`:

  ```yaml
  version: 2
  updates:
    - package-ecosystem: "nuget"
      directory: "/"
      schedule:
        interval: "weekly"
      groups:
        microsoft:
          patterns:
            - "Microsoft.*"
    - package-ecosystem: "github-actions"
      directory: "/"
      schedule:
        interval: "weekly"
  ```

  The `github-actions` entry keeps the `uses:` versions in your workflows current too. Dependabot pull
  requests go through the same ruleset and checks as everyone else's; treat them as changes to review,
  not as chores to merge.
- **Dependency review** compares the dependencies in a pull request with its base and **fails the check**
  when the pull request adds a package with a known vulnerability. As a job in your workflow, made a
  required status check:

  ```yaml
  dependency-review:
    name: Dependency review
    if: ${{ github.event_name == 'pull_request' }}
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v7
      - uses: actions/dependency-review-action@v5
  ```

  This is the job the labs use. With no options, the action fails on a newly added vulnerability of
  any severity (its `fail-on-severity` default is `low`). A team that wants to block only on serious
  ones can raise the threshold, an option the labs don't use:

  ```yaml
  - uses: actions/dependency-review-action@v5
    with:
      fail-on-severity: high   # optional: ignore low and moderate advisories
  ```

Dependency review needs the **dependency graph** turned on, and on private repositories, GitHub Code
Security. For .NET, the dependency graph reads your project files; turn on **automatic dependency
submission** (**Settings** > **Advanced Security**, in the dependency graph section) so it also records
the packages a real restore resolves, transitive ones included. It runs as an Actions job, and it starts
when `dependabot.yml` lists `nuget` or a supported project file sits at the repository root.

These complement **NuGet Audit**, which already warns at restore time (`NU1901` to `NU1904`) and checks
transitive packages by default in .NET 10. NuGet Audit tells the developer at their desk; Dependabot and
dependency review give the team an alert it can track and a check it can require.
Module 14
covers vetting a package before you accept it, lock files, and source mapping.

---

## Secret Scanning and Push Protection

When secret scanning is on, it scans the repository **including its history** and raises an alert for
each credential it finds. **Push protection** goes further: it rejects a push that contains a supported
secret before it reaches GitHub. Push protection for your own pushes to public repositories is on by
default; repository-level push protection, which covers private repositories, needs Secret Protection.
Module 14 covers where secrets
belong in .NET and what to do after a leak. For the delivery lifecycle, four points matter:

- **Agents leak secrets in ordinary-looking places**: a connection string in a test, a key in a sample
  `.http` file, a token in a "temporary" config file. Pushes from Copilot in your IDE, the Copilot CLI,
  or Claude Code are ordinary `git push` commands, so push protection is the net under all of them.
- **A bypass is a security event, not a fix.** By default anyone with write access can bypass a block by
  choosing a reason; with **delegated bypass**, chosen reviewers must approve it. An agent told "the push
  was rejected, fix it" must remove the secret, never look for a way past the block.
- **Plain passwords need custom patterns.** Scanning matches known provider token formats. A password in
  a connection string (`Password=...`) isn't one, so add a **custom pattern** for your connection-string
  shapes.
- **Scan before you commit.** With Secret Protection, the remote GitHub MCP server's `secret_protection`
  toolset (plus its `run_secret_scanning` tool, added with the `X-MCP-Tools` header) lets an agent scan
  your current changes before you commit. Its findings exist only in that chat, not as alerts, so treat
  it as a pre-commit check, not a gate.

---

## Summary

- **GHAS** comes as **GitHub Code Security** (CodeQL, Copilot Autofix, dependency review) and **GitHub
  Secret Protection** (secret scanning, push protection, custom patterns); both are **free on public
  repositories**.
- Run CodeQL with **default setup** (no YAML) or **advanced setup**: `github/codeql-action/init@v4` with
  **`languages: csharp`** and **`build-mode: none`**, then `analyze@v4`, with `security-events: write` on
  that job only. Use **one setup, never both**.
- Make results a **gate** with a ruleset: **Require code scanning results** (security alerts **High or
  higher**, alerts **Errors**), **Require status checks to pass**, and **Require a pull request** with a
  human approval. Keep the cheap layers (**`-warnaserror`**, NuGet Audit, push protection) in front, and
  advisory AI findings out of the required list.
- **Copilot Autofix** suggests fixes in pull requests and on alerts. **Agentic autofix** (assign alerts to
  Copilot) is **GA only on GitHub Enterprise Cloud**; elsewhere, an **agent drafts the fix** from the IDE,
  reading the alert through the MCP server's **`code_security`** toolset. Review every security fix for
  **root cause**, **preserved behavior**, an **attacking test**, and **nothing silenced**.
- **Dependabot** (`dependabot.yml` with `nuget`) keeps packages current and patched; **dependency review**
  (`actions/dependency-review-action@v5`) fails pull requests that add vulnerable packages; turn on
  **automatic dependency submission** for .NET.
- **Push protection** stops secrets at `git push`, including agents' pushes; treat a **bypass** as a
  security event and add **custom patterns** for connection strings.
