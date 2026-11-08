using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.LegalityRules;

/// <summary>H-ENH-006: the same set piece slotted twice in one power.</summary>
internal static class PerPowerSetDuplicateRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The legality context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(LegalityContext context)
    {
        Dictionary<int, HashSet<int>> seenByPower = [];
        foreach (FilledSlot filled in context.Filled)
        {
            if (filled.Enhancement is not { NidSet: >= 0 } enhancement)
            {
                continue;
            }

            if (!seenByPower.TryGetValue(filled.Power.BuildIndex, out HashSet<int>? seen))
            {
                seen = [];
                seenByPower[filled.Power.BuildIndex] = seen;
            }

            if (!seen.Add(enhancement.Nid))
            {
                string message = $"set piece {enhancement.Uid} is slotted more than once in {PyText.Repr(filled.Power.FullName)}; only one of "
                    + "each set piece is allowed per power";
                yield return new Diagnostic("H-ENH-006", "error", message)
                {
                    Fix = "replace the duplicate with a different piece from the set",
                };
            }
        }
    }
}
