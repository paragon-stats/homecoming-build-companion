namespace CohEngine.Core.BuildFile;

/// <summary>One enhancement slot on a power.</summary>
/// <param name="Level">The 0-based placement level (character level - 1).</param>
/// <param name="IsInherent">Whether this is the power's inherent slot.</param>
/// <param name="Enhancement">The enhancement, or null for an empty slot.</param>
/// <param name="FlippedEnhancement">The flipped enhancement, or null.</param>
public sealed record BuildSlot(int Level, bool IsInherent, SlottedEnhancement? Enhancement, SlottedEnhancement? FlippedEnhancement);
