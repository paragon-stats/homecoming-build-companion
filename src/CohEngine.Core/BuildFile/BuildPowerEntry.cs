namespace CohEngine.Core.BuildFile;

/// <summary>A picked power; equal when every field and both lists match, as the Python tuples compare.</summary>
/// <param name="PowerName">The power's full name; empty for a placeholder entry.</param>
/// <param name="Level">The 0-based pick level (character level - 1).</param>
/// <param name="StatInclude">Whether the power counts toward totals.</param>
/// <param name="ProcInclude">Whether procs are included.</param>
/// <param name="VariableValue">The variable value.</param>
/// <param name="InherentSlotsUsed">The inherent slots used.</param>
/// <param name="SubPowerEntries">The sub-powers.</param>
/// <param name="SlotEntries">The slots.</param>
public sealed record BuildPowerEntry(
    string PowerName,
    int Level,
    bool StatInclude,
    bool ProcInclude,
    int VariableValue,
    int InherentSlotsUsed,
    IReadOnlyList<BuildSubPower> SubPowerEntries,
    IReadOnlyList<BuildSlot> SlotEntries)
{
    /// <inheritdoc/>
    public bool Equals(BuildPowerEntry? other)
    {
        return other is not null
            && string.Equals(PowerName, other.PowerName, StringComparison.Ordinal)
            && Level == other.Level
            && StatInclude == other.StatInclude
            && ProcInclude == other.ProcInclude
            && VariableValue == other.VariableValue
            && InherentSlotsUsed == other.InherentSlotsUsed
            && SubPowerEntries.SequenceEqual(other.SubPowerEntries)
            && SlotEntries.SequenceEqual(other.SlotEntries);
    }

    /// <inheritdoc/>
    public override int GetHashCode()
    {
        return HashCode.Combine(PowerName, Level, StatInclude, ProcInclude, VariableValue, InherentSlotsUsed, SubPowerEntries.Count, SlotEntries.Count);
    }
}
