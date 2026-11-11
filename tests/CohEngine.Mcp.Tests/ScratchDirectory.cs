namespace CohEngine.Mcp.Tests;

/// <summary>A temporary directory that copies the files of fixture directories and changes one byte on request.</summary>
internal sealed class ScratchDirectory : IDisposable
{
    private readonly string root = Directory.CreateTempSubdirectory("coh-mcp-scratch").FullName;

    /// <summary>Changes the first line break to a space, one byte that leaves the JSON reading the same, and moves the timestamp on.</summary>
    /// <param name="file">The file.</param>
    public static void ChangeOneByte(string file)
    {
        byte[] bytes = File.ReadAllBytes(file);
        bytes[Array.IndexOf(bytes, (byte)'\n')] = (byte)' ';
        File.WriteAllBytes(file, bytes);
        File.SetLastWriteTimeUtc(file, File.GetLastWriteTimeUtc(file).AddSeconds(5));
    }

    public string CopyOf(string source, string relative)
    {
        string target = Path.Join(root, relative);
        Directory.CreateDirectory(target);
        foreach (string file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Join(target, Path.GetFileName(file)));
        }

        return target;
    }

    public void Dispose()
    {
        Directory.Delete(root, recursive: true);
    }
}
