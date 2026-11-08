using System.Globalization;

namespace CohEngine.Core.CombatLog;

/// <summary>Splits a combat-log file into <see cref="CombatLogLine"/> records, one per line, none dropped.</summary>
public static class CombatLogReader
{
    private const string TimestampFormat = "yyyy-MM-dd HH:mm:ss";
    private const int TimestampLength = 19;

    /// <summary>Reads the log at <paramref name="path"/>; the file is closed when the enumeration ends.</summary>
    /// <param name="path">The log file.</param>
    /// <returns>The lines, in file order.</returns>
    /// <exception cref="IOException">E150, when the file cannot be opened.</exception>
    public static IEnumerable<CombatLogLine> ReadFile(string path)
    {
        StreamReader reader;
        try
        {
            reader = new StreamReader(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw CombatLogErrorCodes.Unreadable(path, exception);
        }

        return ReadAndClose(reader);
    }

    /// <summary>Reads every line of <paramref name="reader"/> lazily.</summary>
    /// <param name="reader">The source text.</param>
    /// <returns>The lines, in order.</returns>
    /// <exception cref="InvalidDataException">E151, on a line that starts like a timestamp but is not one.</exception>
    public static IEnumerable<CombatLogLine> Read(TextReader reader)
    {
        ArgumentNullException.ThrowIfNull(reader);
        return ReadLines(reader);
    }

    private static IEnumerable<CombatLogLine> ReadLines(TextReader reader)
    {
        int number = 0;
        while (reader.ReadLine() is { } raw)
        {
            number++;
            yield return Split(number, raw);
        }
    }

    private static IEnumerable<CombatLogLine> ReadAndClose(StreamReader reader)
    {
        using (reader)
        {
            foreach (CombatLogLine line in ReadLines(reader))
            {
                yield return line;
            }
        }
    }

    private static CombatLogLine Split(int number, string raw)
    {
        if (!LooksTimestamped(raw))
        {
            return new CombatLogLine(number, null, raw, null, raw);
        }

        if (!DateTime.TryParseExact(
                raw.AsSpan(0, TimestampLength), TimestampFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out DateTime stamp))
        {
            throw CombatLogErrorCodes.BadTimestamp(number, raw[..TimestampLength]);
        }

        string text = raw[(TimestampLength + 1)..];
        return new CombatLogLine(number, stamp, text, ChannelOf(text), raw);
    }

    private static bool LooksTimestamped(string raw)
    {
        return raw.Length > TimestampLength
            && char.IsAsciiDigit(raw[0])
            && raw[4] == '-'
            && raw[7] == '-'
            && raw[10] == ' '
            && raw[13] == ':'
            && raw[16] == ':'
            && raw[TimestampLength] == ' ';
    }

    private static string? ChannelOf(string text)
    {
        int close = text.IndexOf(']', StringComparison.Ordinal);
        return text.StartsWith('[') && close > 1 ? text[1..close] : null;
    }
}
