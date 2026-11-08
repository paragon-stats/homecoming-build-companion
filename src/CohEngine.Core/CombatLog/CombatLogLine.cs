namespace CohEngine.Core.CombatLog;

/// <summary>One line of a combat-log file, split into its timestamp and message body.</summary>
/// <param name="Number">The 1-based position of the line in the file.</param>
/// <param name="Timestamp">The second-resolution timestamp, or null on a continuation line that carries none.</param>
/// <param name="Text">The message body after the timestamp (the whole line when there is no timestamp).</param>
/// <param name="Channel">The chat channel tag without brackets, or null on a combat line.</param>
/// <param name="Raw">The line exactly as the file held it.</param>
public sealed record CombatLogLine(int Number, DateTime? Timestamp, string Text, string? Channel, string Raw);
