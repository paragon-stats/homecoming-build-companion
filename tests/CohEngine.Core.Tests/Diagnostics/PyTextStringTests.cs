using System.Text;

using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Diagnostics;

// Expected values are what CPython 3.14's str methods return for the same text.
public sealed class PyTextStringTests
{
    [Fact]
    public void Strip_and_fold_use_python_white_space_including_the_file_separators()
    {
        string text = "\u001c\u00a0 a \u2003b\t\u001f";

        Assert.Equal("a \u2003b", PyText.Strip(text));
        Assert.Equal("a b", PyText.Fold(text));
        Assert.Equal(string.Empty, PyText.Fold(" \u3000 "));
        Assert.Equal(string.Empty, PyText.Strip("\u001d"));
    }

    [Fact]
    public void Split_lines_breaks_at_every_python_line_boundary()
    {
        Assert.Equal(["a", "b", "c", "d", "e", string.Empty, "f"], PyText.SplitLines("a\r\nb\rc\u2028d\u001ee\n\nf\n"));
        Assert.Equal(["x\u001fy"], PyText.SplitLines("x\u001fy"));
        Assert.Empty(PyText.SplitLines(string.Empty));
    }

    [Fact]
    public void Read_text_reads_every_line_ending_as_a_newline_and_keeps_a_byte_order_mark()
    {
        using TempTree tree = new();
        string path = tree.FullPath("a.txt");
        File.WriteAllBytes(path, [0xef, 0xbb, 0xbf, (byte)'a', (byte)'\r', (byte)'\n', (byte)'b', (byte)'\r', (byte)'c']);

        Assert.Equal("\ufeffa\nb\nc", PyText.ReadText(path));
        File.WriteAllBytes(path, [0xff]);
        Assert.Throws<DecoderFallbackException>(() => PyText.ReadText(path));
    }

    [Fact]
    public void Write_text_writes_utf8_without_a_byte_order_mark_and_keeps_the_line_endings()
    {
        using TempTree tree = new();
        string path = tree.FullPath("a.txt");

        PyText.WriteText(path, "\u00e9\n");

        Assert.Equal("é\n"u8.ToArray(), File.ReadAllBytes(path));
    }

    [Fact]
    public void A_write_that_fails_leaves_the_old_file_whole_and_no_temporary_file()
    {
        using TempTree tree = new();
        string path = tree.Write("a.txt", "old\n");

        Assert.Throws<EncoderFallbackException>(() => PyText.WriteText(path, "\ud800"));

        Assert.Equal("old\n", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFiles(tree.Root));
    }

    [Fact]
    public void A_file_another_process_holds_is_tried_three_times_then_named_and_left_whole()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to replace a file another handle holds open.");
        using TempTree tree = new();
        string path = tree.Write("a.txt", "old\n");
        List<TimeSpan> waits = [];

        IOException refused;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            refused = Assert.Throws<IOException>(() => PyText.WriteText(path, "new\n", waits.Add));
        }

        Assert.Equal($"{path} is in use by another process (or access to it is denied); it was tried 3 times", refused.Message);
        Assert.Equal(2, waits.Count);
        Assert.Equal("old\n", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFiles(tree.Root));
    }

    [Fact]
    public void A_temporary_file_that_cannot_be_cleaned_up_does_not_hide_the_error_about_the_target()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to replace a file another handle holds open.");
        using TempTree tree = new();
        string path = tree.Write("a.txt", "old\n");
        FileStream? temporaryHolder = null;

        IOException refused;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            // After the first failed move, hold the temporary file too, so its cleanup fails as well.
            refused = Assert.Throws<IOException>(() => PyText.WriteText(path, "new\n", _ => temporaryHolder ??= new FileStream(Directory.GetFiles(tree.Root, ".*.tmp").Single(), FileMode.Open, FileAccess.Read, FileShare.None)));
            temporaryHolder?.Dispose();
        }

        Assert.StartsWith($"{path} is in use by another process", refused.Message, StringComparison.Ordinal);
        Assert.Equal("old\n", File.ReadAllText(path));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void A_file_released_before_the_last_attempt_is_written(int heldThroughWaits)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to replace a file another handle holds open.");
        using TempTree tree = new();
        string path = tree.Write("a.txt", "old\n");
        using FileStream holder = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        List<TimeSpan> waits = [];

        PyText.WriteText(path, "new\n", wait =>
        {
            waits.Add(wait);
            if (waits.Count == heldThroughWaits)
            {
                holder.Dispose();
            }
        });

        Assert.Equal(heldThroughWaits, waits.Count);
        Assert.Equal("new\n", File.ReadAllText(path));
    }

    [Fact]
    public void An_append_adds_the_text_after_the_old_bytes_and_creates_a_missing_file()
    {
        using TempTree tree = new();
        string path = tree.FullPath("index.jsonl");

        PyText.AppendText(path, "a\n");
        PyText.AppendText(path, "é\n");

        Assert.Equal("a\né\n"u8.ToArray(), File.ReadAllBytes(path));
        Assert.Equal([path], Directory.GetFiles(tree.Root));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void An_append_to_a_file_released_before_the_last_attempt_is_written(int heldThroughWaits)
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to replace a file another handle holds open.");
        using TempTree tree = new();
        string path = tree.Write("index.jsonl", "a\n");
        using FileStream holder = new(path, FileMode.Open, FileAccess.Read, FileShare.None);
        List<TimeSpan> waits = [];

        PyText.AppendText(path, "b\n", wait =>
        {
            waits.Add(wait);
            if (waits.Count == heldThroughWaits)
            {
                holder.Dispose();
            }
        });

        Assert.Equal(heldThroughWaits, waits.Count);
        Assert.Equal("a\nb\n", File.ReadAllText(path));
    }

    [Fact]
    public void An_append_to_a_file_another_process_holds_is_tried_three_times_then_named_and_left_whole()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows refuses to replace a file another handle holds open.");
        using TempTree tree = new();
        string path = tree.Write("index.jsonl", "a\n");
        List<TimeSpan> waits = [];

        IOException refused;
        using (new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        {
            refused = Assert.Throws<IOException>(() => PyText.AppendText(path, "b\n", waits.Add));
        }

        Assert.Equal($"{path} is in use by another process (or access to it is denied); it was tried 3 times", refused.Message);
        Assert.Equal(2, waits.Count);
        Assert.Equal("a\n", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFiles(tree.Root));
    }

    [Fact]
    public void A_file_name_at_the_length_limit_can_be_written()
    {
        using TempTree tree = new();
        string path = tree.FullPath(new string('a', 250) + ".json");

        PyText.WriteText(path, "{}\n");

        Assert.Equal("{}\n", File.ReadAllText(path));
        Assert.Equal([path], Directory.GetFiles(tree.Root));
    }

    [Fact]
    public void Number_reads_any_script_s_decimal_digits_and_holds_at_the_long_range()
    {
        Assert.Equal(48, PyText.Number("48"));
        Assert.Equal(7, PyText.Number("\u0667"));
        Assert.Equal(long.MaxValue, PyText.Number("99999999999999999999"));
    }
}
