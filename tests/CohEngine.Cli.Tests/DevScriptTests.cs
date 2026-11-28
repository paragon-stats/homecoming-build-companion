using System.Text.Json;
using System.Text.RegularExpressions;

using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

/// <summary>Holds the developer scripts and the Husky hook groups to what the repository promises.</summary>
[Collection(DevScripts.Collection)]
public sealed partial class DevScriptTests
{
    private const string CleanMarkdown = "# Title\n\nA clean line.\n";

    [GeneratedRegex(@"uses: super-linter/super-linter@[0-9a-f]{40}\s+#\s*(?<version>v[0-9][0-9.]*)", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex ActionVersion { get; }

    [GeneratedRegex(@"FILTER_REGEX_EXCLUDE: '(?<filter>[^']*)'", RegexOptions.None, matchTimeoutMilliseconds: 1000)]
    private static partial Regex FilterLine { get; }

    [Fact]
    public void The_commit_hook_groups_need_no_python_and_no_second_exclusion_list()
    {
        using JsonDocument runner = JsonDocument.Parse(File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, ".husky", "task-runner.json")));
        List<(string Group, string Name, string Line)> tasks =
        [
            .. runner.RootElement.GetProperty("tasks").EnumerateArray().Select(task => (
                task.GetProperty("group").GetString()!,
                task.GetProperty("name").GetString()!,
                task.GetProperty("command").GetString() + " " + string.Join(' ', task.GetProperty("args").EnumerateArray().Select(arg => arg.GetString())))),
        ];

        foreach ((string group, string name, string line) in tasks)
        {
            Assert.DoesNotContain("python", line, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("pre_commit", line, StringComparison.Ordinal);
            Assert.DoesNotContain("pre-commit-config", line, StringComparison.Ordinal);
            Assert.DoesNotContain("hook-python", line, StringComparison.Ordinal);
            Assert.False(line.Contains("exclude", StringComparison.OrdinalIgnoreCase), $"{group}/{name} carries its own exclusion list: {line}");
        }

        Assert.Contains(tasks, task => task is { Group: "pre-commit", Name: "check-text-files" });
        Assert.Contains(tasks, task => task is { Group: "pre-commit", Name: "check-git-signing" });
        Assert.Contains(tasks, task => task is { Group: "pre-push", Name: "super-linter" });
    }

    [Fact]
    public void The_text_file_task_run_by_husky_fails_on_a_markdown_line_that_breaks_markdownlint()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("notes.md", "# Title\n\nA line with a trailing space.  \n- one\n* two\n");
        DevScripts.Git(tree.Root, "add", "notes.md");

        (int exit, string output) = DevScripts.Run(tree.Root, "dotnet", "husky", "run", "--group", "pre-commit", "--name", "check-text-files");

        Assert.NotEqual(0, exit);
        Assert.Contains("MD", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_file_task_run_by_husky_fails_on_a_file_missing_its_final_newline()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("notes.md", "# Title\n\nNo newline at the end.");
        DevScripts.Git(tree.Root, "add", "notes.md");

        (int exit, string output) = DevScripts.Run(tree.Root, "dotnet", "husky", "run", "--group", "pre-commit", "--name", "check-text-files");

        Assert.NotEqual(0, exit);
        Assert.Contains("final newline", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_file_task_run_by_husky_passes_on_a_clean_tree()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("notes.md", CleanMarkdown);
        tree.Write("data.json", "{\n  \"a\": 1\n}\n");
        DevScripts.Git(tree.Root, "add", "notes.md", "data.json");

        (int exit, string output) = DevScripts.Run(tree.Root, "dotnet", "husky", "run", "--group", "pre-commit", "--name", "check-text-files");

        Assert.True(exit == 0, output);
    }

    [Theory]
    [InlineData("a.txt", "a  \nb\n", "trailing whitespace")]
    [InlineData("a.txt", "a\r\nb\r\n", "line ending")]
    [InlineData("a.json", "{\"a\": }\n", "JSON")]
    public void The_text_check_reports_each_kind_of_fault(string name, string content, string expected)
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write(name, content);

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files", name);

        Assert.Equal(1, exit);
        Assert.Contains(expected, output, StringComparison.Ordinal);
    }

    // CI hands the pull request's changed files over in a file, which a diff of thousands of paths cannot overflow.
    [Fact]
    public void The_text_check_reads_the_files_to_check_from_a_list_and_skips_deleted_ones()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("clean.txt", "a\n");
        tree.Write("dirty.txt", "a  \n");
        tree.Write("changed.txt", "clean.txt\n\ngone.txt\n");

        (int cleanExit, string cleanOutput) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files-from", "changed.txt");
        tree.Write("changed.txt", "clean.txt\ndirty.txt\n");
        (int dirtyExit, string dirtyOutput) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files-from", "changed.txt");
        (int missingExit, string missingOutput) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files-from", "no-such-list.txt");

        Assert.True(cleanExit == 0, cleanOutput);
        Assert.Equal(1, dirtyExit);
        Assert.Contains("dirty.txt:1:", dirtyOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("clean.txt:", dirtyOutput, StringComparison.Ordinal);
        Assert.Equal(1, missingExit);
        Assert.Contains("H-TEXT-012", missingOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_check_follows_the_editorconfig_for_markdown_whitespace_and_the_tool_manifest()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("two.md", "# Title\n\nA hard break  \nnext line\n");
        string manifest = File.ReadAllText(tree.FullPath(".config/dotnet-tools.json")).TrimEnd('\n');
        tree.Write(".config/dotnet-tools.json", manifest);

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root, "--files", "two.md", ".config/dotnet-tools.json");

        Assert.True(exit == 0, output);
    }

    [Fact]
    public void The_text_check_skips_what_the_ci_filter_skips_and_nothing_else()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        string[] skipped = ["samples/a.txt", "src/x/fixtures/a.txt", "data/canonical/a.txt", "data/diff/a.txt", "inbox/a.txt", "LICENSE"];
        string[] checkedPaths = ["docs/a.txt", "src/a.txt"];
        foreach (string path in skipped.Concat(checkedPaths))
        {
            tree.Write(path, "trailing  \n");
        }

        (int skippedExit, string skippedOutput) = DevScripts.RunScript("check-text-files.cs", tree.Root, ["--files", .. skipped]);
        (int checkedExit, string checkedOutput) = DevScripts.RunScript("check-text-files.cs", tree.Root, ["--files", .. checkedPaths]);

        Assert.True(skippedExit == 0, skippedOutput);
        Assert.Equal(1, checkedExit);
        Assert.All(checkedPaths, path => Assert.Contains(path, checkedOutput, StringComparison.Ordinal));
    }

