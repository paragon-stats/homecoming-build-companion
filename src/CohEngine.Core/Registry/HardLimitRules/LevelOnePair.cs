using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>
/// Whether the two level 1 picks are a primary pick and then a secondary pick, the order the level-up view offers them.
/// A pick counts as primary or secondary by its category, the text before the first dot, compared ordinally with that of
/// <c>PowerSets[0]</c> and <c>PowerSets[1]</c>, so an epic archetype's base and branch sets count alike.
/// </summary>
internal static class LevelOnePair
{
    /// <summary>The verdict on the first two level 1 picks.</summary>
    /// <param name="powerSets">The build's <c>PowerSets</c>.</param>
    /// <param name="levelOnePowers">The full names of the level 1 picks, in pick order.</param>
    /// <returns>The verdict.</returns>
    public static LevelOnePairVerdict Classify(IReadOnlyList<string> powerSets, IReadOnlyList<string> levelOnePowers)
    {
        if (powerSets.Count < 2 || string.IsNullOrEmpty(powerSets[0]) || string.IsNullOrEmpty(powerSets[1]) || levelOnePowers.Count < 2)
        {
            return LevelOnePairVerdict.CannotTell;
        }

        (string primary, string secondary) = (PowerGroups.Group(powerSets[0]), PowerGroups.Group(powerSets[1]));
        (string first, string second) = (PowerGroups.Group(levelOnePowers[0]), PowerGroups.Group(levelOnePowers[1]));
        if (Same(first, primary) && Same(second, secondary))
        {
            return LevelOnePairVerdict.InOrder;
        }

        if (Same(first, secondary) && Same(second, primary))
        {
            return LevelOnePairVerdict.Swapped;
        }

        return Same(first, primary) ? LevelOnePairVerdict.SecondNotSecondary : LevelOnePairVerdict.FirstNotPrimary;
    }

    /// <summary>The smallest change that puts the pair in order, or null when there is nothing to change.</summary>
    /// <param name="verdict">The verdict.</param>
    /// <param name="powerSets">The build's <c>PowerSets</c>.</param>
    /// <param name="levelOnePowers">The full names of the level 1 picks, in pick order.</param>
    /// <returns>The action.</returns>
    public static string? Action(LevelOnePairVerdict verdict, IReadOnlyList<string> powerSets, IReadOnlyList<string> levelOnePowers)
    {
        return verdict switch
        {
            LevelOnePairVerdict.Swapped => $"swap {PyText.Repr(levelOnePowers[0])} and {PyText.Repr(levelOnePowers[1])} so the primary pick comes first",
            LevelOnePairVerdict.FirstNotPrimary => $"replace {PyText.Repr(levelOnePowers[0])} with a power from {PyText.Repr(powerSets[0])}",
            LevelOnePairVerdict.SecondNotSecondary => $"replace {PyText.Repr(levelOnePowers[1])} with a power from {PyText.Repr(powerSets[1])}",
            _ => null,
        };
    }

    private static bool Same(string left, string right)
    {
        return string.Equals(left, right, StringComparison.Ordinal);
    }
}
