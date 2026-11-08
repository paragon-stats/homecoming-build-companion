namespace CohEngine.TestSupport;

/// <summary>A scratch directory the test writes input files into and deletes afterwards.</summary>
public sealed class TempTree : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TempTree"/> class.</summary>
    public TempTree()
    {
        Root = Directory.CreateTempSubdirectory("coh-engine-").FullName;
    }

    /// <summary>Gets the directory.</summary>
    public string Root { get; }

    /// <summary>Writes a file under the directory, creating folders as needed.</summary>
    /// <param name="relative">The path below the root, with forward slashes.</param>
    /// <param name="content">The text.</param>
    /// <returns>The file's full path.</returns>
    public string Write(string relative, string content)
    {
        string path = Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    /// <summary>The full path of a name below the directory.</summary>
    /// <param name="relative">The path below the root, with forward slashes.</param>
    /// <returns>The full path.</returns>
    public string FullPath(string relative)
    {
        return Path.Combine(Root, relative.Replace('/', Path.DirectorySeparatorChar));
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        Directory.Delete(Root, recursive: true);
    }
}
