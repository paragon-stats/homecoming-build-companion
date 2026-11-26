using System.Text.Json;
using System.Text.Json.Nodes;

using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

public sealed partial class DevScriptTests
{
    private static readonly string[] HookScripts = ["check-encoding.cs", "check-banned-words.cs", "check-coverage-exclusions.cs"];

    [Fact]
    public void The_hook_groups_run_their_quickest_checks_first_and_the_hooks_restore_no_tools()
    {
        using JsonDocument runner = JsonDocument.Parse(File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, ".husky", "task-runner.json")));
        string[] Group(string group) =>
        [
            .. runner.RootElement.GetProperty("tasks").EnumerateArray()
                .Where(task => string.Equals(task.GetProperty("group").GetString(), group, StringComparison.Ordinal))
                .Select(task => task.GetProperty("name").GetString()!),
        ];

        Assert.Equal(["check-text-files", "check-encoding", "check-coverage-exclusions", "check-git-signing", "check-banned-words", "dotnet-format"], Group("pre-commit"));
        Assert.Equal(["check-banned-words-push", "build", "super-linter", "test"], Group("pre-push"));
        foreach (string hook in new[] { "pre-commit", "pre-push" })
        {
            string text = File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, ".husky", hook));
            Assert.DoesNotContain("tool restore", text.Replace("dotnet tool restore && dotnet husky install", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
            Assert.Contains($"dotnet husky run --quiet --group {hook}", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_commit_hook_prints_one_outcome_line_per_check_and_no_husky_banner()
    {
        using TempTree tree = SeedHookRepository();

        (int exit, string output, string standardOutput, string standardError) = RunHook(tree, "pre-commit");

        Assert.True(exit == 0, output);
        Assert.DoesNotContain("[Husky]", output, StringComparison.Ordinal);
        Assert.Equal(string.Empty, standardError);
        string[] lines = standardOutput.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5, lines.Length);
        Assert.All(lines, line => Assert.True(line.StartsWith("[OK] ", StringComparison.Ordinal) || line.StartsWith("[SKIP] ", StringComparison.Ordinal), line));
        Assert.All(lines, line => Assert.All(line, character => Assert.InRange(character, ' ', '~')));
    }

    [Fact]
    public void A_failing_check_fails_the_commit_hook_with_its_detail_on_standard_error()
    {
        using TempTree tree = SeedHookRepository();
        tree.Write("notes.txt", "trailing  \n");
        DevScripts.Git(tree.Root, "add", "notes.txt");

        (int exit, string output, string standardOutput, string standardError) = RunHook(tree, "pre-commit");

        Assert.True(exit != 0, output);
        Assert.Contains("notes.txt:1", standardError, StringComparison.Ordinal);
        Assert.Contains("trailing whitespace", standardError, StringComparison.Ordinal);
        Assert.DoesNotContain("trailing whitespace", standardOutput, StringComparison.Ordinal);
        Assert.DoesNotContain("[OK]", standardOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void The_push_hook_runs_its_group_quietly()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        File.Copy(Path.Join(DevScripts.RepositoryRoot, ".husky", "pre-push"), tree.FullPath(".husky/pre-push"));
        JsonArray tasks = [];
        foreach (string name in new[] { "first", "second" })
        {
            tasks.Add(new JsonObject { ["name"] = name, ["group"] = "pre-push", ["command"] = "git", ["args"] = new JsonArray("--version") });
        }

        tree.Write(".husky/task-runner.json", new JsonObject { ["tasks"] = tasks }.ToJsonString());

        (int exit, string output, string standardOutput, _) = DevScripts.RunSeparately(tree.Root, DevScripts.Bash(), ShellEnvironment(), "refs/heads/x 1 refs/heads/x 0\n", ".husky/pre-push");

        Assert.True(exit == 0, output);
        Assert.DoesNotContain("[Husky]", output, StringComparison.Ordinal);
        Assert.Equal(2, standardOutput.ReplaceLineEndings("\n").Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
    }

    [Fact]
    public void The_hooks_and_the_environment_check_name_the_restore_when_the_husky_tool_is_missing()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        File.Copy(Path.Join(DevScripts.RepositoryRoot, ".husky", "pre-commit"), tree.FullPath(".husky/pre-commit"));
        string manifest = tree.FullPath(".config/dotnet-tools.json");
        JsonNode tools = JsonNode.Parse(File.ReadAllText(manifest))!;
        tools["tools"]!["husky"]!["version"] = "0.0.1-never-restored";
        File.WriteAllText(manifest, tools.ToJsonString());

        (int hookExit, string hookOutput, _, string hookError) = RunHook(tree, "pre-commit");
        (int verifyExit, string verifyOutput) = DevScripts.RunScript("bootstrap.cs", tree.Root, "--verify");

        Assert.True(hookExit != 0, hookOutput);
        Assert.Contains("dotnet tool restore", hookError, StringComparison.Ordinal);
        Assert.Equal(1, verifyExit);
        Assert.Contains("local .NET tools are not restored - run: dotnet tool restore", verifyOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void The_super_linter_runner_announces_the_run_on_standard_output_and_ends_with_one_outcome_line()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);

        // bash -c true stands in for a Docker that accepts every command.
        Dictionary<string, string?> docker = new(StringComparer.Ordinal)
        {
            ["SUPER_LINTER_DOCKER_CMD"] = $"\"{DevScripts.Bash()}\" -c true",
            ["SUPER_LINTER_PULL_POLICY"] = "missing",
            ["SUPER_LINTER_VALIDATE_ALL_CODEBASE"] = "false",
        };
        string script = Path.Join(DevScripts.RepositoryRoot, "scripts", "dev", "run-super-linter.cs");

        (int exit, string output, string standardOutput, string standardError) = DevScripts.RunSeparately(tree.Root, "dotnet", docker, null, "run", script);

        Assert.True(exit == 0, output);
        Assert.Contains("[INFO] running Super-Linter locally", standardOutput, StringComparison.Ordinal);
        Assert.EndsWith("[OK] Super-Linter found nothing to fix.", standardOutput.TrimEnd(), StringComparison.Ordinal);
        Assert.Equal(string.Empty, standardError);
    }

    // The pre-commit checks this repository wrote, as Husky runs them; dotnet-format needs a solution and prints the SDK's own output.
    private static TempTree SeedHookRepository()
    {
        TempTree tree = new();
        DevScripts.SeedRepository(tree);
        File.Copy(Path.Join(DevScripts.RepositoryRoot, ".husky", "pre-commit"), tree.FullPath(".husky/pre-commit"));
        foreach (string script in HookScripts)
        {
            File.Copy(Path.Join(DevScripts.RepositoryRoot, "scripts", "dev", script), tree.FullPath($"scripts/dev/{script}"));
        }

        JsonNode runner = JsonNode.Parse(File.ReadAllText(tree.FullPath(".husky/task-runner.json")))!;
        JsonArray tasks = runner["tasks"]!.AsArray();
        foreach (JsonNode? task in tasks.Where(task => !string.Equals((string?)task!["group"], "pre-commit", StringComparison.Ordinal) || string.Equals((string?)task!["name"], "dotnet-format", StringComparison.Ordinal)).ToList())
        {
            tasks.Remove(task);
        }

        File.WriteAllText(tree.FullPath(".husky/task-runner.json"), runner.ToJsonString());

        DevScripts.Git(tree.Root, "config", "commit.gpgsign", "true");
        DevScripts.Git(tree.Root, "config", "gpg.format", "ssh");
        DevScripts.Git(tree.Root, "config", "user.signingkey", SigningKeyLiteral);
        DevScripts.Git(tree.Root, "config", "gpg.ssh.program", "signer");
        tree.Write(OperatingSystem.IsWindows() ? "bin/signer.cmd" : "bin/signer", "exit 0\n");
        return tree;
    }

    private static (int Exit, string Output, string StandardOutput, string StandardError) RunHook(TempTree tree, string hook)
    {
        return DevScripts.RunSeparately(tree.Root, DevScripts.Bash(), ShellEnvironment(tree), null, $".husky/{hook}");
    }

    // No operator name and no CI: the banned-words check takes its skip path, which must still print one line.
    private static Dictionary<string, string?> ShellEnvironment(TempTree? tree = null)
    {
        string path = Path.GetDirectoryName(DevScripts.Bash()) + Path.PathSeparator + DevScripts.PathHolding("git", "dotnet", "node");
        return new(StringComparer.Ordinal)
        {
            ["PATH"] = tree is null ? path : tree.FullPath("bin") + Path.PathSeparator + path,
            ["COH_BANNED_NAMES"] = null,
            ["GITHUB_ACTIONS"] = null,
        };
    }
}
