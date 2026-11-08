using CohEngine.Core.Data;

namespace CohEngine.Core.Registry;

/// <summary>The inputs every legality rule reads, with the build's slots walked once.</summary>
/// <param name="SetDb">The set database.</param>
/// <param name="Filled">Every filled slot in build order.</param>
/// <param name="EnhEffects">Enhancement-mode effects by index, or null when the caller has only the legality dump.</param>
internal sealed record LegalityContext(SetBonusDb SetDb, IReadOnlyList<FilledSlot> Filled, IReadOnlyDictionary<int, EnhancementRecord>? EnhEffects);
