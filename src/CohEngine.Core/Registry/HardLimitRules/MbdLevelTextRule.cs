using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>H-FMT-003: the header <c>Level</c> is the character level minus one, as text from "0" to "49".</summary>
internal static class MbdLevelTextRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        if (context.Format is { } format && MbdFormat.LevelTextProblem(format.LevelText) is { } problem)
        {
            yield return new Diagnostic("H-FMT-003", "error", $"the header Level {problem}")
            {
                Fix = "write the character level minus one as a string",
                Actual = PyText.Str(format.LevelText),
            };
        }
    }
}
