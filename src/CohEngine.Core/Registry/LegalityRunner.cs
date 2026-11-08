using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry;

/// <summary>Runs the legality registry: builds the context once and folds every registered rule over it.</summary>
public static class LegalityRunner
{
    /// <summary>Every legality violation of a build; an empty list means legal.</summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="slots">The build's slots by build index.</param>
    /// <param name="enhDb">The enhancement legality records.</param>
    /// <param name="setDb">The set database.</param>
    /// <param name="enhEffects">Enhancement-mode effects, which let P-SET-001 recognise a pure proc; null leaves procs reported.</param>
    /// <returns>The diagnostics in rule order.</returns>
    public static IReadOnlyList<Diagnostic> Check(
        IReadOnlyList<Power> powers,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, EnhancementLegality> enhDb,
        SetBonusDb setDb,
        IReadOnlyDictionary<int, EnhancementRecord>? enhEffects = null)
    {
        ArgumentNullException.ThrowIfNull(powers);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(enhDb);
        ArgumentNullException.ThrowIfNull(setDb);
        FilledSlot[] filled =
        [
            .. powers.SelectMany(
                power => slots.GetValueOrDefault(power.BuildIndex, []).Where(slot => slot.Enh >= 0),
                (power, slot) => new FilledSlot(power, slot, enhDb.GetValueOrDefault(slot.Enh))),
        ];
        LegalityContext context = new(setDb, filled, enhEffects);
        return [.. RuleRegistry.LegalityRules.SelectMany(rule => rule.Run(context))];
    }
}
