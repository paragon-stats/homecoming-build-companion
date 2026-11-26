using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

public sealed partial class DevScriptTests
{
    private const string SigningKeyLiteral = "ssh-ed25519 AAAAC3NzaC1lZDI1NTE5AAAAIAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA test";

    [Fact]
    public void The_text_check_judges_the_staged_content_not_the_working_tree()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("staged-bad.txt", "trailing  \n");
        tree.Write("staged-clean.txt", "clean\n");
        tree.Write("staged-deleted.txt", "trailing  \n");
        DevScripts.Git(tree.Root, "add", "staged-bad.txt", "staged-clean.txt", "staged-deleted.txt");
        tree.Write("staged-bad.txt", "now clean\n");
        tree.Write("staged-clean.txt", "now trailing  \n");
        File.Delete(tree.FullPath("staged-deleted.txt"));

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.Equal(1, exit);
        Assert.Contains("staged-bad.txt:1", output, StringComparison.Ordinal);
        Assert.Contains("staged-deleted.txt:1", output, StringComparison.Ordinal);
        Assert.DoesNotContain("staged-clean.txt", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_check_skips_a_staged_symlink_and_a_submodule()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);

        // A symlink's blob is its target, written with no final newline; a submodule entry names a commit this repository lacks.
        tree.Write("link-target", "target  ");
        string blob = DevScripts.Git(tree.Root, "hash-object", "-w", "link-target").Trim();
        DevScripts.Git(tree.Root, "update-index", "--add", "--cacheinfo", $"120000,{blob},link.txt");
        DevScripts.Git(tree.Root, "update-index", "--add", "--cacheinfo", $"160000,{new string('1', 40)},vendor/sub");

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.True(exit == 0, output);
    }

    [Fact]
    public void The_text_check_reads_a_staged_file_with_a_non_ascii_name()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("café.txt", "trailing  \n");
        DevScripts.Git(tree.Root, "add", "--all");

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.Equal(1, exit);
        Assert.Contains("trailing whitespace", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_check_fails_when_git_cannot_list_the_files()
    {
        using TempTree tree = new();
        tree.Write(".editorconfig", "root = true\n");
        tree.Write(".github/workflows/super-linter.yml", File.ReadAllText(Path.Join(DevScripts.RepositoryRoot, ".github", "workflows", "super-linter.yml")));

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.Equal(1, exit);
        Assert.Contains("git", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_text_check_lints_a_staged_markdown_file_whose_name_has_a_space()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("my notes.md", "# Title\n\n- one\n* two\n");
        DevScripts.Git(tree.Root, "add", "--", "my notes.md");

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.Equal(1, exit);
        Assert.Contains("my notes.md:4", output, StringComparison.Ordinal);
        Assert.Contains("MD004", output, StringComparison.Ordinal);
    }

    // A '|' cannot be in a Windows file name, so the Windows CI cannot stage one.
    [Theory]
    [InlineData('&')]
    [InlineData('^')]
    [InlineData('%')]
    [InlineData('!')]
    public void The_text_check_never_hands_a_file_name_with_shell_characters_to_a_shell(char shellCharacter)
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        string name = $"x{shellCharacter}{Environment.MachineName}{shellCharacter}.md";
        tree.Write(name, CleanMarkdown);
        DevScripts.Git(tree.Root, "add", "--", name);

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.Equal(1, exit);
        Assert.Contains("H-TEXT-008", output, StringComparison.Ordinal);
        Assert.DoesNotContain(output.ReplaceLineEndings("\n").Split('\n'), line => string.Equals(line.Trim(), Environment.MachineName, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void The_text_check_names_a_missing_npx_instead_of_failing_obscurely()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("notes.md", CleanMarkdown);
        DevScripts.Git(tree.Root, "add", "notes.md");
        Dictionary<string, string?> noNode = new(StringComparer.Ordinal) { ["PATH"] = DevScripts.PathHolding("git", "dotnet") };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("check-text-files.cs", tree.Root, noNode);

        Assert.Equal(1, exit);
        Assert.Contains("npx not found", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_check_validates_json_even_where_the_ci_filter_skips_the_file_and_flags_extra_blank_lines()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("samples/bad.json", "{\"a\": }\n");
        tree.Write("docs/extra.txt", "text\n\n");
        DevScripts.Git(tree.Root, "add", "--all");

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.Equal(1, exit);
        Assert.Contains("samples/bad.json", output, StringComparison.Ordinal);
        Assert.Contains("docs/extra.txt", output, StringComparison.Ordinal);
        Assert.Contains("blank line", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("absolute")]
    [InlineData("relative")]
    [InlineData("trailing separator")]
    [InlineData("other drive-letter case")]
    public void The_hook_check_accepts_any_spelling_of_this_checkouts_hooks_path(string spelling)
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");
        foreach (string hook in new[] { "pre-commit", "commit-msg", "pre-push" })
        {
            tree.Write($".husky/{hook}", "#!/bin/sh\n");
        }

        string absolute = tree.FullPath(".husky");
        string hooksPath = spelling switch
        {
            "absolute" => absolute,
            "relative" => ".husky",
            "trailing separator" => ".husky/",
            _ => (char.IsUpper(absolute[0]) ? char.ToLowerInvariant(absolute[0]) : char.ToUpperInvariant(absolute[0])) + absolute[1..],
        };
        DevScripts.Git(tree.Root, "config", "core.hooksPath", hooksPath);

        (int exit, string output) = DevScripts.RunScript("bootstrap.cs", tree.Root, "--verify");

        Assert.Equal(1, exit);
        Assert.DoesNotContain("hooks not installed", output, StringComparison.Ordinal);
        Assert.Contains("commit.gpgsign", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hook_check_rejects_a_hooks_path_that_names_another_checkout()
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");
        DevScripts.Git(tree.Root, "config", "core.hooksPath", Path.Join(Path.GetTempPath(), "elsewhere", ".husky"));

        (int exit, string output) = DevScripts.RunScript("bootstrap.cs", tree.Root, "--verify");

        Assert.Equal(1, exit);
        Assert.Contains("hooks not installed", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_hook_check_needs_no_python()
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");
        Dictionary<string, string?> noPython = new(StringComparer.Ordinal) { ["PATH"] = DevScripts.PathHolding("git", "dotnet") };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("bootstrap.cs", tree.Root, noPython, "--verify");

        Assert.Equal(1, exit);
        Assert.DoesNotContain("python", output, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void The_super_linter_runner_skips_when_docker_is_not_installed()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        Dictionary<string, string?> noDocker = new(StringComparer.Ordinal) { ["PATH"] = DevScripts.PathHolding("git", "dotnet") };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("run-super-linter.cs", tree.Root, noDocker);

        Assert.True(exit == 0, output);
        Assert.Contains("[SKIP]", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_super_linter_runner_fails_when_the_docker_override_is_missing()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        Dictionary<string, string?> missing = new(StringComparer.Ordinal) { ["SUPER_LINTER_DOCKER_CMD"] = "no-such-docker-program" };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("run-super-linter.cs", tree.Root, missing);

        Assert.Equal(1, exit);
        Assert.Contains("not found", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_super_linter_runner_fails_with_a_fix_when_docker_is_found_but_not_usable()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);

        // A quoted path with spaces must reach the shell-free launcher whole; dotnet rejects the word "version", as a stopped daemon does.
        Dictionary<string, string?> unusable = new(StringComparer.Ordinal) { ["SUPER_LINTER_DOCKER_CMD"] = $"\"{DevScripts.Locate("dotnet")}\"" };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("run-super-linter.cs", tree.Root, unusable);

        Assert.Equal(1, exit);
        Assert.Contains("not usable", output, StringComparison.Ordinal);
        Assert.Contains("start Docker", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(SigningKeyLiteral)]
    [InlineData("key::" + SigningKeyLiteral)]
    public void The_signing_check_accepts_a_literal_key_and_a_signer_found_through_pathext(string signingKey)
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");
        DevScripts.Git(tree.Root, "config", "commit.gpgsign", "TRUE");
        DevScripts.Git(tree.Root, "config", "gpg.format", "ssh");
        DevScripts.Git(tree.Root, "config", "user.signingkey", signingKey);

        // The signer exists only with an extension PATHEXT adds, as the 1Password op-ssh-sign.exe does.
        tree.Write(OperatingSystem.IsWindows() ? "bin/signer.cmd" : "bin/signer", "exit 0\n");
        DevScripts.Git(tree.Root, "config", "gpg.ssh.program", "signer");
        Dictionary<string, string?> path = new(StringComparer.Ordinal) { ["PATH"] = tree.FullPath("bin") + Path.PathSeparator + DevScripts.PathHolding("git", "dotnet") };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("check-git-signing.cs", tree.Root, path);

        Assert.True(exit == 0, output);
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("on")]
    [InlineData("1")]
    public void The_signing_check_and_bootstrap_read_commit_gpgsign_as_git_does(string enabled)
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");
        DevScripts.Git(tree.Root, "config", "commit.gpgsign", enabled);
        DevScripts.Git(tree.Root, "config", "gpg.format", "ssh");
        DevScripts.Git(tree.Root, "config", "user.signingkey", SigningKeyLiteral);
        tree.Write(OperatingSystem.IsWindows() ? "bin/signer.cmd" : "bin/signer", "exit 0\n");
        DevScripts.Git(tree.Root, "config", "gpg.ssh.program", "signer");
        Dictionary<string, string?> path = new(StringComparer.Ordinal) { ["PATH"] = tree.FullPath("bin") + Path.PathSeparator + DevScripts.PathHolding("git", "dotnet") };

        (int hookExit, string hookOutput) = DevScripts.RunScriptWithEnvironment("check-git-signing.cs", tree.Root, path);
        (_, string verifyOutput) = DevScripts.RunScript("bootstrap.cs", tree.Root, "--verify");

        Assert.True(hookExit == 0, hookOutput);
        Assert.DoesNotContain("commit.gpgsign", verifyOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void The_signing_check_signs_with_a_literal_key_through_the_agent_and_reports_an_agent_without_it()
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");
        DevScripts.Git(tree.Root, "config", "commit.gpgsign", "true");
        DevScripts.Git(tree.Root, "config", "gpg.format", "ssh");
        DevScripts.Git(tree.Root, "config", "user.signingkey", "key::" + SigningKeyLiteral);
        Dictionary<string, string?> noAgent = new(StringComparer.Ordinal) { ["SSH_AUTH_SOCK"] = tree.FullPath("no-agent") };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("check-git-signing.cs", tree.Root, noAgent);

        Assert.Equal(1, exit);
        Assert.Contains("signing check failed", output, StringComparison.Ordinal);
    }
}
