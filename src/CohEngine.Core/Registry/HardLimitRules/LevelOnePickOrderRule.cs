using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>
/// H-POWER-006: the two level 1 picks are a primary pick and then a secondary pick, the order the level-up view offers them;
/// <see cref="LevelOnePair"/> decides it. A Mids dump directory has no <c>PowerSets</c>, so the rule stays silent there,
/// and fewer than two level 1 picks is H-POWER-003's. Names match ordinally, as the <c>.mbd</c> reader and the other rules
/// match them, because Mids' spelling is the contract.
/// </summary>
internal static class LevelOnePickOrderRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        if (context.Format is not { } format)
        {
            yield break;
        }

        string[] levelOne = [.. context.Picks.Where(pick => pick.PickLevel == 0).Select(pick => pick.FullName)];
        LevelOnePairVerdict verdict = LevelOnePair.Classify(format.PowerSets, levelOne);
        if (verdict is LevelOnePairVerdict.InOrder or LevelOnePairVerdict.CannotTell)
        {
            yield break;
        }

        (string first, string second) = (PyText.Repr(levelOne[0]), PyText.Repr(levelOne[1]));
        string message = verdict == LevelOnePairVerdict.Swapped
            ? $"{first}, the secondary pick, comes before {second}, the primary pick, at level 1; the level-up view offers the primary pick first"
            : $"the level 1 picks are {first} and {second}; the level-up view offers a primary pick and then a secondary pick at level 1";
        (string expected, string actual) = verdict switch
        {
            LevelOnePairVerdict.Swapped => (levelOne[1], levelOne[0]),
            LevelOnePairVerdict.FirstNotPrimary => (format.PowerSets[0], levelOne[0]),
            _ => (format.PowerSets[1], levelOne[1]),
        };
        yield return new Diagnostic("H-POWER-006", "error", message)
        {
            Fix = LevelOnePair.Action(verdict, format.PowerSets, levelOne)!,
            Expected = expected,
            Actual = actual,
        };
    }
}
