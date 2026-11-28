using YamlDotNet.RepresentationModel;

namespace CohEngine.Analyzers.Tests.Ci;

/// <summary>The SBOM the build job proves on every pull request and the release workflow attaches to each release.</summary>
public sealed class SbomWorkflowTests
{
    private const string Generate = "dotnet tool run dotnet-CycloneDX src/CohEngine.Cli/CohEngine.Cli.csproj -F Json --output $env:RUNNER_TEMP --filename coh-engine.cdx.json --set-name coh-engine --set-version $version";
    private const string ShippedCheck = "if (-not ($shipped | Where-Object name -eq 'ModelContextProtocol'))";

    [Fact]
    public void The_build_job_generates_the_sbom_and_fails_without_a_known_shipped_package()
    {
        string run = Workflows.Scalar(Step("build.yml", "build", "Generate the SBOM"), "run")!;

        Assert.Contains("$version = '0.0.0'", run, StringComparison.Ordinal);
        Assert.Contains(Generate, run, StringComparison.Ordinal);
        Assert.Contains("if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }", run, StringComparison.Ordinal);
        Assert.Contains(ShippedCheck, run, StringComparison.Ordinal);
        Assert.DoesNotContain("${{", run, StringComparison.Ordinal);
    }

    [Fact]
    public void The_release_exposes_whether_it_cut_a_release_and_its_tag()
    {
        YamlMappingNode outputs = (YamlMappingNode)Workflows.Child(Job("release"), "outputs")!;

        Assert.Equal("${{ steps.release.outputs.released }}", Workflows.Scalar(outputs, "released"));
        Assert.Equal("${{ steps.release.outputs.tag }}", Workflows.Scalar(outputs, "tag"));
    }

    [Fact]
    public void The_sbom_is_attached_only_when_this_run_cut_a_release()
    {
        YamlMappingNode job = Job("sbom");
        YamlMappingNode generate = Step("release.yml", "sbom", "Generate the SBOM");
        YamlMappingNode upload = Step("release.yml", "sbom", "Attach the SBOM to the release");
        string generateRun = Workflows.Scalar(generate, "run")!;
        string uploadRun = Workflows.Scalar(upload, "run")!;

        Assert.Equal("release", Workflows.Scalar(job, "needs"));
        Assert.Equal("needs.release.outputs.released == 'true'", Workflows.Scalar(job, "if"));
        Assert.Equal("write", Workflows.Scalar(Workflows.Child(job, "permissions")!, "contents"));

        // The generating step restores packages, whose MSBuild files run: it gets the tag and nothing that can write.
        Assert.Equal("${{ needs.release.outputs.tag }}", Workflows.Scalar(Workflows.Child(generate, "env")!, "TAG"));
        Assert.Null(Workflows.Scalar(Workflows.Child(generate, "env")!, "GH_TOKEN"));
        Assert.Contains("$version = $env:TAG -replace '^v', ''", generateRun, StringComparison.Ordinal);
        Assert.Contains(Generate, generateRun, StringComparison.Ordinal);
        Assert.Contains(ShippedCheck, generateRun, StringComparison.Ordinal);
        Assert.DoesNotContain("gh ", generateRun, StringComparison.Ordinal);

        Assert.Equal("${{ github.token }}", Workflows.Scalar(Workflows.Child(upload, "env")!, "GH_TOKEN"));
        Assert.Equal("${{ needs.release.outputs.tag }}", Workflows.Scalar(Workflows.Child(upload, "env")!, "TAG"));
        Assert.Equal(
            "gh release upload $env:TAG (Join-Path $env:RUNNER_TEMP 'coh-engine.cdx.json') --repo $env:GITHUB_REPOSITORY --clobber\nif ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }\n",
            uploadRun);
        Assert.DoesNotContain("${{", generateRun + uploadRun, StringComparison.Ordinal);
    }

    private static YamlMappingNode Job(string name) =>
        Workflows.Jobs(Workflows.LoadWorkflow("release.yml")).Single(pair => string.Equals(pair.Key, name, StringComparison.Ordinal)).Value;

    private static YamlMappingNode Step(string workflow, string job, string name) =>
        Workflows.Steps(Workflows.Jobs(Workflows.LoadWorkflow(workflow)).Single(pair => string.Equals(pair.Key, job, StringComparison.Ordinal)).Value)
            .Single(step => string.Equals(Workflows.Scalar(step, "name"), name, StringComparison.Ordinal));
}