    [Fact]
    public void The_super_linter_runner_takes_the_image_and_the_filter_from_the_ci_workflow()
    {
        string workflow = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, ".github", "workflows", "super-linter.yml"));
        string version = ActionVersion.Match(workflow).Groups["version"].Value;
        string filter = FilterLine.Match(workflow).Groups["filter"].Value;
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);

        (int exit, string output) = DevScripts.RunScript("run-super-linter.cs", tree.Root, "--print-plan");

        Assert.True(exit == 0, output);
        Assert.Contains($"ghcr.io/super-linter/super-linter:{version}", output, StringComparison.Ordinal);
        Assert.Contains(filter, output, StringComparison.Ordinal);
        Assert.Contains("--env-file", output, StringComparison.Ordinal);
        Assert.Contains("RUN_LOCAL=true", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_super_linter_runner_fails_when_the_workflow_has_no_pinned_action()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write(".github/workflows/super-linter.yml", "name: Super-Linter\n");

        (int exit, string output) = DevScripts.RunScript("run-super-linter.cs", tree.Root, "--print-plan");

        Assert.NotEqual(0, exit);
        Assert.Contains("super-linter.yml", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_super_linter_runner_mounts_a_linked_worktree_through_its_shared_git_directory()
    {
        using TempTree main = new();
        DevScripts.Git(main.Root, "init", "--quiet", "--initial-branch=main");
        DevScripts.Git(main.Root, "-c", "user.name=t", "-c", "user.email=t@example.invalid", "-c", "commit.gpgsign=false", "commit", "--allow-empty", "--quiet", "-m", "init");
        string linked = main.FullPath("linked");
        DevScripts.Git(main.Root, "worktree", "add", "--quiet", linked);
        Directory.CreateDirectory(Path.Join(linked, ".github", "workflows"));
        File.Copy(Path.Join(DevScripts.RepositoryRoot, ".github", "workflows", "super-linter.yml"), Path.Join(linked, ".github", "workflows", "super-linter.yml"));

        string gitDir = Directory.GetDirectories(Path.Join(main.Root, ".git", "worktrees")).Single();
        string gitFile = Path.Join(gitDir, "super-linter.gitfile");

        (int planExit, string plan) = DevScripts.RunScript("run-super-linter.cs", linked, "--print-plan");

        Assert.True(planExit == 0, plan);
        Assert.Contains(":/git-common", plan, StringComparison.Ordinal);
        Assert.Contains("super-linter.gitfile", plan, StringComparison.Ordinal);
        Assert.False(File.Exists(gitFile), "--print-plan must write nothing");

        // node -p 0 stands in for docker: it exits 0 whatever follows it, so the real run reaches the point where the file is written.
        Dictionary<string, string?> fakeDocker = new(StringComparer.Ordinal)
        {
            ["SUPER_LINTER_DOCKER_CMD"] = $"\"{DevScripts.Locate("node")}\" -p 0",
            ["SUPER_LINTER_PULL_POLICY"] = "missing",
        };
        (int runExit, string run) = DevScripts.RunScriptWithEnvironment("run-super-linter.cs", linked, fakeDocker);

        Assert.True(runExit == 0, run);
        Assert.Equal($"gitdir: /git-common/worktrees/{Path.GetFileName(gitDir)}", File.ReadAllText(gitFile));
    }

    [Fact]
    public void The_signing_check_fails_with_a_diagnostic_when_signing_is_off_and_passes_with_a_working_key()
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");
        DevScripts.Git(tree.Root, "config", "commit.gpgsign", "false");

        (int offExit, string offOutput) = DevScripts.RunScript("check-git-signing.cs", tree.Root);

        Assert.Equal(1, offExit);
        Assert.Contains("H-SIGN-001", offOutput, StringComparison.Ordinal);

        string key = tree.FullPath("signing-key");
        (int keygen, string keygenOutput) = DevScripts.Run(tree.Root, "ssh-keygen", "-q", "-t", "ed25519", "-N", string.Empty, "-f", key);
        Assert.True(keygen == 0, keygenOutput);
        DevScripts.Git(tree.Root, "config", "commit.gpgsign", "true");
        DevScripts.Git(tree.Root, "config", "gpg.format", "ssh");
        DevScripts.Git(tree.Root, "config", "user.signingkey", key);

        (int onExit, string onOutput) = DevScripts.RunScript("check-git-signing.cs", tree.Root);

        Assert.True(onExit == 0, onOutput);
    }

    [Fact]
    public void The_hook_check_names_the_install_command_when_git_does_not_run_the_husky_hooks()
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");

        (int exit, string output) = DevScripts.RunScript("bootstrap.cs", tree.Root, "--verify");

        Assert.Equal(1, exit);
        Assert.Contains("dotnet husky install", output, StringComparison.Ordinal);
    }
}
