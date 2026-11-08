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

    /// <summary>Copies the files of a directory (not its subdirectories) below the root.</summary>
    /// <param name="source">The directory to copy.</param>
    /// <param name="relative">The target below the root, with forward slashes.</param>
    /// <returns>The target's full path.</returns>
    public string CopyFiles(string source, string relative)
    {
        string target = FullPath(relative);
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return target;
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
