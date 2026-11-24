using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.SlottingRules;

/// <summary>P-SET-003: a lower-tier set in a power while a rarer set of the same category sits unused in the build (info).</summary>
internal static class RarerSetUnused
{
    // All-unique sets that are not top tier: the canonical data tags them with a category and no rarity.
    private const string OverwhelmingForce = "Overwhelming_Force";
    private const string AttunedCupidsCrush = "Attuned_Cupids_Crush";

    private const int MinimumPieces = 2;

    /// <summary>Gets the rarer sets: those whose every piece is a unique IO, less the two all-unique sets that are not top tier.</summary>
    /// <param name="setDb">The set database.</param>
    /// <param name="enhDb">The enhancement legality records.</param>
    /// <returns>The sets, in database order.</returns>
    public static IReadOnlyList<EnhancementSetDef> RarerSets(SetBonusDb setDb, IReadOnlyDictionary<int, EnhancementLegality> enhDb)
    {
        return [.. setDb.Sets.Values.Where(set => set.Uid is not (OverwhelmingForce or AttunedCupidsCrush) && set.Enhancements.Count > 0 && set.Enhancements.All(nid => enhDb.TryGetValue(nid, out EnhancementLegality? piece) && piece.Unique))];
    }

    /// <summary>Runs the rule.</summary>
    /// <param name="context">The slotting context.</param>
    /// <returns>The findings, one per power and set.</returns>
    public static IEnumerable<Diagnostic> Run(SlottingContext context)
    {
        IReadOnlyList<EnhancementSetDef> rarer = RarerSets(context.SetDb, context.EnhDb);
        HashSet<int> rarerNids = [.. rarer.Select(set => set.Nid)];
        HashSet<string> slottedVersions =
        [
            .. context.Filled
                .Where(slot => slot.Enhancement is not null && context.SetDb.Sets.ContainsKey(slot.Enhancement.NidSet))
                .Select(slot => LegalityRules.MutexRule.StripVersionPrefixes(context.SetDb.Sets[slot.Enhancement!.NidSet].Uid)),
        ];
        foreach (IGrouping<(int BuildIndex, int NidSet), FilledSlot> group in context.Filled
            .Where(slot => slot.Enhancement is not null && context.SetDb.Sets.ContainsKey(slot.Enhancement.NidSet))
            .GroupBy(slot => (slot.Power.BuildIndex, slot.Enhancement!.NidSet)))
        {
            EnhancementSetDef lower = context.SetDb.Sets[group.Key.NidSet];
            string[] unused =
            [
                .. rarer.Where(set => set.SetType == lower.SetType && !slottedVersions.Contains(LegalityRules.MutexRule.StripVersionPrefixes(set.Uid)))
                    .OrderBy(set => IsSuperior(context, set) ? 0 : 1)
                    .ThenBy(set => set.Uid, StringComparer.Ordinal)
                    .Select(set => set.Uid),
            ];
            if (group.Skip(MinimumPieces - 1).Any() && !rarerNids.Contains(lower.Nid) && unused.Length > 0)
            {
                string verb = unused.Length == 1 ? "is" : "are";
                yield return new Diagnostic("P-SET-003", "info", $"'{lower.Uid}' fills '{group.First().Power.FullName}' while {string.Join(", ", unused)} of the same category {verb} not slotted anywhere in the build")
                {
                    Fix = $"compare the build's totals with the rarer set in this power before keeping '{lower.Uid}'",
                };
            }
        }
    }

    // A set is Superior when every piece carries the Superior flag in the legality dump.
    private static bool IsSuperior(SlottingContext context, EnhancementSetDef set)
    {
        return set.Enhancements.All(nid => context.EnhDb.TryGetValue(nid, out EnhancementLegality? piece) && piece.Superior);
    }
}
