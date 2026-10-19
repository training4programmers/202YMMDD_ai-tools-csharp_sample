// Offline checks for a GitHub Actions workflow, since a workflow only runs on GitHub:
//   1. the YAML parses (no tabs, valid indentation); it lists the jobs with their needs and conditions;
//   2. the top-level permissions block exists and grants no write scope (write goes on the job that needs it);
//   3. no pull_request_target trigger (it hands secrets and a write token to runs on pull request code);
//   4. no ${{ github.event.* }}, ${{ github.head_ref }}, or ${{ inputs.* }} inside a run: script
//      (script injection: pass those values through env: and read the variable instead);
//   5. every ${{ secrets.X }} sits in a step's env: (or an action's with:), in at most one step per job;
//   6. every job that runs claude or copilot, or uses anthropics/claude-code-action, has
//      continue-on-error: true and timeout-minutes;
//   7. every needs: names a job in the file, and every action is pinned to a version (@v1) or a commit SHA.
//
// Usage, from the solution folder:
//   dotnet run --file tools/check-workflow.cs -- .github/workflows/ci.yml
//
// The solution uses central package management, which would reject the version on the #:package line
// (NU1008), so this script opts out and stays self-contained.
#:package YamlDotNet@18.1.0
#:property ManagePackageVersionsCentrally=false

using System.Text.RegularExpressions;
using YamlDotNet.RepresentationModel;

if (args.Length == 0)
{
    Console.Error.WriteLine("Usage: dotnet run --file tools/check-workflow.cs -- <workflow.yml> [...]");
    return 2;
}

var problems = 0;
foreach (var path in args)
{
    try
    {
        problems += Check(path);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"FAIL {path}: {ex.Message}");
        problems++;
    }
}

Console.WriteLine(problems == 0 ? "All checks passed." : $"{problems} problem(s) found.");
return problems == 0 ? 0 : 1;

static int Check(string path)
{
    var text = File.ReadAllText(path);
    if (text.Contains('\t'))
    {
        throw new InvalidDataException("the file contains a tab character; YAML indentation must use spaces");
    }

    var stream = new YamlStream();
    stream.Load(new StringReader(text));
    var root = stream.Documents.FirstOrDefault()?.RootNode as YamlMappingNode
        ?? throw new InvalidDataException("the file isn't a YAML mapping");
    var jobs = Child(root, "jobs") as YamlMappingNode
        ?? throw new InvalidDataException("there is no jobs: section");

    Console.WriteLine($"OK   {path} parses. Jobs: {string.Join(", ", jobs.Children.Keys)}");
    foreach (var (name, node) in jobs.Children)
    {
        var job = (YamlMappingNode)node;
        var needs = Child(job, "needs") is { } n ? $"  needs {string.Join(", ", Names(n))}" : "";
        var condition = Child(job, "if") is { } c ? $"  if {c}" : "";
        Console.WriteLine($"     job {name}{needs}{condition}");
    }

    List<string> findings = [];
    CheckPermissions(root, findings);
    if (Child(root, "on") is { } on && Names(on).Contains("pull_request_target"))
    {
        findings.Add("on: pull_request_target runs with secrets and a write token; use pull_request");
    }

    foreach (var (name, node) in jobs.Children)
    {
        CheckJob(name.ToString(), (YamlMappingNode)node, jobs, findings);
    }

    CheckSecrets(root, findings);

    foreach (var finding in findings)
    {
        Console.WriteLine($"FAIL {path}: {finding}");
    }

    return findings.Count;
}

static void CheckPermissions(YamlMappingNode root, List<string> findings)
{
    switch (Child(root, "permissions"))
    {
        case null:
            findings.Add("no top-level permissions: block, so every job gets the repository's default token; add permissions: contents: read");
            break;
        case YamlScalarNode { Value: "write-all" }:
            findings.Add("top-level permissions: write-all lets every job write; use contents: read and grant write scopes per job");
            break;
        case YamlMappingNode scopes:
            var writes = scopes.Children.Where(s => s.Value.ToString() == "write").Select(s => $"{s.Key}: write").ToList();
            if (writes.Count > 0)
            {
                findings.Add($"top-level permissions grant {string.Join(", ", writes)}; move write scopes to the job that needs them");
            }

            break;
    }
}

