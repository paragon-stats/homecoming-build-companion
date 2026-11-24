using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using CohEngine.Core.Data;

namespace CohEngine.Core.Tests.Data;

public sealed class CanonicalExtractCommandTests : IDisposable
{
    private const string Supplements = """
        [
            {"powerset": "Sonic_Aura", "source": {"commit": "fd4a2ee0"}, "directories": ["powers/pets/sonic_aura"], "file_count": 1},
            {"powerset": "Light_Affinity", "directories": ["powers/redirects/light_affinity/"], "file_count": 1}
        ]
        """;

    private readonly string scratch = Directory.CreateTempSubdirectory("coh-extract-").FullName;

    private string OutDir => Path.Join(scratch, "canonical");

    public void Dispose()
    {
        Directory.Delete(scratch, recursive: true);
    }

    [Fact]
    public void Colons_in_each_path_segment_become_underscores()
    {
        string zip = MakeZip("raw.zip", ("powers/pool:speed/super:speed.json", "{\"a\":1}"), ("archetypes/blaster.json", "{}"), ("tables/", null));

        (int code, string output, string error) = Run("--zip", zip, "--out-dir", OutDir);

        Assert.Equal((0, string.Empty), (code, error));
        Assert.Contains("extracted", output, StringComparison.Ordinal);
        Assert.Equal("{\"a\":1}", File.ReadAllText(Path.Join(OutDir, "powers", "pool_speed", "super_speed.json")));
        Assert.Equal("{}", File.ReadAllText(Path.Join(OutDir, "archetypes", "blaster.json")));
        Assert.False(Directory.Exists(Path.Join(OutDir, "tables")));
    }

    [Theory]
    [InlineData("powers/a:b.json", "powers/a_b.json", "powers/a_b.json")]
    [InlineData("Powers/A.json", "powers/a.json", "powers/a.json")]
    public void Two_entries_that_map_to_one_path_are_refused_with_E190(string first, string second, string mapped)
    {
        string zip = MakeZip("raw.zip", (first, "1"), (second, "2"));

        (int code, string output, string error) = Run("--zip", zip, "--out-dir", OutDir);

        Assert.Equal((1, string.Empty), (code, output));
        Assert.Equal($"error: E190: zip entries '{first}' and '{second}' both map to '{mapped}'; rename one in the extractor\n", error);
        Assert.False(File.Exists(Path.Join(OutDir, "manifest.json")));
    }

    [Fact]
    public void An_entry_outside_the_target_folder_is_refused_with_E191()
    {
        string zip = MakeZip("raw.zip", ("../canonical2/evil.json", "1"));

        (int code, _, string error) = Run("--zip", zip, "--out-dir", OutDir);

        Assert.Equal(1, code);
        Assert.Equal($"error: E191: zip entry '../canonical2/evil.json' resolves outside {OutDir}; refusing to extract\n", error);
        Assert.False(File.Exists(Path.Join(scratch, "canonical2", "evil.json")));
    }

