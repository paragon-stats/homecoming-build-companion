using System.ComponentModel;
using System.Diagnostics;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.SourceMaterial;

/// <summary>
/// Where the forum source material lives: the <c>--source-root</c> flag, else <c>COH_SOURCE_ROOT</c>, else
/// <c>homecoming-source-material</c> beside the main checkout of the repository the command runs in (a harness worktree
/// resolves to the main checkout, not to itself).
/// </summary>
internal static class SourceRoot
{
    /// <summary>The environment variable that names the root.</summary>
    public const string EnvironmentVariable = "COH_SOURCE_ROOT";

    /// <summary>The sibling checkout's folder name.</summary>
    public const string Sibling = "homecoming-source-material";

    /// <summary>Resolves the root.</summary>
    /// <param name="flag">The <c>--source-root</c> value, or null.</param>
    /// <param name="variable">The <c>COH_SOURCE_ROOT</c> value, or null when it is unset.</param>
    /// <param name="repository">The directory the command runs in, for the default.</param>
    /// <returns>The root directory.</returns>
    /// <exception cref="EngineException">E210: the root is missing or is not a directory.</exception>
    public static string Resolve(string? flag, string? variable, string repository)
    {
        string root = flag ?? (string.IsNullOrEmpty(variable) ? DefaultRoot(repository) : variable);
        if (!Directory.Exists(root))
        {
            string found = File.Exists(root) ? "exists but is not a directory" : "was not found";
            throw new EngineException($"{ErrorCodes.E210}: expected a directory at {root}, which {found}; set --source-root or {EnvironmentVariable}");
        }

        return root;
    }

    /// <summary><c>homecoming-source-material</c> beside the main checkout of <paramref name="repository"/>.</summary>
    /// <param name="repository">A directory inside the repository.</param>
    /// <returns>The default root.</returns>
    public static string DefaultRoot(string repository)
    {
        return Path.Join(Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(MainCheckout(repository)))), Sibling);
    }

    /// <summary>The main checkout of the repository at <paramref name="repository"/>; the directory itself outside a git repository.</summary>
    /// <param name="repository">A directory inside the repository.</param>
    /// <param name="git">The git executable.</param>
    /// <returns>The main checkout.</returns>
    public static string MainCheckout(string repository, string git = "git")
    {
        string? common = Git(repository, git, ["rev-parse", "--path-format=absolute", "--git-common-dir"]);
        return common is null ? repository : Path.GetDirectoryName(common)!;
    }

    /// <summary>The <c>git config user.name</c> seen from <paramref name="repository"/>, or null when git has none or cannot run.</summary>
    /// <param name="repository">The directory git runs in.</param>
    /// <param name="git">The git executable.</param>
    /// <returns>The name, or null.</returns>
    public static string? GitUserName(string repository, string git = "git")
    {
        return Git(repository, git, ["config", "user.name"]) is { Length: > 0 } name ? name : null;
    }

    /// <summary>A git command's trimmed standard output, or null when it fails or git cannot run.</summary>
    private static string? Git(string repository, string git, string[] args)
    {
        ProcessStartInfo start = new(git, args)
        {
            WorkingDirectory = repository,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        try
        {
            using Process process = Process.Start(start)!;
            string text = process.StandardOutput.ReadToEnd().Trim();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? text : null;
        }
        catch (Win32Exception)
        {
            return null;
        }
    }
}
