using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>
/// H-POWER-006: at level 1 the primary pick comes before the secondary pick, the order the level-up view offers them.
/// A power counts as primary or secondary by its category, the first segment of its name, so an epic archetype's base
/// and branch sets count alike. The categories come from the file's <c>PowerSets</c>; a Mids dump directory has none,
/// so the rule stays silent there.
/// </summary>
internal static class LevelOnePickOrderRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        if (context.Format is not { PowerSets.Count: >= 2 } format)
        {
            yield break;
        }

        Power[] levelOne = [.. context.Picks.Where(pick => pick.PickLevel == 0)];
        Power? primary = levelOne.FirstOrDefault(pick => InCategory(pick, format.PowerSets[0]));
        Power? secondary = levelOne.FirstOrDefault(pick => InCategory(pick, format.PowerSets[1]));
        if (primary is not null && secondary is not null && secondary.BuildIndex < primary.BuildIndex)
        {
            string first = PyText.Repr(secondary.FullName);
            string second = PyText.Repr(primary.FullName);
            yield return new Diagnostic("H-POWER-006", "error", $"{first}, the secondary pick, comes before {second}, the primary pick, at level 1; the level-up view offers the primary pick first")
            {
                Fix = $"swap {first} and {second} so the primary pick comes first",
                Expected = primary.FullName,
                Actual = secondary.FullName,
            };
        }
    }

    private static bool InCategory(Power power, string powerSet)
    {
        return string.Equals(PowerGroups.Group(power), powerSet?.Split('.')[0], StringComparison.Ordinal);
    }
}
