using System.Collections.Concurrent;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;

namespace CohEngine.TestSupport;

/// <summary>The files git tracks under a folder, so untracked working files never reach a test.</summary>
public static class GitFiles
{
    private static readonly ConcurrentDictionary<(string Root, string Folder), IReadOnlyList<string>> Cache = new();

    /// <summary>Lists the tracked files under a folder, running <c>git</c> once per folder.</summary>
    /// <param name="repositoryRoot">The work tree's root, where <c>git</c> runs.</param>
    /// <param name="folder">The folder relative to the root, with forward slashes.</param>
    /// <returns>The full paths in ordinal order.</returns>
    /// <exception cref="InvalidOperationException">See <see cref="Query"/>.</exception>
    public static IReadOnlyList<string> Tracked(string repositoryRoot, string folder)
    {
        return Cache.GetOrAdd((repositoryRoot, folder), key => Query(key.Root, key.Folder, "git"));
    }

    /// <summary>Runs <c>git ls-files -z</c> for a folder, without the cache.</summary>
    /// <param name="repositoryRoot">The work tree's root, where <c>git</c> runs.</param>
    /// <param name="folder">The folder relative to the root, with forward slashes.</param>
    /// <param name="git">The git executable.</param>
    /// <returns>The full paths in ordinal order.</returns>
    /// <exception cref="InvalidOperationException">Git cannot start, or fails, for example outside a work tree.</exception>
    public static IReadOnlyList<string> Query(string repositoryRoot, string folder, string git)
    {
        ProcessStartInfo start = new(git)
        {
            WorkingDirectory = repositoryRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
        };
        foreach (string argument in new[] { "ls-files", "-z", "--", folder })
        {
            start.ArgumentList.Add(argument);
        }

        // A git hook that runs the tests sets these to its own repository; the working directory must decide.
        foreach (string variable in new[] { "GIT_DIR", "GIT_WORK_TREE", "GIT_INDEX_FILE" })
        {
            start.Environment.Remove(variable);
        }

        string command = $"{git} ls-files -z -- {folder}";
        using Process process = new() { StartInfo = start };
        try
        {
            process.Start();
        }
        catch (Win32Exception exception)
        {
            throw new InvalidOperationException($"cannot run '{command}': {git} is not on PATH or cannot start; the sample-build tests need git", exception);
        }

        Task<string> error = process.StandardError.ReadToEndAsync();
        string output = process.StandardOutput.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"'{command}' in {repositoryRoot} exited {process.ExitCode}; the sample-build tests need a git work tree: {error.GetAwaiter().GetResult().Trim()}"));
        }

        return
        [
            .. output
                .Split('\0', StringSplitOptions.RemoveEmptyEntries)
                .Select(path => Path.Join(repositoryRoot, path.Replace('/', Path.DirectorySeparatorChar)))
                .Order(StringComparer.Ordinal),
        ];
    }
}
