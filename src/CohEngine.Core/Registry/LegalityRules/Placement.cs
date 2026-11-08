using CohEngine.Core.Data;
using CohEngine.Core.SetBonuses;

namespace CohEngine.Core.Registry.LegalityRules;

/// <summary>Placement legality: whether a power accepts an enhancement (<c>Power.IsEnhancementValid</c>).</summary>
internal static class Placement
{
    /// <summary>The placement verdict: a set IO needs a set type the power lists; a standard IO needs one of its classes in the power's list.</summary>
    /// <param name="power">The power.</param>
    /// <param name="enhancement">The enhancement.</param>
    /// <param name="setDb">The set database.</param>
    /// <returns>The verdict.</returns>
    public static PlacementVerdict Classify(Power power, EnhancementLegality enhancement, SetBonusDb setDb)
    {
        if (string.Equals(enhancement.TypeName, "SetO", StringComparison.Ordinal))
        {
            if (!setDb.Sets.TryGetValue(enhancement.NidSet, out EnhancementSetDef? setDef))
            {
                return PlacementVerdict.UnknownSet;
            }

            return SetSlotting.PowerAcceptsSet(power, setDef) ? PlacementVerdict.Valid : PlacementVerdict.SetNotAccepted;
        }

        return enhancement.ClassIds.Any(power.Enhancements.Contains) ? PlacementVerdict.Valid : PlacementVerdict.ClassNotAccepted;
    }

    /// <summary>Whether the enhancement may slot in the power.</summary>
    /// <param name="power">The power.</param>
    /// <param name="enhancement">The enhancement.</param>
    /// <param name="setDb">The set database.</param>
    /// <returns>True when the verdict is valid.</returns>
    public static bool IsEnhancementValid(Power power, EnhancementLegality enhancement, SetBonusDb setDb)
    {
        return Classify(power, enhancement, setDb) == PlacementVerdict.Valid;
    }
}
