using CohEngine.Core.Registry;

namespace CohEngine.Core.Tests.Registry;

public sealed class DataVersionTests : IDisposable
{
    private readonly string scratch = Directory.CreateTempSubdirectory("coh-data-version").FullName;

    public void Dispose()
    {
        Directory.Delete(scratch, recursive: true);
    }

    [Fact]
    public void No_path_read_is_unknown()
    {
        Assert.Equal(DataVersion.Unknown, DataVersion.Of());
    }

    [Fact]
    public void The_same_files_in_two_directories_give_the_same_value()
    {
        string first = Make("a", ("x.json", "1"), ("y.json", "2"));
        string second = Make("b", ("y.json", "2"), ("x.json", "1"));

        Assert.Equal(DataVersion.Of(first), DataVersion.Of(second));
        Assert.Matches("^sha256:[0-9a-f]{64}$", DataVersion.Of(first));
    }

    [Fact]
    public void One_changed_byte_a_renamed_file_or_a_missing_file_changes_the_value()
    {
        string baseline = DataVersion.Of(Make("a", ("x.json", "1"), ("y.json", "2")));

        Assert.NotEqual(baseline, DataVersion.Of(Make("b", ("x.json", "1"), ("y.json", "3"))), StringComparer.Ordinal);
        Assert.NotEqual(baseline, DataVersion.Of(Make("c", ("x.json", "1"), ("z.json", "2"))), StringComparer.Ordinal);
        Assert.NotEqual(baseline, DataVersion.Of(Make("d", ("x.json", "1"))), StringComparer.Ordinal);
    }

    [Fact]
    public void Where_a_name_ends_and_the_bytes_begin_is_part_of_the_value()
    {
        Assert.NotEqual(DataVersion.Of(Make("a", ("ab", "c"))), DataVersion.Of(Make("b", ("a", "bc"))), StringComparer.Ordinal);
    }

    [Fact]
    public void A_subdirectory_is_not_read()
    {
        string plain = Make("a", ("x.json", "1"));
        Directory.CreateDirectory(Path.Join(plain, "nested"));
        File.WriteAllText(Path.Join(plain, "nested", "ignored.json"), "changes nothing");
        string other = Make("b", ("x.json", "1"));

        Assert.Equal(DataVersion.Of(other), DataVersion.Of(plain));
    }

    [Fact]
    public void The_order_the_paths_are_given_in_is_part_of_the_value()
    {
        string first = Make("a", ("x.json", "1"));
        string second = Make("b", ("y.json", "2"));

        Assert.NotEqual(DataVersion.Of(first, second), DataVersion.Of(second, first), StringComparer.Ordinal);
    }

    [Fact]
    public void A_file_changed_in_place_changes_the_value_on_the_next_call()
    {
        string directory = Make("a", ("x.json", "1"));
        string before = DataVersion.Of(directory);

        File.WriteAllText(Path.Join(directory, "x.json"), "2");
        File.SetLastWriteTimeUtc(Path.Join(directory, "x.json"), DateTime.UtcNow.AddMinutes(5));

        Assert.NotEqual(before, DataVersion.Of(directory), StringComparer.Ordinal);
    }

    [Fact]
    public void A_file_whose_length_changed_is_read_again_even_with_the_same_timestamp()
    {
        string directory = Make("a", ("x.json", "1"));
        string file = Path.Join(directory, "x.json");
        DateTime stamp = File.GetLastWriteTimeUtc(file);
        string before = DataVersion.Of(directory);

        File.WriteAllText(file, "22");
        File.SetLastWriteTimeUtc(file, stamp);

        Assert.NotEqual(before, DataVersion.Of(directory), StringComparer.Ordinal);
    }

    [Fact]
    public void An_unchanged_directory_gives_the_same_value_without_reading_its_files_again()
    {
        string directory = Make("a", ("x.json", "1"), ("y.json", "2"));
        string[] files = [Path.GetFullPath(Path.Join(directory, "x.json")), Path.GetFullPath(Path.Join(directory, "y.json"))];
        string first = DataVersion.Of(directory);
        int[] reads = [.. files.Select(file => DataVersion.Reads[file])];

        string second = DataVersion.Of(directory);

        Assert.Equal(first, second, StringComparer.Ordinal);
        Assert.Equal([1, 1], reads);
        Assert.Equal(reads, files.Select(file => DataVersion.Reads[file]));
    }

    private string Make(string name, params (string File, string Text)[] files)
    {
        string directory = Path.Join(scratch, name);
        Directory.CreateDirectory(directory);
        foreach ((string file, string text) in files)
        {
            File.WriteAllText(Path.Join(directory, file), text);
        }

        return directory;
    }
}
