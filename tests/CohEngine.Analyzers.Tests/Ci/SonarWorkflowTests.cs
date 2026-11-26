using System.Text.RegularExpressions;

using YamlDotNet.RepresentationModel;

namespace CohEngine.Analyzers.Tests.Ci;

/// <summary>The SonarQube scanner settings and job condition in the sonar workflow.</summary>
public sealed class SonarWorkflowTests
{
    private const string Workflow = "sonar.yml";

    [Theory]
    [InlineData("sonar.exclusions")]
    [InlineData("sonar.cpd.exclusions")]
    public void Nested_test_fixtures_are_excluded(string property)
    {
        string text = File.ReadAllText(Path.Join(Workflows.WorkflowDirectory, Workflow));
        Match match = Regex.Match(text, $"/d:{Regex.Escape(property)}=\"(?<patterns>[^\"]*)\"", RegexOptions.ExplicitCapture, TimeSpan.FromSeconds(5));

        Assert.True(match.Success);
        Assert.Contains("**/tests/fixtures/**", match.Groups["patterns"].Value.Split(","), StringComparer.Ordinal);
    }

    [Fact]
    public void The_scan_skips_fork_and_dependabot_pull_requests_but_runs_on_push()
    {
        YamlMappingNode job = SonarJob();

        Assert.Equal(
            "${{ github.event_name != 'pull_request' || (!github.event.pull_request.head.repo.fork && github.actor != 'dependabot[bot]') }}",
            Workflows.Scalar(job, "if"));
    }

    [Fact]
    public void The_rule_gate_runs_the_dotnet_script_with_the_rules_and_scanner_output()
    {
        YamlMappingNode job = SonarJob();
        string gate = Workflows.Scalar(Workflows.Steps(job).Single(step => string.Equals(Workflows.Scalar(step, "name"), "Sonar rule gate", StringComparison.Ordinal)), "run")!;

        Assert.Contains("dotnet run scripts/ci/sonar-gate.cs", gate, StringComparison.Ordinal);
        Assert.Contains("'--rules-dir', 'scripts/ci/sonar_gate_rules', '--sarif-root', '.sonarqube/out'", gate, StringComparison.Ordinal);
    }

    private static YamlMappingNode SonarJob() =>
        Workflows.Jobs(Workflows.LoadWorkflow(Workflow)).Single(pair => string.Equals(pair.Key, "sonar", StringComparison.Ordinal)).Value;
}
