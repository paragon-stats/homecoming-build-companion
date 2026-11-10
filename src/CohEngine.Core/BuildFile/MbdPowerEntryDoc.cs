namespace CohEngine.Core.BuildFile;

/// <summary>One <c>PowerEntries</c> object of a <c>.mbd</c> file.</summary>
public sealed record MbdPowerEntryDoc
{
    /// <summary>Gets the power's full name; empty for a placeholder.</summary>
    public string? PowerName { get; set; }

    /// <summary>Gets the 1-based pick level as stored.</summary>
    public int? Level { get; set; }

    /// <summary>Gets a value indicating whether the power counts toward totals.</summary>
    public bool StatInclude { get; set; }

    /// <summary>Gets a value indicating whether procs are included.</summary>
    public bool ProcInclude { get; set; }

    /// <summary>Gets the variable value.</summary>
    public int VariableValue { get; set; }

    /// <summary>Gets the inherent slots used.</summary>
    public int InherentSlotsUsed { get; set; }

    /// <summary>Gets the sub-powers.</summary>
    public IReadOnlyList<MbdSubPowerDoc> SubPowerEntries { get; set; } = [];

    /// <summary>Gets the slots.</summary>
    public IReadOnlyList<MbdSlotDoc> SlotEntries { get; set; } = [];
}