static void CheckJob(string name, YamlMappingNode job, YamlMappingNode jobs, List<string> findings)
{
    foreach (var need in Child(job, "needs") is { } needs ? Names(needs) : [])
    {
        if (!jobs.Children.ContainsKey(new YamlScalarNode(need)))
        {
            findings.Add($"job {name}: needs {need}, which isn't a job in this workflow");
        }
    }

    if (Child(job, "uses") is YamlScalarNode { Value: { } workflow } && !IsPinned(workflow))
    {
        findings.Add($"job {name}: {workflow} isn't pinned; reference a version tag (@v1) or a commit SHA");
    }

    var steps = (Child(job, "steps") as YamlSequenceNode)?.Children.OfType<YamlMappingNode>().ToList() ?? [];
    var runsAgent = false;
    for (var i = 0; i < steps.Count; i++)
    {
        var step = $"job {name}, step {StepName(steps[i], i)}";
        if (Child(steps[i], "run") is YamlScalarNode { Value: { } script })
        {
            foreach (var expression in Patterns.Expression().Matches(script).Select(m => m.Value))
            {
                if (Patterns.Untrusted().IsMatch(expression))
                {
                    findings.Add($"{step}: {expression} inside run: is script injection; pass it through env: instead");
                }
            }

            runsAgent |= Patterns.AgentCommand().IsMatch(script);
        }

        if (Child(steps[i], "uses") is YamlScalarNode { Value: { } uses })
        {
            if (!IsPinned(uses))
            {
                findings.Add($"{step}: {uses} isn't pinned; reference a version tag (@v1) or a commit SHA");
            }

            runsAgent |= uses.StartsWith("anthropics/claude-code-action", StringComparison.Ordinal);
        }
    }

    if (runsAgent && Child(job, "continue-on-error") is not YamlScalarNode { Value: "true" })
    {
        findings.Add($"job {name} runs an agent without continue-on-error: true, so a model failure fails the run");
    }

    if (runsAgent && Child(job, "timeout-minutes") is null)
    {
        findings.Add($"job {name} runs an agent without timeout-minutes, so a hung run holds a runner for hours");
    }
}

static void CheckSecrets(YamlMappingNode root, List<string> findings)
{
    Dictionary<(string Job, string Secret), HashSet<string>> mapped = [];
    foreach (var (path, value) in Scalars(root, []))
    {
        var secrets = Patterns.Expression().Matches(value)
            .SelectMany(e => Patterns.Secret().Matches(e.Value))
            .Select(m => m.Groups["name"].Value);
        foreach (var secret in secrets)
        {
            if (path is ["jobs", var job, "steps", var step, "env" or "with", _])
            {
                if (!mapped.TryGetValue((job, secret), out var inSteps))
                {
                    mapped[(job, secret)] = inSteps = [];
                }

                inSteps.Add(step);
            }
            else
            {
                findings.Add($"{string.Join('.', path)}: secret {secret} outside a step's env:; map it into the one step that needs it");
            }
        }
    }

    foreach (var ((job, secret), steps) in mapped.Where(m => m.Value.Count > 1))
    {
        findings.Add($"job {job}: secret {secret} is mapped into {steps.Count} steps; give it only to the step that needs it");
    }
}

static IEnumerable<(string[] Path, string Value)> Scalars(YamlNode node, string[] path) => node switch
{
    YamlScalarNode scalar => [(path, scalar.Value ?? "")],
    YamlMappingNode mapping => mapping.Children.SelectMany(c => Scalars(c.Value, [.. path, c.Key.ToString()])),
    YamlSequenceNode sequence => sequence.Children.SelectMany((c, i) => Scalars(c, [.. path, $"{i}"])),
    _ => [],
};

static IEnumerable<string> Names(YamlNode node) => node switch
{
    YamlScalarNode scalar => [scalar.Value ?? ""],
    YamlSequenceNode sequence => sequence.Children.Select(c => c.ToString()),
    YamlMappingNode mapping => mapping.Children.Keys.Select(k => k.ToString()),
    _ => [],
};

static bool IsPinned(string uses) =>
    uses.StartsWith("./", StringComparison.Ordinal) || uses.StartsWith("docker://", StringComparison.Ordinal) ||
    Patterns.PinnedAction().IsMatch(uses);

static string StepName(YamlMappingNode step, int index) =>
    (Child(step, "name") ?? Child(step, "id") ?? Child(step, "uses"))?.ToString() ?? $"#{index + 1}";

static YamlNode? Child(YamlMappingNode node, string key) =>
    node.Children.TryGetValue(new YamlScalarNode(key), out var value) ? value : null;

static partial class Patterns
{
    // One ${{ ... }} expression; GitHub replaces it with its value before the shell sees the script.
    [GeneratedRegex(@"\$\{\{.*?\}\}", RegexOptions.Singleline)]
    public static partial Regex Expression();

    // Values a pull request author (or whoever starts the run) controls.
    [GeneratedRegex(@"\bgithub\.event\b|\bgithub\.head_ref\b|\binputs\.")]
    public static partial Regex Untrusted();

    [GeneratedRegex(@"\bsecrets\.(?<name>[A-Za-z_][A-Za-z0-9_]*)")]
    public static partial Regex Secret();

    // claude or copilot run as a command with flags (not the npm package names that install them).
    [GeneratedRegex(@"(?<![\w@/.-])(claude|copilot)\s+-", RegexOptions.Multiline)]
    public static partial Regex AgentCommand();

    [GeneratedRegex(@"^[^@\s]+@(v\d[\w.-]*|[0-9a-f]{40})$")]
    public static partial Regex PinnedAction();
}
