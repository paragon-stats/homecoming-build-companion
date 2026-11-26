using System.Text.RegularExpressions;

using YamlDotNet.RepresentationModel;

namespace CohEngine.Analyzers.Tests.Ci;

/// <summary>The repository's GitHub workflow files, read as YAML, and the check contexts their jobs report.</summary>
internal static partial class Workflows
{
    private const string StatusAction = "/.github/actions/report-status-context";

    public static string RepositoryRoot { get; } = FindRoot();

    public static string WorkflowDirectory => Path.Join(RepositoryRoot, ".github", "workflows");

    [GeneratedRegex(@"github\.event_name\s*==\s*'(?<event>[a-z_]+)'", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex EventTest { get; }

    [GeneratedRegex(@"\$\{\{\s*matrix\.(?<key>[\w-]+)\s*\}\}", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex MatrixValue { get; }

    public static YamlMappingNode Load(string path)
    {
        YamlStream stream = [];
        using StreamReader reader = new(path);
        stream.Load(reader);
        return (YamlMappingNode)stream.Documents[0].RootNode;
    }

    public static YamlMappingNode LoadWorkflow(string fileName) => Load(Path.Join(WorkflowDirectory, fileName));

    public static string? Scalar(YamlNode node, string key) =>
        node is YamlMappingNode mapping && mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value) && value is YamlScalarNode scalar ? scalar.Value : null;

    public static YamlNode? Child(YamlNode node, string key) =>
        node is YamlMappingNode mapping && mapping.Children.TryGetValue(new YamlScalarNode(key), out YamlNode? value) ? value : null;

    public static IEnumerable<KeyValuePair<string, YamlMappingNode>> Jobs(YamlMappingNode workflow) =>
        ((YamlMappingNode)workflow["jobs"]).Children.Select(pair => new KeyValuePair<string, YamlMappingNode>(((YamlScalarNode)pair.Key).Value!, (YamlMappingNode)pair.Value));

    public static IEnumerable<YamlMappingNode> Steps(YamlMappingNode job) =>
        Child(job, "steps") is YamlSequenceNode steps ? steps.Children.Cast<YamlMappingNode>() : [];

    public static IReadOnlySet<string> Events(YamlMappingNode workflow) => Child(workflow, "on") switch
    {
        YamlScalarNode single => new HashSet<string>(StringComparer.Ordinal) { single.Value! },
        YamlSequenceNode list => list.Children.Select(item => ((YamlScalarNode)item).Value!).ToHashSet(StringComparer.Ordinal),
        YamlMappingNode map => map.Children.Keys.Select(item => ((YamlScalarNode)item).Value!).ToHashSet(StringComparer.Ordinal),
        _ => new HashSet<string>(StringComparer.Ordinal),
    };

    /// <summary>Whether an <c>if:</c> lets the event through.</summary>
    /// <remarks>
    /// Only <c>github.event_name == '...'</c> tests are read, and a condition with none admits every event.
    /// Any other use of <c>github.event_name</c> (<c>!=</c>, <c>contains</c>) throws rather than being guessed at.
    /// </remarks>
    /// <exception cref="NotSupportedException">The condition tests the event in a form this reader does not know.</exception>
    public static bool Admits(string? condition, string eventName)
    {
        if (condition is null)
        {
            return true;
        }

        List<string> named = [.. EventTest.Matches(condition).Select(match => match.Groups["event"].Value)];
        if (named.Count != Regex.Count(condition, @"github\.event_name", RegexOptions.None, TimeSpan.FromSeconds(5)))
        {
            throw new NotSupportedException($"cannot read the event test in '{condition}'");
        }

        return named.Count == 0 || named.Contains(eventName, StringComparer.Ordinal);
    }

    /// <summary>The steps of a job that run the report-status-context action.</summary>
    public static IEnumerable<YamlMappingNode> StatusSteps(YamlMappingNode job) =>
        Steps(job).Where(step => (Scalar(step, "uses") ?? string.Empty).EndsWith(StatusAction, StringComparison.Ordinal));

    /// <summary>Every context the workflows report on one event, with the job that reports it.</summary>
    /// <remarks>
    /// A job reports its check run under its name whenever its workflow runs, even when its <c>if:</c> skips it,
    /// and a skipped check satisfies a ruleset. A commit status is reported only when the job and the
    /// report-status-context step both run.
    /// </remarks>
    public static List<(string Context, string Job)> Contexts(string eventName)
    {
        List<(string Context, string Job)> found = [];
        IEnumerable<(string Path, YamlMappingNode Workflow)> triggered = Directory.EnumerateFiles(WorkflowDirectory, "*.yml")
            .Order(StringComparer.Ordinal)
            .Select(path => (path, Load(path)))
            .Where(pair => Events(pair.Item2).Contains(eventName));
        foreach ((string path, YamlMappingNode workflow) in triggered)
        {
            foreach ((string jobId, YamlMappingNode job) in Jobs(workflow))
            {
                string where = $"{Path.GetFileName(path)} / {jobId}";
                string? name = Scalar(job, "name");

                // The job's condition matters only for the statuses it posts; its own check reports either way.
                List<YamlMappingNode> reporting = [.. StatusSteps(job).Where(step => Admits(Scalar(step, "if"), eventName))];
                bool runs = reporting.Count > 0 && Admits(Scalar(job, "if"), eventName);
                foreach (Dictionary<string, string> combination in Combinations(job))
                {
                    found.Add((name is null ? DefaultName(jobId, combination) : Expand(name, combination), where));
                    if (runs)
                    {
                        found.AddRange(reporting.Select(step => (Expand(Scalar(Child(step, "with")!, "status-context")!, combination), where)));
                    }
                }
            }
        }

        return found;
    }

    private static string DefaultName(string jobId, Dictionary<string, string> combination) =>
        combination.Count == 0 ? jobId : $"{jobId} ({string.Join(", ", combination.Values)})";

    private static string Expand(string text, Dictionary<string, string> combination) =>
        MatrixValue.Replace(text, match => combination[match.Groups["key"].Value]);

    /// <summary>The matrix combinations of a job: one empty combination without a matrix.</summary>
    /// <remarks>shortcut: a matrix of lists, or of <c>include</c> entries alone; one that mixes both throws.</remarks>
    private static List<Dictionary<string, string>> Combinations(YamlMappingNode job)
    {
        if (Child(job, "strategy") is not YamlMappingNode strategy || Child(strategy, "matrix") is not YamlMappingNode matrix)
        {
            return [[]];
        }

        List<Dictionary<string, string>> combinations = [[]];
        bool lists = false;
        foreach ((YamlNode key, YamlNode value) in matrix.Children.Where(pair => !string.Equals(((YamlScalarNode)pair.Key).Value, "include", StringComparison.Ordinal)))
        {
            string name = ((YamlScalarNode)key).Value!;
            lists = true;
            combinations =
            [
                .. combinations.SelectMany(partial => ((YamlSequenceNode)value).Children.Select(item => new Dictionary<string, string>(partial, StringComparer.Ordinal) { [name] = ((YamlScalarNode)item).Value! })),
            ];
        }

        if (Child(matrix, "include") is not YamlSequenceNode include)
        {
            return combinations;
        }

        if (lists)
        {
            throw new NotSupportedException("a matrix with both lists and include entries is not modelled");
        }

        return
        [
            .. include.Children.Cast<YamlMappingNode>().Select(entry => entry.Children.ToDictionary(pair => ((YamlScalarNode)pair.Key).Value!, pair => ((YamlScalarNode)pair.Value).Value!, StringComparer.Ordinal)),
        ];
    }

    private static string FindRoot()
    {
        DirectoryInfo? root = new(AppContext.BaseDirectory);
        while (root is not null && !File.Exists(Path.Join(root.FullName, "CohEngine.slnx")))
        {
            root = root.Parent;
        }

        return root?.FullName ?? throw new InvalidOperationException("CohEngine.slnx not found above the test output");
    }
}
