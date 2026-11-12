using System.Globalization;

using CohEngine.Core.Damage;

namespace CohEngine.Core.Chain;

/// <summary>Path helpers that print as Python's <c>str(Path(...))</c> does.</summary>
public static class ChainPaths
{
    /// <summary>Normalizes separators to the platform's, dropping empty and <c>.</c> segments and trailing separators.</summary>
    /// <param name="path">The path as typed.</param>
    /// <returns>The path as Python prints it.</returns>
    public static string Norm(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        string[] segments = path.Split('/', '\\');
        bool rooted = segments.Length > 1 && segments[0].Length == 0;
        string joined = string.Join(Path.DirectorySeparatorChar, segments.Where(segment => segment.Length > 0 && !string.Equals(segment, ".", StringComparison.Ordinal)));
        return rooted ? Path.DirectorySeparatorChar + joined : joined.Length == 0 ? "." : joined;
    }

    /// <summary>Finds the repository root, the directory holding <c>strategy/breakpoints.json</c>, from the binary and then the working directory.</summary>
    /// <returns>The root.</returns>
    /// <exception cref="FileNotFoundException">No such directory above either start.</exception>
    public static string RepoRoot()
    {
        return RepoRoot([AppContext.BaseDirectory, Directory.GetCurrentDirectory()]);
    }

    /// <summary>Finds the repository root above any of the given directories.</summary>
    /// <param name="starts">The directories to climb from, in order.</param>
    /// <returns>The root.</returns>
    /// <exception cref="FileNotFoundException">No directory above any start holds <c>strategy/breakpoints.json</c>.</exception>
    internal static string RepoRoot(IEnumerable<string> starts)
    {
        foreach (string start in starts)
        {
            for (DirectoryInfo? directory = new(start); directory is not null; directory = directory.Parent)
            {
                if (File.Exists(Path.Join(directory.FullName, "strategy", "breakpoints.json")))
                {
                    return directory.FullName;
                }
            }
        }

        throw new FileNotFoundException("strategy/breakpoints.json not found above the binary or the working directory", "strategy/breakpoints.json");
    }
}
