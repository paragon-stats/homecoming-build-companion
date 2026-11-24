using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Data;

/// <summary>Extracts the City of Data raw-data zip into the canonical data folder and writes its <c>manifest.json</c>.</summary>
public static class CanonicalZip
{
    private static readonly string[] Notes =
    [
        "Canonical Homecoming game data from City of Data v3 raw-data export.",
        "Format is CoD's snake_case JSON. See docs/canonical-data-shape.md for field reference.",
        "Powers reference each other and set bonuses by full path (e.g. set_bonus.set_bonus.luck_of_the_gambler).",
    ];

    /// <summary>
    /// Extracts <paramref name="zipPath"/> into <paramref name="outDir"/>, mapping <c>:</c> to <c>_</c> in each path segment,
    /// and writes <c>manifest.json</c>. Nothing is extracted when the manifest already records the zip's SHA256, unless forced;
    /// forcing first deletes every directory under the target.
    /// </summary>
    /// <param name="zipPath">The raw-data zip.</param>
    /// <param name="outDir">The target folder.</param>
    /// <param name="force">Extract even when the manifest records the zip's hash.</param>
    /// <returns>The zip's SHA256 as uppercase hex, and whether it was extracted.</returns>
    /// <exception cref="EngineException">E41 when two entries map to one path; E42 when an entry resolves outside the target.</exception>
    public static (string Sha256, bool Extracted) Extract(string zipPath, string outDir, bool force)
    {
        ArgumentNullException.ThrowIfNull(zipPath);
        ArgumentNullException.ThrowIfNull(outDir);
        string zip = Path.GetFullPath(zipPath);
        string destination = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outDir));
        string sha256;
        using (FileStream stream = File.OpenRead(zip))
        {
            sha256 = Convert.ToHexString(SHA256.HashData(stream));
        }

        string manifestPath = Path.Join(destination, "manifest.json");
        if (!force && string.Equals(RecordedSha256(manifestPath), sha256, StringComparison.OrdinalIgnoreCase))
        {
            return (sha256, false);
        }

        Directory.CreateDirectory(destination);
        if (force)
        {
            foreach (string directory in Directory.GetDirectories(destination))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        Expand(zip, destination);
        WriteManifest(zip, destination, sha256, manifestPath);
        return (sha256, true);
    }

    private static string? RecordedSha256(string manifestPath)
    {
        if (!File.Exists(manifestPath))
        {
            return null;
        }

        // An unreadable manifest records no hash, so the zip is extracted again.
        try
        {
            using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return manifest.RootElement.GetProperty("source_zip").GetProperty("sha256").GetString();
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or KeyNotFoundException)
        {
            return null;
        }
    }

    // NTFS reads 'name:stream' as an alternate data stream, so ':' in a segment becomes '_'; the same rule as
    // coh_engine.attack_profile.canonical_power_path. Two entries are one path when they differ only in case, as on NTFS.
    private static void Expand(string zip, string destination)
    {
        string root = destination + Path.DirectorySeparatorChar;
        Dictionary<string, string> seen = new(StringComparer.OrdinalIgnoreCase);
        using ZipArchive archive = ZipFile.OpenRead(zip);
        foreach (ZipArchiveEntry entry in archive.Entries.Where(entry => !entry.FullName.EndsWith('/')))
        {
            string relative = string.Join('/', entry.FullName.Split('/').Select(segment => segment.Replace(':', '_')));
            if (seen.TryGetValue(relative, out string? first))
            {
                throw new EngineException($"E41: zip entries '{first}' and '{entry.FullName}' both map to '{relative}'; rename one in the extractor");
            }

            seen[relative] = entry.FullName;
            string target = Path.GetFullPath(Path.Join(destination, relative));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                throw new EngineException($"E42: zip entry '{entry.FullName}' resolves outside {destination}; refusing to extract");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, overwrite: true);
        }
    }

    private static void WriteManifest(string zip, string destination, string sha256, string manifestPath)
    {
        FileInfo zipInfo = new(zip);
        List<(string Name, int FileCount, long TotalBytes)> inventory =
        [
            .. new DirectoryInfo(destination).GetDirectories()
                .OrderBy(directory => directory.Name, StringComparer.OrdinalIgnoreCase)
                .Select(directory =>
                {
                    FileInfo[] files = directory.GetFiles("*", SearchOption.AllDirectories);
                    return (directory.Name, files.Length, files.Sum(file => file.Length));
                }),
        ];

        using FileStream stream = File.Create(manifestPath);
        using (Utf8JsonWriter json = new(stream, new JsonWriterOptions { Indented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            json.WriteStartObject();
            json.WriteNumber("schema_version", 1);
            json.WriteString("generated_at", DateTimeOffset.Now.ToString("o", CultureInfo.InvariantCulture));
            json.WriteStartObject("source_zip");
            json.WriteString("path", zip);
            json.WriteNumber("size", zipInfo.Length);
            json.WriteString("modified", zipInfo.LastWriteTime.ToString("o", CultureInfo.InvariantCulture));
            json.WriteString("sha256", sha256);
            json.WriteEndObject();
            json.WriteStartObject("extraction");
            json.WriteString("out_dir", destination);
            json.WriteNumber("file_count", inventory.Sum(directory => directory.FileCount));
            json.WriteNumber("total_bytes", inventory.Sum(directory => directory.TotalBytes));
            json.WriteStartArray("directories");
            foreach ((string name, int fileCount, long totalBytes) in inventory)
            {
                json.WriteStartObject();
                json.WriteString("directory", name);
                json.WriteNumber("file_count", fileCount);
                json.WriteNumber("total_bytes", totalBytes);
                json.WriteEndObject();
            }

            json.WriteEndArray();
            json.WriteEndObject();
            json.WriteStartArray("notes");
            foreach (string note in Notes)
            {
                json.WriteStringValue(note);
            }

            json.WriteEndArray();
            json.WriteEndObject();
        }

        stream.WriteByte((byte)'\n');
    }
}
