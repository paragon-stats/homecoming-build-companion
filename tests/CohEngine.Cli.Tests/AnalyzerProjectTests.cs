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
        int comment = Array.FindIndex(editorConfig, line => line.Contains("\"Sonar way comprehensive\"", StringComparison.Ordinal));
        Assert.True(comment >= 0, "the Sonar block names the profile it mirrors");
        int start = Array.FindIndex(editorConfig, comment, line => line.StartsWith('['));
        Assert.Equal("[*.cs]", editorConfig[start]);
        int end = Array.FindIndex(editorConfig, start + 1, line => line.StartsWith('['));
        string[] block = [.. editorConfig[(start + 1)..(end < 0 ? editorConfig.Length : end)].Where(IsSonarLine)];
        Assert.All(block, line => Assert.True(line.EndsWith(" = warning", StringComparison.Ordinal) || line.EndsWith(" = none", StringComparison.Ordinal), line));
        string[] warnings = [.. block.Where(line => line.EndsWith(" = warning", StringComparison.Ordinal))];
        Assert.Equal(348, warnings.Length);
        foreach (string known in new[] { "S3776", "S1994", "S127", "S3358", "S1192", "S3267" })
        {
            Assert.Contains($"dotnet_diagnostic.{known}.severity = warning", warnings, StringComparer.Ordinal);
        }

        // Outside the block, only the per-file entry that mirrors the server's recorded decision for Exact.cs.
        List<string> outside = [];
        string section = string.Empty;
        for (int i = 0; i < editorConfig.Length; i++)
        {
            section = editorConfig[i].StartsWith('[') ? editorConfig[i] : section;
            if ((i <= start || (end >= 0 && i >= end)) && IsSonarLine(editorConfig[i]))
            {
                outside.Add($"{section} {editorConfig[i]}");
            }
        }

        Assert.Equal(["[src/CohEngine.Core/Maths/Exact.cs] dotnet_diagnostic.S1244.severity = none"], outside);

        // No nested .editorconfig changes a Sonar rule.
        string[] nested = [.. DevScripts.Git(DevScripts.RepositoryRoot, "ls-files", "-z", "--", ":(glob)**/.editorconfig").Split('\0', StringSplitOptions.RemoveEmptyEntries).Where(path => path.Contains('/', StringComparison.Ordinal))];
        Assert.NotEmpty(nested);
        Assert.All(nested, path => Assert.DoesNotContain(File.ReadAllLines(Path.Join(DevScripts.RepositoryRoot, path)), IsSonarLine));
    }

    private static bool IsSonarLine(string line)
    {
        return line.StartsWith("dotnet_diagnostic.S", StringComparison.Ordinal) && line.Length > 19 && char.IsAsciiDigit(line[19]);
    }
}