    [Fact]
    public void The_manifest_records_the_zip_and_an_inventory_of_each_directory()
    {
        string zip = MakeZip("raw.zip", ("powers/x/a.json", "12345"), ("powers/b.json", "12"), ("archetypes/c.json", "1"));

        Assert.Equal(0, Run("--zip", zip, "--out-dir", OutDir + Path.DirectorySeparatorChar).Code);

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Join(OutDir, "manifest.json")));
        JsonElement root = manifest.RootElement;
        Assert.Equal(1, root.GetProperty("schema_version").GetInt32());
        Assert.True(DateTimeOffset.TryParse(root.GetProperty("generated_at").GetString(), System.Globalization.CultureInfo.InvariantCulture, out _));
        JsonElement source = root.GetProperty("source_zip");
        Assert.Equal(zip, source.GetProperty("path").GetString());
        Assert.Equal(new FileInfo(zip).Length, source.GetProperty("size").GetInt64());
        Assert.Equal(new FileInfo(zip).LastWriteTime, DateTime.Parse(source.GetProperty("modified").GetString()!, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.RoundtripKind));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))), source.GetProperty("sha256").GetString());
        JsonElement extraction = root.GetProperty("extraction");
        Assert.Equal(OutDir, extraction.GetProperty("out_dir").GetString());
        Assert.Equal(3, extraction.GetProperty("file_count").GetInt32());
        Assert.Equal(8, extraction.GetProperty("total_bytes").GetInt64());
        Assert.Equal(
            ["archetypes 1 1", "powers 2 7"],
            extraction.GetProperty("directories").EnumerateArray().Select(entry => string.Join(' ', entry.GetProperty("directory").GetString(), entry.GetProperty("file_count").GetRawText(), entry.GetProperty("total_bytes").GetRawText())),
            StringComparer.Ordinal);
        Assert.Equal(3, root.GetProperty("notes").GetArrayLength());
        Assert.Contains("CoD's snake_case JSON", File.ReadAllText(Path.Join(OutDir, "manifest.json")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_zip_whose_hash_the_manifest_records_is_not_extracted_again()
    {
        string zip = MakeZip("raw.zip", ("powers/a.json", "1"));
        Assert.Equal(0, Run("--zip", zip, "--out-dir", OutDir).Code);
        File.Delete(Path.Join(OutDir, "powers", "a.json"));

        (int code, string output, string error) = Run("--zip", zip, "--out-dir", OutDir);

        Assert.Equal((0, string.Empty), (code, error));
        Assert.Contains("pass --force to re-extract", output, StringComparison.Ordinal);
        Assert.False(File.Exists(Path.Join(OutDir, "powers", "a.json")));
    }

    [Fact]
    public void Force_clears_the_directories_and_extracts_again()
    {
        string zip = MakeZip("raw.zip", ("powers/a.json", "1"));
        Assert.Equal(0, Run("--zip", zip, "--out-dir", OutDir).Code);
        Directory.CreateDirectory(Path.Join(OutDir, "stale"));
        File.WriteAllText(Path.Join(OutDir, "stale", "old.json"), "0");
        File.Delete(Path.Join(OutDir, "powers", "a.json"));

        (int code, string output, _) = Run("--zip", zip, "--out-dir", OutDir, "--force");

        Assert.Equal(0, code);
        Assert.Contains("extracted", output, StringComparison.Ordinal);
        Assert.True(File.Exists(Path.Join(OutDir, "powers", "a.json")));
        Assert.False(Directory.Exists(Path.Join(OutDir, "stale")));
    }

    [Fact]
    public void A_re_extract_carries_the_supplements_block_unchanged()
    {
        string zip = MakeZip("raw.zip", ("powers/a.json", "1"));
        Directory.CreateDirectory(OutDir);
        File.WriteAllText(Path.Join(OutDir, "manifest.json"), $$"""{"source_zip": {"sha256": "OLD"}, "supplements": {{Supplements}}}""");

        Assert.Equal(0, Run("--zip", zip, "--out-dir", OutDir).Code);

        using JsonDocument manifest = JsonDocument.Parse(File.ReadAllText(Path.Join(OutDir, "manifest.json")));
        using JsonDocument expected = JsonDocument.Parse(Supplements);
        Assert.True(JsonElement.DeepEquals(expected.RootElement, manifest.RootElement.GetProperty("supplements")));
        Assert.Equal(Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(zip))), manifest.RootElement.GetProperty("source_zip").GetProperty("sha256").GetString());
    }

    [Fact]
    public void Force_keeps_the_folders_the_supplements_list_and_removes_other_leftovers()
    {
        string zip = MakeZip("raw.zip", ("powers/a.json", "1"));
        Assert.Equal(0, Run("--zip", zip, "--out-dir", OutDir).Code);
        using (JsonDocument first = JsonDocument.Parse(File.ReadAllText(Path.Join(OutDir, "manifest.json"))))
        {
            File.WriteAllText(Path.Join(OutDir, "manifest.json"), first.RootElement.GetRawText()[..^1] + $$""", "supplements": {{Supplements}}}""");
        }

        string[] kept = [Path.Join(OutDir, "powers", "pets", "sonic_aura", "x.json"), Path.Join(OutDir, "powers", "redirects", "light_affinity", "y.json")];
        string[] removed = [Path.Join(OutDir, "powers", "stale.json"), Path.Join(OutDir, "powers", "pets", "other", "z.json"), Path.Join(OutDir, "stale", "w.json")];
        foreach (string file in kept.Concat(removed))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllText(file, "0");
        }

        Assert.Equal(0, Run("--zip", zip, "--out-dir", OutDir, "--force").Code);

        Assert.All(kept, file => Assert.True(File.Exists(file), file));
        Assert.All(removed, file => Assert.False(File.Exists(file), file));
        Assert.False(Directory.Exists(Path.Join(OutDir, "stale")));
        Assert.True(File.Exists(Path.Join(OutDir, "powers", "a.json")));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{\"source_zip\": 3}")]
    public void An_unreadable_manifest_does_not_block_extraction(string manifest)
    {
        string zip = MakeZip("raw.zip", ("powers/a.json", "1"));
        Directory.CreateDirectory(OutDir);
        File.WriteAllText(Path.Join(OutDir, "manifest.json"), manifest);

        Assert.Equal(0, Run("--zip", zip, "--out-dir", OutDir).Code);
        Assert.True(File.Exists(Path.Join(OutDir, "powers", "a.json")));
    }

    [Fact]
    public void Without_arguments_the_newest_download_goes_into_the_repository_data_folder()
    {
        string downloads = Directory.CreateDirectory(Path.Join(scratch, "Downloads")).FullName;
        string older = MakeZip(Path.Join("Downloads", "raw_data_homecoming-1.zip"), ("powers/old.json", "1"));
        File.SetLastWriteTimeUtc(older, new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc));
        MakeZip(Path.Join("Downloads", "raw_data_homecoming-2.zip"), ("powers/new.json", "2"));

        (int code, _, string error) = CanonicalRun([], downloads, scratch);

        Assert.Equal((0, string.Empty), (code, error));
        Assert.True(File.Exists(Path.Join(scratch, "data", "canonical", "powers", "new.json")));
        Assert.False(File.Exists(Path.Join(scratch, "data", "canonical", "powers", "old.json")));
    }

    [Fact]
    public void No_download_to_pick_is_E72()
    {
        string downloads = Directory.CreateDirectory(Path.Join(scratch, "Downloads")).FullName;

        (int code, _, string error) = CanonicalRun([], downloads, scratch);

        Assert.Equal(1, code);
        Assert.Equal($"error: E72: cannot read {Path.Join(downloads, "raw_data_homecoming-*.zip")}: No such file or directory; pass --zip <path>\n", error);
    }

    [Fact]
    public void A_missing_zip_is_E72()
    {
        string zip = Path.Join(scratch, "missing.zip");

        (int code, _, string error) = Run("--zip", zip, "--out-dir", OutDir);

        Assert.Equal(1, code);
        Assert.Equal($"error: E72: cannot read {zip}: No such file or directory; check that the path exists and is readable\n", error);
    }

    [Fact]
    public void A_file_that_is_not_a_zip_is_E72()
    {
        string zip = Path.Join(scratch, "raw.zip");
        File.WriteAllText(zip, "not a zip");

        (int code, _, string error) = Run("--zip", zip, "--out-dir", OutDir);

        Assert.Equal(1, code);
        Assert.StartsWith($"error: E72: cannot read {zip}: ", error, StringComparison.Ordinal);
        Assert.EndsWith("; check that it is a City of Data raw-data zip\n", error, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_target_is_E72()
    {
        string zip = MakeZip("raw.zip", ("powers/a.json", "1"));
        File.WriteAllText(OutDir, "a file where the folder goes");

        (int code, _, string error) = Run("--zip", zip, "--out-dir", OutDir);

        Assert.Equal(1, code);
        Assert.StartsWith("error: E72: cannot read an input file: ", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("--bogus", "unrecognized arguments: --bogus")]
    [InlineData("--zip", "argument --zip: expected one argument")]
    public void A_bad_command_line_prints_usage_and_exits_2(string token, string problem)
    {
        (int code, string output, string error) = Run(token);

        Assert.Equal((2, string.Empty), (code, output));
        Assert.Equal($"usage: coh-engine extract-canonical [--zip ZIP] [--out-dir DIR] [--force]\ncoh-engine extract-canonical: error: {problem}\n", error);
    }

    private static (int Code, string Output, string Error) CanonicalRun(string[] args, string? downloads, string? repoRoot)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int code = CanonicalExtractCommand.Run(args, output, error, downloads, repoRoot);
        return (code, output.ToString(), error.ToString());
    }

    private (int Code, string Output, string Error) Run(params string[] args)
    {
        return CanonicalRun(args, Path.Join(scratch, "no-downloads"), Path.Join(scratch, "no-repo"));
    }

    private string MakeZip(string name, params (string Entry, string? Content)[] entries)
    {
        string path = Path.Join(scratch, name);
        using (ZipArchive archive = ZipFile.Open(path, ZipArchiveMode.Create))
        {
            foreach ((string entry, string? content) in entries)
            {
                ZipArchiveEntry created = archive.CreateEntry(entry);
                if (content is not null)
                {
                    using Stream stream = created.Open();
                    stream.Write(Encoding.UTF8.GetBytes(content));
                }
            }
        }

        return path;
    }
}
