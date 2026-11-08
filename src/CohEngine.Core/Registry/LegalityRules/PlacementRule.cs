using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.LegalityRules;

/// <summary>Per-slot placement: an enhancement missing from the database is H-ENH-007, otherwise the placement verdict.</summary>
internal static class PlacementRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The legality context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(LegalityContext context)
    {
        foreach ((Power power, SlotRef slot, EnhancementLegality? enhancement) in context.Filled)
        {
            if (enhancement is null)
            {
                yield return new Diagnostic(
                    "H-ENH-007",
                    "error",
                    $"enhancement nID {slot.Enh} slotted in {PyText.Repr(power.FullName)} is not in the enhancement database")
                {
                    Fix = "remove the slot or re-dump the enhancement database",
                };
                continue;
            }

            PlacementVerdict verdict = Placement.Classify(power, enhancement, context.SetDb);
            if (verdict != PlacementVerdict.Valid)
            {
                yield return Describe(verdict, power, enhancement, context.SetDb);
            }
        }
    }

    private static Diagnostic Describe(PlacementVerdict verdict, Power power, EnhancementLegality enhancement, SetBonusDb setDb)
    {
        string name = PyText.Repr(power.FullName);
        if (verdict == PlacementVerdict.UnknownSet)
        {
            return new Diagnostic(
                "H-ENH-002",
                "error",
                $"{enhancement.Uid} in {name} is a set IO whose set (nIDSet {enhancement.NidSet}) is not in the set database")
            {
                Fix = "re-dump the set database so it includes this set, or remove the piece",
            };
        }

        if (verdict == PlacementVerdict.SetNotAccepted)
        {
            EnhancementSetDef setDef = setDb.Sets[enhancement.NidSet];
            int[] accepted = [.. power.SetTypes.Order()];
            return new Diagnostic(
                "H-ENH-001",
                "error",
                $"set {PyText.Repr(setDef.Uid)} (set type {setDef.SetType}) is not slottable in {name} (accepts set types {PyText.List(accepted)})")
            {
                Fix = "move the piece to a power that accepts this set",
                Expected = accepted,
                Actual = setDef.SetType,
            };
        }

        string message = $"{PyText.Repr(enhancement.LongName)} ({enhancement.Uid}) is not slottable in {name}: its enhancement "
            + $"class {PyText.List(enhancement.ClassIds)} is not in the power's accepted classes";
        return new Diagnostic("H-ENH-003", "error", message)
        {
            Fix = "move the piece to a power that accepts this enhancement class",
            Expected = power.Enhancements.Order().ToArray(),
            Actual = enhancement.ClassIds.ToArray(),
        };
    }
}
