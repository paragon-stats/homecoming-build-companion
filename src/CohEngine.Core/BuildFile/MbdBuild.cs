namespace CohEngine.Core.BuildFile;

/// <summary>A parsed <c>.mbd</c> build; equal when every field and both lists match.</summary>
/// <param name="Meta">The <c>BuiltWith</c> block.</param>
/// <param name="Level">The header level, raw (the file stores the character level minus one).</param>
/// <param name="ClassName">The class name.</param>
/// <param name="Origin">The origin.</param>
/// <param name="Alignment">The alignment.</param>
/// <param name="Name">The build name.</param>
/// <param name="Comment">The comment.</param>
/// <param name="PowerSets">The power set names.</param>
/// <param name="LastPower">The last-power index, raw.</param>
/// <param name="PowerEntries">The power entries.</param>
public sealed record MbdBuild(
    BuildMetaData Meta,
    int Level,
    string ClassName,
    string Origin,
    string Alignment,
    string Name,
    string Comment,
    IReadOnlyList<string> PowerSets,
    int LastPower,
    IReadOnlyList<BuildPowerEntry> PowerEntries)
{
    /// <inheritdoc/>
    public bool Equals(MbdBuild? other)
    {
        return other is not null
            && Meta == other.Meta
            && Level == other.Level
            && string.Equals(ClassName, other.ClassName, StringComparison.Ordinal)
            && string.Equals(Origin, other.Origin, StringComparison.Ordinal)
            && string.Equals(Alignment, other.Alignment, StringComparison.Ordinal)
            && string.Equals(Name, other.Name, StringComparison.Ordinal)
            && string.Equals(Comment, other.Comment, StringComparison.Ordinal)
            && LastPower == other.LastPower
            && PowerSets.SequenceEqual(other.PowerSets, StringComparer.Ordinal)
            && PowerEntries.SequenceEqual(other.PowerEntries);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(Meta, Level, ClassName, Name, LastPower, PowerSets.Count, PowerEntries.Count);
    }
}
