using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.SlottingRules;

/// <summary>
/// P-IO-002: a resistance toggle that holds generic IOs while the build lacks the resistance uniques (a warning). The
/// two uniques are the ones <c>build-creation.md</c> names as the resistance toggle's contents.
/// </summary>
internal static class ResistanceMule
{
    private const string Toggle = "Toggle";
    private const string PlainIo = "InventO";
    private const string SetPiece = "SetO";
    private const string ResistanceAspect = "Resistance";

    // eSetType.Resistance in enums.json.
    private const int ResistanceSetType = 8;

    private static readonly string[] Uniques = ["Crafted_Steadfast_Protection_B", "Crafted_Gladiators_Armor_F"];

    /// <summary>Runs the rule.</summary>
    /// <param name="context">The slotting context.</param>
    /// <returns>The findings, one per power.</returns>
    public static IEnumerable<Diagnostic> Run(SlottingContext context)
    {
        string[] missing = [.. Uniques.Where(uid => !context.Slots(uid))];
        if (missing.Length == 0)
        {
            yield break;
        }

        string names = string.Join(" or ", missing.Select(context.LongNameOf));
        foreach (Power power in context.Powers.Where(power => string.Equals(power.PowerType, Toggle, StringComparison.Ordinal) && power.SetTypes.Contains(ResistanceSetType)))
        {
            FilledSlot[] held = [.. context.Filled.Where(slot => slot.Power.BuildIndex == power.BuildIndex)];
            if (held.Length > 0 && !held.Any(slot => IsType(slot, SetPiece)) && held.Any(slot => IsType(slot, PlainIo) && AddsResistance(context, slot)))
            {
                yield return new Diagnostic("P-IO-002", "warning", $"'{power.FullName}' holds only generic IOs while the build has no {names}; a resistance toggle is the home for the resistance-set uniques")
                {
                    Fix = "slot the missing unique here, or two or more pieces of one resistance set for a set bonus",
                };
            }
        }
    }

    private static bool IsType(FilledSlot slot, string typeName)
    {
        return string.Equals(slot.Enhancement?.TypeName, typeName, StringComparison.Ordinal);
    }

    private static bool AddsResistance(SlottingContext context, FilledSlot slot)
    {
        return context.EnhEffects.TryGetValue(slot.Slot.Enh, out EnhancementRecord? record)
            && record.Effects.Any(effect => string.Equals(effect.Enhance, ResistanceAspect, StringComparison.Ordinal));
    }
}
