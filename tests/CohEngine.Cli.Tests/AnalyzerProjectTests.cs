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

    [Fact]
    public void Every_project_runs_the_sonar_rules_the_server_applies()
    {
        string build = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "Directory.Build.props"));
        string packages = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "Directory.Packages.props"));
        string[] editorConfig = File.ReadAllLines(Path.Join(DevScripts.RepositoryRoot, ".editorconfig"));

        Assert.Contains("<PackageReference Include=\"SonarAnalyzer.CSharp\" PrivateAssets=\"all\" />", build, StringComparison.Ordinal);
        Assert.Contains("<PackageVersion Include=\"SonarAnalyzer.CSharp\" Version=\"", packages, StringComparison.Ordinal);

        // One list, mirroring the SonarQube Cloud profile: its active rules are warnings, the build makes them errors.
        Assert.Contains(editorConfig, line => line.Contains("\"Sonar way comprehensive\"", StringComparison.Ordinal));
        Assert.Equal(348, editorConfig.Count(line => line.StartsWith("dotnet_diagnostic.S", StringComparison.Ordinal) && line.EndsWith(".severity = warning", StringComparison.Ordinal)));
        Assert.Equal(2, editorConfig.Count(line => line.StartsWith("dotnet_diagnostic.S", StringComparison.Ordinal) && line.EndsWith(".severity = none", StringComparison.Ordinal) && (line.Contains("S107.", StringComparison.Ordinal) || line.Contains("S1244.", StringComparison.Ordinal))));
    }
}
