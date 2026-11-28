using System.Text.RegularExpressions;

using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

public sealed partial class DevScriptTests
{
    private const string ValidToml = "[tool.a]\nkey = \"value\"\nlist = [\"x\", \"y\"]\n";
    private const string BrokenToml = "[tool.a]\nkey = \"value\nother = 1\n";

    [GeneratedRegex(@"<PackageVersion Include=""Tomlyn"" Version=""\d+\.\d+\.\d+"" />", RegexOptions.None, matchTimeoutMilliseconds: 5000)]
    private static partial Regex TomlynPin { get; }

    [Fact]
    public void The_text_check_passes_a_valid_toml_file()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("config.toml", ValidToml);

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files", "config.toml");

        Assert.True(exit == 0, output);
    }

    [Fact]
    public void The_text_check_names_a_toml_file_with_a_syntax_error()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("config.toml", BrokenToml);

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files", "config.toml");

        Assert.Equal(1, exit);
        Assert.Matches(@"config\.toml:\d+:\d+: error: H-TEXT-014: invalid TOML: ", output);
    }

    [Theory]
    [InlineData(BrokenToml, ValidToml, 1)]
    [InlineData(ValidToml, BrokenToml, 0)]
    public void The_commit_hook_judges_the_staged_toml_not_the_working_copy(string staged, string working, int expected)
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("config.toml", staged);
        DevScripts.Git(tree.Root, "add", "--", "config.toml");
        tree.Write("config.toml", working);

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.True(exit == expected, output);
    }

    [Fact]
    public void The_text_check_reads_toml_from_the_ci_file_list()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("config.toml", BrokenToml);
        tree.Write("changed.list", "config.toml\0");

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files-from", "changed.list");

        Assert.Equal(1, exit);
        Assert.Contains("H-TEXT-014", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_repository_pyproject_is_valid_toml()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("pyproject.toml", File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "pyproject.toml")));

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files", "pyproject.toml");

        Assert.True(exit == 0, output);
    }

    // Central package management refuses a version on the directive, so the script names the package and the pin file holds the exact version.
    [Fact]
    public void The_toml_parser_is_pinned_to_an_exact_version_in_the_pin_file()
    {
        Assert.Contains("#:package Tomlyn\n", File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "scripts", "dev", "check-text-files.cs")).ReplaceLineEndings("\n"), StringComparison.Ordinal);
        Assert.Matches(TomlynPin, File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, "Directory.Packages.props")));
    }
}
