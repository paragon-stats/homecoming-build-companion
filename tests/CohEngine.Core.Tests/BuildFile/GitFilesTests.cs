using System.Diagnostics;

using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.BuildFile;

public sealed class GitFilesTests
{
    private const string Folder = "samples/builds";

    private static void Git(string directory, params string[] arguments)
    {
        ProcessStartInfo start = new("git") { WorkingDirectory = directory, UseShellExecute = false };
        foreach (string argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        // Under a git hook these name the real repository, which the scratch commands must not touch.
        foreach (string variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE" })
        {
            start.Environment.Remove(variable);
        }

        using Process process = Process.Start(start)!;
        process.WaitForExit();
        Assert.Equal(0, process.ExitCode);
    }

    // git add writes read-only object files, which the scratch directory's recursive delete cannot remove on Windows.
    private static void Track(string directory, params string[] paths)
    {
        Git(directory, ["update-index", "--add", "--info-only", "--", .. paths]);
    }

    [Fact]
    public void Only_the_tracked_files_are_listed_by_full_path_in_ordinal_order()
    {
        using TempTree tree = new();
        Git(tree.Root, "init", "--quiet");
        string spaced = tree.Write($"{Folder}/sub folder/b 'q'.mbd", "tracked");
        string plain = tree.Write($"{Folder}/a.mbd", "tracked");
        tree.Write($"{Folder}/untracked.mbd", "working copy");
        Track(tree.Root, $"{Folder}/a.mbd", $"{Folder}/sub folder/b 'q'.mbd");

        Assert.Equal([plain, spaced], GitFiles.Query(tree.Root, Folder, "git"));
    }

    [Fact]
    public void The_listing_is_made_once_per_folder()
    {
        using TempTree tree = new();
        Git(tree.Root, "init", "--quiet");
        tree.Write($"{Folder}/a.mbd", "tracked");
        Track(tree.Root, $"{Folder}/a.mbd");

        IReadOnlyList<string> first = GitFiles.Tracked(tree.Root, Folder);

        Assert.Single(first);
        Assert.Same(first, GitFiles.Tracked(tree.Root, Folder));
    }

    [Fact]
    public void A_folder_outside_a_work_tree_throws()
    {
        using TempTree tree = new();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => GitFiles.Query(tree.Root, Folder, "git"));

        Assert.Contains("the sample-build tests need a git work tree", thrown.Message, StringComparison.Ordinal);
        Assert.Contains("not a git repository", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_git_throws_and_says_so()
    {
        using TempTree tree = new();

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => GitFiles.Query(tree.Root, Folder, "no-such-git"));

        Assert.Contains("no-such-git is not on PATH", thrown.Message, StringComparison.Ordinal);
    }
}
