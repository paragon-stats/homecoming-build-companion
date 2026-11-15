using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;
using CohEngine.Core.Registry.SlottingRules;

namespace CohEngine.Core.Registry;

/// <summary>Runs the slotting registry: builds the context once, classifies diversification once, and folds every rule over it.</summary>
public static class SlottingRunner
{
    /// <summary>Every slotting finding of a build; an empty list means nothing to improve.</summary>
    /// <param name="powers">The build's powers.</param>
    /// <param name="slots">The build's slots by build index.</param>
    /// <param name="enhDb">The enhancement legality records.</param>
    /// <param name="setDb">The set database.</param>
    /// <param name="enhEffects">Enhancement-mode effects by index.</param>
    /// <param name="tables">The Maths.mhd tables.</param>
    /// <returns>The diagnostics in rule order.</returns>
    public static IReadOnlyList<Diagnostic> Check(
        IReadOnlyList<Power> powers,
        IReadOnlyDictionary<int, IReadOnlyList<SlotRef>> slots,
        IReadOnlyDictionary<int, EnhancementLegality> enhDb,
        SetBonusDb setDb,
        IReadOnlyDictionary<int, EnhancementRecord> enhEffects,
        MathTables tables)
    {
        ArgumentNullException.ThrowIfNull(powers);
        ArgumentNullException.ThrowIfNull(slots);
        ArgumentNullException.ThrowIfNull(enhDb);
        ArgumentNullException.ThrowIfNull(setDb);
        ArgumentNullException.ThrowIfNull(enhEffects);
        ArgumentNullException.ThrowIfNull(tables);
        FilledSlot[] filled =
        [
            .. powers.SelectMany(
                power => slots.GetValueOrDefault(power.BuildIndex, []).Where(slot => slot.Enh >= 0),
                (power, slot) => new FilledSlot(power, slot, enhDb.GetValueOrDefault(slot.Enh))),
        ];
        SlottingContext context = new(powers, filled, setDb, enhEffects, EdOverSlotting.Classify(powers, slots, enhDb, enhEffects, tables), enhDb);
        return [.. RuleRegistry.SlottingRules.SelectMany(rule => rule.Run(context))];
    }
}
