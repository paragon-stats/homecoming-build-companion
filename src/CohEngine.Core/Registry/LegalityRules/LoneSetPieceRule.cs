using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.SetBonuses;

namespace CohEngine.Core.Registry.LegalityRules;

/// <summary>
/// P-SET-001: a single piece of a set in one power earns no set bonus (a warning). Exempt are pieces that work alone:
/// a per-enhancement special, a unique IO, or a pure proc (no enhancement-mode effects, which needs the effects dump).
/// </summary>
internal static class LoneSetPieceRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The legality context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(LegalityContext context)
    {
        Dictionary<(int BuildIndex, int NidSet), int> counts = [];
        Dictionary<(int BuildIndex, int NidSet), (Power Power, EnhancementLegality Enhancement)> first = [];
        List<(int BuildIndex, int NidSet)> order = [];
        foreach ((Power power, _, EnhancementLegality? enhancement) in context.Filled)
        {
            if (enhancement is null || enhancement.NidSet < 0 || !context.SetDb.Sets.ContainsKey(enhancement.NidSet))
            {
                continue;
            }

            (int BuildIndex, int NidSet) key = (power.BuildIndex, enhancement.NidSet);
            if (counts.TryGetValue(key, out int count))
            {
                counts[key] = count + 1;
                continue;
            }

            counts[key] = 1;
            first[key] = (power, enhancement);
            order.Add(key);
        }

        foreach ((int BuildIndex, int NidSet) key in order.Where(key => counts[key] == 1))
        {
            (Power power, EnhancementLegality enhancement) = first[key];
            if (WorksAlone(context, enhancement))
            {
                continue;
            }

            string setUid = PyText.Repr(context.SetDb.Sets[key.NidSet].Uid);
            string message = $"{PyText.Repr(enhancement.LongName)} is the only {setUid} piece in {PyText.Repr(power.FullName)}; set bonuses "
                + "need at least 2 pieces of one set in the same power, so this piece adds no set bonus "
                + "(its own enhancement value, and any proc it carries, still apply)";
            yield return new Diagnostic("P-SET-001", "warning", message)
            {
                Fix = $"slot a second {setUid} piece in this power, use a piece carrying a global, "
                    + "or confirm the lone piece is a deliberate proc carrier or mule",
                Expected = 2,
                Actual = 1,
            };
        }
    }

    private static bool WorksAlone(LegalityContext context, EnhancementLegality enhancement)
    {
        if (enhancement.Unique)
        {
            return true;
        }

        if (context.SetDb.Sets.TryGetValue(enhancement.NidSet, out EnhancementSetDef? setDef) && SetSlotting.PieceGrantsSpecial(setDef, enhancement.Nid))
        {
            return true;
        }

        return context.EnhEffects is not null
            && context.EnhEffects.TryGetValue(enhancement.Nid, out EnhancementRecord? record)
            && record.Effects.Count == 0;
    }
}
