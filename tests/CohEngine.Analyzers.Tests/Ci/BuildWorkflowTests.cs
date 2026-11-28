using YamlDotNet.RepresentationModel;

namespace CohEngine.Analyzers.Tests.Ci;

/// <summary>The build workflow's check of the files a change touches.</summary>
public sealed class BuildWorkflowTests
{
    private const string Step = "Check the changed text files";

    [Theory]
    [InlineData("pull_request", true)]
    [InlineData("merge_group", true)]
    [InlineData("push", false)]
    public void The_changed_text_files_are_checked_on_pull_requests_and_in_the_merge_queue(string eventName, bool runs)
    {
        Assert.Equal(runs, Workflows.Admits(Workflows.Scalar(TextStep(), "if"), eventName));
    }

    [Fact]
    public void The_text_check_reads_the_diff_from_a_file_and_the_shas_from_env()
    {
        YamlMappingNode step = TextStep();
        string run = Workflows.Scalar(step, "run")!;
        YamlNode env = Workflows.Child(step, "env")!;

        Assert.Contains("git -c core.quotepath=off diff --name-only \"$env:BASE_SHA...$env:HEAD_SHA\" > $list", run, StringComparison.Ordinal);
        Assert.Contains("if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }", run, StringComparison.Ordinal);
        Assert.Contains("dotnet run scripts/dev/check-text-files.cs -- --files-from $list", run, StringComparison.Ordinal);
        Assert.DoesNotContain("${{", run, StringComparison.Ordinal);
        Assert.Equal("${{ github.event.pull_request.base.sha || github.event.merge_group.base_sha }}", Workflows.Scalar(env, "BASE_SHA"));
        Assert.Equal("${{ github.event.pull_request.head.sha || github.event.merge_group.head_sha }}", Workflows.Scalar(env, "HEAD_SHA"));
    }

    private static YamlMappingNode TextStep() =>
        Workflows.Steps(Workflows.Jobs(Workflows.LoadWorkflow("build.yml")).Single(pair => string.Equals(pair.Key, "build", StringComparison.Ordinal)).Value)
            .Single(step => string.Equals(Workflows.Scalar(step, "name"), Step, StringComparison.Ordinal));
}
