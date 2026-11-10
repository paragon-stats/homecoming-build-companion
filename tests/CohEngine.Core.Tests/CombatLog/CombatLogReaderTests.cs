using CohEngine.Core.CombatLog;

namespace CohEngine.Core.Tests.CombatLog;

public sealed class CombatLogReaderTests
{
    [Fact]
    public void A_combat_line_splits_into_timestamp_and_body_with_no_channel()
    {
        CombatLogLine line = Assert.Single(Read("2024-04-24 00:00:58 Hasten is recharged."));

        Assert.Equal(1, line.Number);
        Assert.Equal(new DateTime(2024, 4, 24, 0, 0, 58), line.Timestamp);
        Assert.Equal("Hasten is recharged.", line.Text);
        Assert.Null(line.Channel);
        Assert.Equal("2024-04-24 00:00:58 Hasten is recharged.", line.Raw);
    }

    [Fact]
    public void A_chat_line_carries_its_channel_without_brackets()
    {
        CombatLogLine line = Assert.Single(Read("2024-04-24 00:00:58 [Looking For Group] Someone: hello"));

        Assert.Equal("Looking For Group", line.Channel);
        Assert.Equal("[Looking For Group] Someone: hello", line.Text);
    }

    [Theory]
    [InlineData("2024-04-24 00:00:58 [] empty tag")]
    [InlineData("2024-04-24 00:00:58 [unclosed tag")]
    [InlineData("2024-04-24 00:00:58 no tag [General] here")]
    public void A_bracket_that_is_not_a_leading_tag_is_not_a_channel(string raw)
    {
        Assert.Null(Assert.Single(Read(raw)).Channel);
    }

    [Fact]
    public void A_line_without_a_timestamp_is_kept_as_a_continuation()
    {
        CombatLogLine[] lines = [.. Read("2024-04-24 00:00:58 first\nsecond half of a message\n")];

        Assert.Equal(2, lines.Length);
        Assert.Equal(2, lines[1].Number);
        Assert.Null(lines[1].Timestamp);
        Assert.Equal("second half of a message", lines[1].Text);
        Assert.Equal("second half of a message", lines[1].Raw);
    }

    [Theory]
    [InlineData("short")]
    [InlineData("2024-04-24 00:00:58")]
    [InlineData("2024/04/24 00:00:58 wrong separators")]
    public void A_line_that_does_not_look_timestamped_is_a_continuation(string raw)
    {
        Assert.Null(Assert.Single(Read(raw)).Timestamp);
    }

    [Fact]
    public void Windows_line_endings_leave_no_carriage_return_on_the_text()
    {
        CombatLogLine[] lines = [.. Read("2024-04-24 00:00:58 one\r\n2024-04-24 00:00:59 two\r\n")];

        Assert.Equal(["one", "two"], lines.Select(line => line.Text), StringComparer.Ordinal);
    }

    [Fact]
    public void An_empty_body_after_the_timestamp_is_an_empty_text()
    {
        Assert.Equal(string.Empty, Assert.Single(Read("2024-04-24 00:00:58 ")).Text);
    }

    [Theory]
    [InlineData("2024-13-45 00:00:58 month thirteen")]
    [InlineData("2024-04-24 25:61:61 hour twenty-five")]
    [InlineData("2024-0a-24 00:00:58 not a number")]
    public void A_line_that_starts_like_a_timestamp_but_is_not_one_raises_E151(string raw)
    {
        InvalidDataException exception = Assert.Throws<InvalidDataException>(() => Read($"2024-04-24 00:00:58 fine\n{raw}\n").ToList());

        Assert.StartsWith("E151: line 2 starts '", exception.Message, StringComparison.Ordinal);
        Assert.Contains("repair or delete the line", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_null_reader_is_refused_at_the_call()
    {
        Assert.Throws<ArgumentNullException>(() => CombatLogReader.Read(null!));
    }

    [Fact]
    public void A_missing_file_raises_E150_naming_the_path()
    {
        string path = Path.Join(AppContext.BaseDirectory, "no-such-combat-log.txt");

        IOException exception = Assert.Throws<IOException>(() => CombatLogReader.ReadFile(path));

        Assert.StartsWith("E150: ", exception.Message, StringComparison.Ordinal);
        Assert.Contains(path, exception.Message, StringComparison.Ordinal);
        Assert.IsType<FileNotFoundException>(exception.InnerException);
    }

    [Fact]
    public void A_directory_in_place_of_a_file_raises_E150()
    {
        IOException exception = Assert.Throws<IOException>(() => CombatLogReader.ReadFile(AppContext.BaseDirectory));

        Assert.StartsWith("E150: ", exception.Message, StringComparison.Ordinal);
        Assert.NotNull(exception.InnerException);
    }

    [Fact]
    public void A_file_is_read_in_full_and_then_closed()
    {
        string path = Path.Join(Fixtures.Store.Root, "combatlog", "sample-trimmed.log");

        CombatLogLine[] lines = [.. CombatLogReader.ReadFile(path)];

        Assert.InRange(lines.Length, 150, 300);
        Assert.Equal(Enumerable.Range(1, lines.Length), lines.Select(line => line.Number));
        using FileStream reopened = new(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.True(reopened.CanWrite);
    }

    private static CombatLogLine[] Read(string text)
    {
        using StringReader reader = new(text);
        return [.. CombatLogReader.Read(reader)];
    }
}
