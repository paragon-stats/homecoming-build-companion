namespace CohEngine.Core.BuildFile;

/// <summary>One <c>SlotEntries</c> object of a <c>.mbd</c> file.</summary>
public sealed record MbdSlotDoc
{
    /// <summary>Gets the 1-based placement level as stored; a file without one is refused.</summary>
    public int? Level { get; set; }

    /// <summary>Gets a value indicating whether this is the inherent slot.</summary>
    public bool IsInherent { get; set; }

    /// <summary>Gets the enhancement, or null for an empty slot.</summary>
    public MbdEnhancementDoc? Enhancement { get; set; }

    /// <summary>Gets the flipped enhancement, or null.</summary>
    public MbdEnhancementDoc? FlippedEnhancement { get; set; }
}
