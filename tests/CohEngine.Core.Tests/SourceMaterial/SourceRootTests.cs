using System.Diagnostics;

using CohEngine.Core.Diagnostics;
using CohEngine.Core.SourceMaterial;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.SourceMaterial;

public sealed class SourceRootTests
{
    [Fact]
    public void The_flag_beats_the_variable_and_the_variable_beats_the_default()
    {
        using TempTree tree = new();
        string flag = Directory.CreateDirectory(tree.FullPath("flag")).FullName;
        string variable = Directory.CreateDirectory(tree.FullPath("variable")).FullName;
        string repository = Directory.CreateDirectory(tree.FullPath("outside/repository")).FullName;
        string fallback = Directory.CreateDirectory(tree.FullPath("outside/" + SourceRoot.Sibling)).FullName;

        Assert.Equal(flag, SourceRoot.Resolve(flag, variable, repository));
        Assert.Equal(variable, SourceRoot.Resolve(flag: null, variable, repository));
        Assert.Equal(fallback, SourceRoot.Resolve(flag: null, variable: null, repository));
        Assert.Equal(fallback, SourceRoot.Resolve(flag: null, string.Empty, repository));
    }

    [Fact]
    public void A_missing_root_and_a_file_are_named_errors()
    {
        using TempTree tree = new();
        string absent = tree.FullPath("absent");
        string file = tree.Write("file", "x");

        Assert.Equal($"E210: expected a directory at {absent}, which was not found; set --source-root or COH_SOURCE_ROOT", Assert.Throws<EngineException>(() => SourceRoot.Resolve(absent, variable: null, tree.Root)).Message);
        Assert.Contains("which exists but is not a directory", Assert.Throws<EngineException>(() => SourceRoot.Resolve(file, variable: null, tree.Root)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_main_checkout_of_a_worktree_is_the_main_repository()
    {
        using TempTree tree = new();
        string main = tree.FullPath("main");
        using (Process git = Process.Start("git", ["init", "-q", main]))
        {
            git.WaitForExit();
        }

        string worktree = tree.FullPath("main/.claude/worktrees/agent");
        string admin = tree.FullPath("main/.git/worktrees/agent");
        Directory.CreateDirectory(worktree);
        tree.Write("main/.git/worktrees/agent/HEAD", "ref: refs/heads/agent\n");
        tree.Write("main/.git/worktrees/agent/commondir", "../..\n");
        tree.Write("main/.git/worktrees/agent/gitdir", Path.Join(worktree, ".git") + "\n");
        tree.Write("main/.claude/worktrees/agent/.git", $"gitdir: {admin}\n");

        Assert.Equal(Path.GetFullPath(main), Path.GetFullPath(SourceRoot.MainCheckout(main)));
        Assert.Equal(Path.GetFullPath(main), Path.GetFullPath(SourceRoot.MainCheckout(worktree)));
        Assert.Equal(Path.Join(tree.Root, SourceRoot.Sibling), SourceRoot.DefaultRoot(worktree));
    }

    [Fact]
    public void The_git_user_name_is_the_one_git_reports_and_none_without_git()
    {
        using TempTree tree = new();
        using Process git = Process.Start(new ProcessStartInfo("git", ["config", "user.name"]) { WorkingDirectory = tree.Root, RedirectStandardOutput = true, UseShellExecute = false })!;
        string expected = git.StandardOutput.ReadToEnd().Trim();
        git.WaitForExit();

        Assert.Equal(expected.Length > 0 ? expected : null, SourceRoot.GitUserName(tree.Root));
        Assert.Null(SourceRoot.GitUserName(tree.Root, git: tree.FullPath("no-such-git")));
    }

    [Fact]
    public void Outside_a_repository_or_without_git_the_directory_is_its_own_main_checkout()
    {
        using TempTree tree = new();
        string absent = tree.FullPath("absent");

        Assert.Equal(tree.Root, SourceRoot.MainCheckout(tree.Root));
        Assert.Equal(absent, SourceRoot.MainCheckout(absent));
        Assert.Equal(tree.Root, SourceRoot.MainCheckout(tree.Root, git: tree.FullPath("no-such-git")));
    }
}
