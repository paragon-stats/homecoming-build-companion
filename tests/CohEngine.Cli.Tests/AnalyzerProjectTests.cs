using System.Text.Json;

namespace CohEngine.Cli.Tests;

/// <summary>Holds the analyzer project's build configuration to what the release-tracking analyzer and dotnet format need.</summary>
public sealed class AnalyzerProjectTests
{
    [Fact]
    public void The_release_tracking_files_reach_the_analyzer_once_each()
    {
        string project = Path.Join(DevScripts.RepositoryRoot, "src", "CohEngine.Analyzers", "CohEngine.Analyzers.csproj");

        (int exit, string output) = DevScripts.Run(DevScripts.RepositoryRoot, "dotnet", "msbuild", project, "-getItem:AdditionalFiles");

        Assert.True(exit == 0, output);
        using JsonDocument items = JsonDocument.Parse(output);
        string[] names =
        [
            .. items.RootElement.GetProperty("Items").GetProperty("AdditionalFiles").EnumerateArray()
                .Select(item => Path.GetFileName(item.GetProperty("FullPath").GetString()!)),
        ];

        // A file listed twice makes dotnet format warn "Duplicate source file" while loading the workspace.
        Assert.Single(names, name => string.Equals(name, "AnalyzerReleases.Shipped.md", StringComparison.Ordinal));
        Assert.Single(names, name => string.Equals(name, "AnalyzerReleases.Unshipped.md", StringComparison.Ordinal));
    }
}
