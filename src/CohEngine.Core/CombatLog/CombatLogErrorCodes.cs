namespace CohEngine.Core.CombatLog;

/// <summary>The E150 to E159 block: what the combat-log reader refuses, and the message each one carries.</summary>
public static class CombatLogErrorCodes
{
    /// <summary>The combat-log file could not be opened or read.</summary>
    public const string UnreadableFile = "E150";

    /// <summary>A line starts like a timestamp but holds no real date and time.</summary>
    public const string MalformedTimestamp = "E151";

    internal static IOException Unreadable(string path, Exception inner)
    {
        return new IOException(
            $"{UnreadableFile}: cannot read combat log '{path}' ({inner.Message}); check the path and that the file is not locked",
            inner);
    }

    internal static InvalidDataException BadTimestamp(int number, string prefix)
    {
        return new InvalidDataException(
            $"{MalformedTimestamp}: line {number} starts '{prefix}', which is not a real 'YYYY-MM-DD HH:MM:SS' timestamp; "
            + "repair or delete the line");
    }
}
