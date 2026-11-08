using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.LegalityRules;

/// <summary>H-ENH-004: a <c>Unique</c> IO slotted more than once build-wide.</summary>
internal static class GlobalUniqueRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The legality context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(LegalityContext context)
    {
        Dictionary<int, string> seen = [];
        foreach (FilledSlot filled in context.Filled)
        {
            if (filled.Enhancement is not { Unique: true } enhancement)
            {
                continue;
            }

            if (seen.TryGetValue(enhancement.Nid, out string? first))
            {
                string message = $"unique enhancement {PyText.Repr(enhancement.LongName)} ({enhancement.Uid}) is slotted more than once "
                    + $"(also in {PyText.Repr(first)}, now {PyText.Repr(filled.Power.FullName)}); a unique IO may be slotted once per build";
                yield return new Diagnostic("H-ENH-004", "error", message)
                {
                    Fix = "remove the extra copy",
                };
            }
            else
            {
                seen[enhancement.Nid] = filled.Power.FullName;
            }
        }
    }
}
