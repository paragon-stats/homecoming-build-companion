using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.SlottingRules;

/// <summary>P-IO-003: survivability uniques the build leaves out while Health could hold them (info).</summary>
internal static class HealthUniques
{
    private const string HealthPower = "Inherent.Fitness.Health";

    // Numina's Convalescence: +Regeneration/+Recovery, Miracle: +Recovery, Panacea: +Hit Points/Endurance,
    // Preventive Medicine: Chance for +Absorb, Regenerative Tissue: +Regeneration.
    private static readonly string[] Uniques =
    [
        "Crafted_Numinas_Convalesence_F",
        "Crafted_Miracle_F",
        "Crafted_Panacea_F",
        "Crafted_Preventive_Medicine_F",
        "Crafted_Regenerative_Tissue_E",
    ];

    /// <summary>Runs the rule.</summary>
    /// <param name="context">The slotting context.</param>
    /// <returns>The finding, when there is one.</returns>
    public static IEnumerable<Diagnostic> Run(SlottingContext context)
    {
        string[] missing = [.. Uniques.Where(uid => !context.Slots(uid))];
        if (missing.Length > 0 && context.Powers.Any(power => string.Equals(power.FullName, HealthPower, StringComparison.Ordinal)))
        {
            yield return new Diagnostic("P-IO-003", "info", $"the build has no {string.Join(", ", missing.Select(context.LongNameOf))}; Health takes each once, and an always-on power fires a chance-based piece more often than a long-recharge click")
            {
                Fix = "slot the missing pieces in Health; take the slots from plain IOs or from a common set's last piece, not from a Very Rare, Winter or archetype set",
            };
        }
    }
}
