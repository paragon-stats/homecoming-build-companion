using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>
/// H-POWER-006: the two level 1 picks are a primary pick and then a secondary pick, the order the level-up view offers them.
/// A power counts as primary or secondary by its category, the first segment of its name, so an epic archetype's base
/// and branch sets count alike. The categories come from the file's <c>PowerSets</c>; a Mids dump directory has none,
/// so the rule stays silent there, and fewer than two level 1 picks is H-POWER-003's. Names match ordinally, as the
/// <c>.mbd</c> reader and the other rules match them, because Mids' spelling is the contract.
/// </summary>
internal static class LevelOnePickOrderRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        Power[] levelOne = [.. context.Picks.Where(pick => pick.PickLevel == 0)];
        if (context.Format is not { PowerSets.Count: >= 2 } format || string.IsNullOrEmpty(format.PowerSets[0]) || string.IsNullOrEmpty(format.PowerSets[1]) || levelOne.Length < 2)
        {
            yield break;
        }

        (Power first, Power second) = (levelOne[0], levelOne[1]);
        (string primarySet, string secondarySet) = (format.PowerSets[0], format.PowerSets[1]);
        (string firstName, string secondName) = (PyText.Repr(first.FullName), PyText.Repr(second.FullName));
        if (InCategory(first, secondarySet) && InCategory(second, primarySet))
        {
            yield return new Diagnostic("H-POWER-006", "error", $"{firstName}, the secondary pick, comes before {secondName}, the primary pick, at level 1; the level-up view offers the primary pick first")
            {
                Fix = $"swap {firstName} and {secondName} so the primary pick comes first",
                Expected = second.FullName,
                Actual = first.FullName,
            };
        }
        else if (!InCategory(first, primarySet) || !InCategory(second, secondarySet))
        {
            (Power replaced, string needed) = InCategory(first, primarySet) ? (second, secondarySet) : (first, primarySet);
            yield return new Diagnostic("H-POWER-006", "error", $"the level 1 picks are {firstName} and {secondName}; the level-up view offers a primary pick and then a secondary pick at level 1")
            {
                Fix = $"replace {PyText.Repr(replaced.FullName)} with a power from {PyText.Repr(needed)}",
                Expected = needed,
                Actual = replaced.FullName,
            };
        }
    }

    private static bool InCategory(Power power, string powerSet)
    {
        return string.Equals(PowerGroups.Group(power), powerSet.Split('.')[0], StringComparison.Ordinal);
    }
}
