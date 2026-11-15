using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry;

/// <summary>The inputs every slotting rule reads: the build once, and the diversification findings classified once.</summary>
/// <param name="Powers">The build's powers.</param>
/// <param name="Filled">Every filled slot in build order.</param>
/// <param name="SetDb">The set database.</param>
/// <param name="EnhEffects">Enhancement-mode effects by index.</param>
/// <param name="EdFindings">The P-ED-001 and P-ED-002 findings, which the rules that read reclaimable slots share.</param>
/// <param name="EnhDb">The enhancement legality records, for uniqueness and display names.</param>
internal sealed record SlottingContext(
    IReadOnlyList<Power> Powers,
    IReadOnlyList<FilledSlot> Filled,
    SetBonusDb SetDb,
    IReadOnlyDictionary<int, EnhancementRecord> EnhEffects,
    IReadOnlyList<Diagnostic> EdFindings,
    IReadOnlyDictionary<int, EnhancementLegality> EnhDb)
{
    /// <summary>Gets the display name of the enhancement with a UID, or the UID when the database lacks it.</summary>
    /// <param name="uid">The enhancement UID.</param>
    /// <returns>The long name.</returns>
    public string LongNameOf(string uid)
    {
        return EnhDb.Values.FirstOrDefault(record => string.Equals(record.Uid, uid, StringComparison.Ordinal))?.LongName ?? uid;
    }

    /// <summary>Gets a value indicating whether a filled slot anywhere in the build holds the enhancement with a UID.</summary>
    /// <param name="uid">The enhancement UID.</param>
    /// <returns>True when the build slots it.</returns>
    public bool Slots(string uid)
    {
        return Filled.Any(slot => string.Equals(slot.Enhancement?.Uid, uid, StringComparison.Ordinal));
    }
}
