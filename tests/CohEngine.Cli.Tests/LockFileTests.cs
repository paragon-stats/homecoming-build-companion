using System.Text.RegularExpressions;

namespace CohEngine.Cli.Tests;

/// <summary>Every project in the solution restores from a committed lock file, and CI refuses one that would change.</summary>
public sealed partial class LockFileTests
{
    [GeneratedRegex(@"<Project Path=""(?<path>[^""]+\.csproj)""", RegexOptions.ExplicitCapture, matchTimeoutMilliseconds: 5000)]
    private static partial Regex SolutionProject { get; }

    [Fact]
    public void The_lock_file_is_on_for_projects_and_locked_in_ci()
    {
        string build = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "Directory.Build.props"));

        Assert.Contains("<RestorePackagesWithLockFile Condition=\"'$(FileBasedProgram)' != 'true'\">true</RestorePackagesWithLockFile>", build, StringComparison.Ordinal);
        Assert.Contains("<RestoreLockedMode Condition=\"'$(CI)' == 'true' And '$(FileBasedProgram)' != 'true'\">true</RestoreLockedMode>", build, StringComparison.Ordinal);
    }

    // A Native AOT restore adds the host runtime, so a lock file written on Windows or on Linux must already list both.
    [Fact]
    public void The_cli_lock_file_covers_the_published_and_the_host_runtimes()
    {
        string lockFile = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "src", "CohEngine.Cli", "packages.lock.json"));

        Assert.Contains("\"net10.0/win-x64\": {", lockFile, StringComparison.Ordinal);
        Assert.Contains("\"net10.0/linux-x64\": {", lockFile, StringComparison.Ordinal);
    }

    [Fact]
    public void Every_project_in_the_solution_has_a_committed_lock_file()
    {
        string[] projects = [.. SolutionProject.Matches(File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "CohEngine.slnx"))).Select(match => match.Groups["path"].Value.Replace('\\', '/'))];
        string[] tracked = DevScripts.Git(DevScripts.RepositoryRoot, "ls-files", "-z", "--", ":(glob)**/packages.lock.json").Split('\0', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        Assert.NotEmpty(projects);
        Assert.Equal(
            projects.Select(project => $"{Path.GetDirectoryName(project)!.Replace('\\', '/')}/packages.lock.json").Order(StringComparer.Ordinal),
            tracked.Order(StringComparer.Ordinal),
            StringComparer.Ordinal);
    }
}
