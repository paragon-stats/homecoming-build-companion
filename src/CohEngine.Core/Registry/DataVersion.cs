using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace CohEngine.Core.Registry;

/// <summary>
/// Identifies the data an answer was computed from: a SHA-256 over the files it read. A directory counts as its own
/// files, in ordinal name order, each as its name and its bytes; a path hashes once per process.
/// </summary>
public static class DataVersion
{
    /// <summary>The value when an answer read no data files, so it is never mistaken for a hash.</summary>
    public const string Unknown = "unknown";

    private static readonly ConcurrentDictionary<string, string> Cache = new(StringComparer.Ordinal);

    /// <summary>Hashes the directories and files an answer was computed from, in the order given.</summary>
    /// <param name="paths">Existing directories (their own files, not subdirectories) and files.</param>
    /// <returns><c>sha256:</c> and 64 lowercase hex digits, or <see cref="Unknown"/> when no path is given.</returns>
    public static string Of(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        return paths.Length == 0 ? Unknown : "sha256:" + Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join('\n', paths.Select(HashOf)))));
    }

    private static string HashOf(string path)
    {
        return Cache.GetOrAdd(Path.GetFullPath(path), Compute);
    }

    private static string Compute(string full)
    {
        string[] files = Directory.Exists(full) ? [.. Directory.GetFiles(full).Order(StringComparer.Ordinal)] : [full];
        using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        foreach (string file in files)
        {
            byte[] name = Encoding.UTF8.GetBytes(Path.GetFileName(file));
            byte[] bytes = File.ReadAllBytes(file);
            hash.AppendData(BitConverter.GetBytes(name.Length));
            hash.AppendData(name);
            hash.AppendData(BitConverter.GetBytes((long)bytes.Length));
            hash.AppendData(bytes);
        }

        return Convert.ToHexStringLower(hash.GetHashAndReset());
    }
}
