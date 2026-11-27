using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

public sealed partial class DevScriptTests
{
    // Built in two parts, so this file holds no banned word of its own.
    private static readonly string BannedWord = "orac" + "le";

    [Theory]
    [InlineData("src/My Dir/Clean File.cs")]
    [InlineData("src/Café.cs")]
    public void The_coverage_check_reads_a_staged_file_whose_name_has_a_space_or_a_non_ascii_letter(string name)
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write(name, "namespace Sample;\n\npublic sealed class Clean;\n");
        DevScripts.Git(tree.Root, "add", "--", name);

        (int cleanExit, string cleanOutput) = DevScripts.RunScript("check-coverage-exclusions.cs", tree.Root, "--staged");
        tree.Write(name, "namespace Sample;\n\n[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]\npublic sealed class Hidden;\n");
        DevScripts.Git(tree.Root, "add", "--", name);
        tree.Write(name, "namespace Sample;\n\npublic sealed class Clean;\n");
        (int stagedExit, string stagedOutput) = DevScripts.RunScript("check-coverage-exclusions.cs", tree.Root, "--staged");

        Assert.True(cleanExit == 0, cleanOutput);
        Assert.Equal(1, stagedExit);
        Assert.Contains($"{name}:3: [ExcludeFromCodeCoverage]", stagedOutput, StringComparison.Ordinal);
    }

    [Fact]
    public void The_banned_words_check_judges_the_staged_content_in_hook_mode()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("my notes.txt", $"the {BannedWord}\n");
        DevScripts.Git(tree.Root, "add", "--", "my notes.txt");
        tree.Write("my notes.txt", "clean\n");
        tree.Write("untracked.txt", $"the {BannedWord}\n");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal) { ["COH_BANNED_NAMES"] = "Somebody Else", ["GITHUB_ACTIONS"] = null };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("check-banned-words.cs", tree.Root, environment, "--staged");

        Assert.Equal(1, exit);
        Assert.Contains("my notes.txt:1: banned word", output, StringComparison.Ordinal);
        Assert.DoesNotContain("untracked.txt", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_banned_words_check_fails_when_git_cannot_read_a_staged_file()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);

        // An index entry naming an object the repository does not have: cat-file answers "missing".
        DevScripts.Git(tree.Root, "update-index", "--add", "--cacheinfo", $"100644,{new string('1', 40)},ghost.txt");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal) { ["COH_BANNED_NAMES"] = "Somebody Else", ["GITHUB_ACTIONS"] = null };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("check-banned-words.cs", tree.Root, environment, "--staged");

        Assert.Equal(1, exit);
        Assert.Contains("ghost.txt:1: git cannot read the staged content of this file", output, StringComparison.Ordinal);
        Assert.DoesNotContain("[OK]", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_encoding_check_finds_a_hit_when_run_from_a_subfolder(bool staged)
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("scripts/dev/hit.cs", "// café\n");
        DevScripts.Git(tree.Root, "add", "--", "scripts/dev/hit.cs");
        Directory.CreateDirectory(tree.FullPath("docs"));

        // A user's diff.relative=true makes git diff list only the current folder unless told otherwise.
        DevScripts.Git(tree.Root, "config", "diff.relative", "true");

        (int exit, string output) = DevScripts.RunScript("check-encoding.cs", tree.FullPath("docs"), staged ? ["--staged"] : []);

        Assert.Equal(1, exit);
        Assert.Contains("scripts/dev/hit.cs:1:7: non-ASCII U+00E9", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_banned_words_check_finds_a_staged_hit_when_run_from_a_subfolder()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("notes.txt", $"the {BannedWord}\n");
        DevScripts.Git(tree.Root, "add", "--", "notes.txt");
        Directory.CreateDirectory(tree.FullPath("docs"));
        DevScripts.Git(tree.Root, "config", "diff.relative", "true");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal) { ["COH_BANNED_NAMES"] = "Somebody Else", ["GITHUB_ACTIONS"] = null };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("check-banned-words.cs", tree.FullPath("docs"), environment, "--staged");

        Assert.Equal(1, exit);
        Assert.Contains("notes.txt:1: banned word", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_banned_words_check_fails_on_its_seed_file()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        string seed = Path.Join(DevScripts.RepositoryRoot, "tests", "fixtures", "banned-words-seed.txt");
        Dictionary<string, string?> environment = new(StringComparer.Ordinal) { ["COH_BANNED_NAMES"] = "Somebody Else", ["GITHUB_ACTIONS"] = null };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("check-banned-words.cs", tree.Root, environment, "--seed", seed);

        Assert.Equal(1, exit);
        Assert.Contains("banned-words-seed.txt:2: banned word", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_encoding_check_judges_the_staged_content_in_hook_mode_and_skips_untracked_and_build_output()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("scripts/dev/staged.cs", "// café\n");
        DevScripts.Git(tree.Root, "add", "--", "scripts/dev/staged.cs");
        tree.Write("scripts/dev/staged.cs", "// cafe\n");
        tree.Write("scripts/dev/untracked.cs", "// café\n");
        tree.Write("src/CohEngine.Cli/obj/Debug/Generated.cs", "// café\n");

        (int exit, string output) = DevScripts.RunScript("check-encoding.cs", tree.Root, "--staged");

        Assert.Equal(1, exit);
        Assert.Contains("scripts/dev/staged.cs:1:7: non-ASCII U+00E9", output, StringComparison.Ordinal);
        Assert.DoesNotContain("untracked.cs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Generated.cs", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_encoding_check_reports_a_tracked_file_and_leaves_untracked_and_build_output_alone()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        tree.Write("src/CohEngine.Cli/Output.cs", "// café\n");
        DevScripts.Git(tree.Root, "add", "--", "src/CohEngine.Cli/Output.cs");
        tree.Write("scripts/dev/untracked.cs", "// café\n");
        tree.Write("src/CohEngine.Cli/bin/Debug/Generated.cs", "// café\n");

        (int exit, string output) = DevScripts.RunScript("check-encoding.cs", tree.Root);

        Assert.Equal(1, exit);
        Assert.Contains("src/CohEngine.Cli/Output.cs:1:7: non-ASCII U+00E9", output, StringComparison.Ordinal);
        Assert.DoesNotContain("untracked.cs", output, StringComparison.Ordinal);
        Assert.DoesNotContain("Generated.cs", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_text_check_judges_a_symlink_that_became_a_regular_file()
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);

        // The symlink lives only in the index and the scratch commit, so no OS symlink support is needed.
        tree.Write("target", "target");
        string blob = DevScripts.Git(tree.Root, "hash-object", "-w", "target").Trim();
        File.Delete(tree.FullPath("target"));
        DevScripts.Git(tree.Root, "update-index", "--add", "--cacheinfo", $"120000,{blob},link.txt");
        DevScripts.Git(tree.Root, "-c", "user.name=scratch", "-c", "user.email=scratch@example.invalid", "commit", "--quiet", "-m", "scratch");

        // Without core.symlinks, git add keeps an index entry's symlink mode; dropping the entry first makes it a regular file.
        DevScripts.Git(tree.Root, "rm", "--cached", "--quiet", "--", "link.txt");
        tree.Write("link.txt", "trailing  \n");
        DevScripts.Git(tree.Root, "add", "--", "link.txt");
        Assert.Contains("T\0link.txt", DevScripts.Git(tree.Root, "diff", "--cached", "--name-status", "-z"), StringComparison.Ordinal);

        (int exit, string output) = DevScripts.RunScript("check-text-files.cs", tree.Root);

        Assert.Equal(1, exit);
        Assert.Contains("link.txt:1", output, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void The_push_hook_skips_its_checks_only_when_every_ref_is_a_deletion(bool deletionsOnly)
    {
        using TempTree tree = new();
        DevScripts.SeedRepository(tree);
        File.Copy(Path.Join(DevScripts.RepositoryRoot, ".husky", "pre-push"), tree.FullPath(".husky/pre-push"));
        tree.Write(".husky/task-runner.json", "{\"tasks\":[{\"name\":\"fails\",\"group\":\"pre-push\",\"command\":\"git\",\"args\":[\"no-such-command\"]}]}");
        string zeros = new('0', 40);
        string some = new('1', 40);
        string refs = $"(delete) {zeros} refs/heads/gone {some}\n" + (deletionsOnly ? $"(delete) {zeros} refs/heads/also-gone {some}\n" : $"refs/heads/work {some} refs/heads/work {zeros}\n");

        (int exit, string output, _, _) = DevScripts.RunSeparately(tree.Root, DevScripts.Bash(), ShellEnvironment(), refs, ".husky/pre-push");

        if (deletionsOnly)
        {
            Assert.True(exit == 0, output);
            Assert.Equal(string.Empty, output.Trim());
        }
        else
        {
            Assert.True(exit != 0, output);
            Assert.Contains("no-such-command", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void The_environment_check_reports_what_the_signing_check_fails_on_and_a_missing_node()
    {
        using TempTree tree = new();
        DevScripts.Git(tree.Root, "init", "--quiet");
        DevScripts.Git(tree.Root, "config", "commit.gpgsign", "true");
        Dictionary<string, string?> noNode = new(StringComparer.Ordinal) { ["PATH"] = DevScripts.PathHolding("git", "dotnet") };

        (int exit, string output) = DevScripts.RunScriptWithEnvironment("bootstrap.cs", tree.Root, noNode, "--verify");

        Assert.Equal(1, exit);
        Assert.Contains("gpg.format is '' (expected 'ssh')", output, StringComparison.Ordinal);
        Assert.Contains("  - Node.js not found", output, StringComparison.Ordinal);
    }
}
