using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using YamlDotNet.RepresentationModel;

namespace CohEngine.Analyzers.Tests.Ci;

/// <summary>Every context a ruleset requires is reported, by one job, on pull requests and in the merge queue.</summary>
public sealed partial class RequiredCheckTests
{
    private const string MainRuleset = "main-ruleset.json";
    private const string ProposedRuleset = "main-ruleset.proposed.json";
    private const string PullRequest = "pull_request";
    private const string MergeGroup = "merge_group";

    [GeneratedRegex(@"(^|[\s;&|(])python3?(\s|$)", RegexOptions.ExplicitCapture | RegexOptions.Multiline, matchTimeoutMilliseconds: 5000)]
    private static partial Regex PythonCommand { get; }

    public static TheoryData<string, string> RulesetsAndEvents() => new()
    {
        { MainRuleset, PullRequest },
        { MainRuleset, MergeGroup },
        { ProposedRuleset, PullRequest },
        { ProposedRuleset, MergeGroup },
    };

    [Theory]
    [MemberData(nameof(RulesetsAndEvents))]
    public void Every_required_context_is_reported_by_exactly_one_job(string ruleset, string eventName)
    {
        List<(string Context, string Job)> reported = Workflows.Contexts(eventName);
        List<string> problems = [];
        foreach (string context in Rulesets.RequiredContexts(ruleset))
        {
            List<string> jobs = [.. reported.Where(item => string.Equals(item.Context, context, StringComparison.Ordinal)).Select(item => item.Job).Distinct(StringComparer.Ordinal)];
            if (jobs.Count != 1)
            {
                problems.Add($"'{context}' on {eventName}: reported by [{string.Join("; ", jobs)}]");
            }
        }

        Assert.Empty(problems);
    }

    [Theory]
    [InlineData(".github/workflows/build.yml")]
    [InlineData(".github/workflows/codeql.yml")]
    [InlineData(".github/workflows/pr-summary.yml")]
    [InlineData(".github/workflows/sonar.yml")]
    [InlineData(".github/workflows/super-linter.yml")]
    [InlineData(".github/actions/report-status-context/action.yml")]
    public void The_file_runs_no_python(string file)
    {
        YamlMappingNode root = Workflows.Load(Path.Join(Workflows.RepositoryRoot, file));
        IEnumerable<YamlMappingNode> steps = Workflows.Child(root, "jobs") is null
            ? Workflows.Steps((YamlMappingNode)root["runs"])
            : Workflows.Jobs(root).SelectMany(job => Workflows.Steps(job.Value));
        List<string> python =
        [
            .. steps
                .Where(step => PythonCommand.IsMatch(Workflows.Scalar(step, "run") ?? string.Empty) || (Workflows.Scalar(step, "uses") ?? string.Empty).StartsWith("actions/setup-python@", StringComparison.Ordinal))
                .Select(step => Workflows.Scalar(step, "name") ?? Workflows.Scalar(step, "uses") ?? Workflows.Scalar(step, "run")!),
        ];

        Assert.Empty(python);
    }

    [Fact]
    public void The_proposed_ruleset_changes_only_the_required_checks()
    {
        Assert.True(JsonNode.DeepEquals(WithoutChecks(MainRuleset), WithoutChecks(ProposedRuleset)));
    }

    [Theory]
    [InlineData(PullRequest, true)]
    [InlineData(MergeGroup, true)]
    [InlineData("push", false)]
    public void An_event_test_admits_only_the_events_it_names(string eventName, bool admitted)
    {
        Assert.Equal(admitted, Workflows.Admits("${{ (github.event_name == 'pull_request' || github.event_name == 'merge_group') && always() }}", eventName));
    }

    [Fact]
    public void A_condition_without_an_event_test_admits_every_event()
    {
        Assert.True(Workflows.Admits("${{ always() }}", "push"));
        Assert.True(Workflows.Admits(condition: null, "push"));
    }

    private static JsonNode WithoutChecks(string ruleset)
    {
        JsonNode body = Rulesets.Load(ruleset);
        Rulesets.Rule(body, "required_status_checks")["parameters"]!.AsObject().Remove("required_status_checks");
        return body;
    }
}
