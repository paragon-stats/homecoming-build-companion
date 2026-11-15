using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.SetBonuses;

namespace CohEngine.Core.Registry.SlottingRules;

/// <summary>
/// P-SET-002: a lone Luck of the Gambler recharge piece while the build holds slots that diversification wastes (a
/// warning). P-SET-001 exempts the piece because it grants a special; this rule asks for a second piece when slots are free.
/// </summary>
internal static class LoneGamblerRecharge
{
    private const string GamblerUid = "Luck_of_the_Gambler";

    /// <summary>Runs the rule.</summary>
    /// <param name="context">The slotting context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(SlottingContext context)
    {
        EnhancementSetDef? gambler = context.SetDb.Sets.Values.FirstOrDefault(set => string.Equals(set.Uid, GamblerUid, StringComparison.Ordinal));
        if (gambler is null || context.EdFindings.Count == 0)
        {
            yield break;
        }

        foreach (IGrouping<int, FilledSlot> power in context.Filled.Where(slot => slot.Enhancement?.NidSet == gambler.Nid).GroupBy(slot => slot.Power.BuildIndex))
        {
            FilledSlot[] pieces = [.. power];
            if (pieces.Length == 1 && SetSlotting.PieceGrantsSpecial(gambler, pieces[0].Enhancement!.Nid))
            {
                string message = $"'{pieces[0].Enhancement!.LongName}' is the only Luck of the Gambler piece in '{pieces[0].Power.FullName}' while the build has {context.EdFindings.Count} slots that add little (P-ED-001, P-ED-002); "
                    + "a second piece earns the set's 2-piece Regeneration bonus and a third its Hit Points bonus";
                yield return new Diagnostic("P-SET-002", "warning", message)
                {
                    Fix = "move a reclaimed slot here and add Luck of the Gambler: Defense/Endurance or Defense/Endurance/Recharge, which also lower this power's endurance cost",
                };
            }
        }
    }
}
