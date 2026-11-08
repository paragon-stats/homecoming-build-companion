using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Registry.HardLimitRules;

/// <summary>
/// Power prerequisites (<c>Build.MeetsRequirement</c> steps C and D). A power's required list is an OR of AND groups,
/// satisfied when some group has every member in the build at or below this pick (H-POWER-004); any power in the
/// forbidden list that is in the build is a conflict (H-POWER-005).
/// </summary>
internal static class PrerequisitesRule
{
    /// <summary>Runs the rule.</summary>
    /// <param name="context">The hard-limits context.</param>
    /// <returns>The findings.</returns>
    public static IEnumerable<Diagnostic> Run(HardLimitsContext context)
    {
        Dictionary<string, int> history = [];
        foreach (Power power in context.Powers.Where(power => power.BuildIndex >= 0))
        {
            history[power.FullName] = power.PickLevel;
        }

        return context.Picks.SelectMany(pick => Check(pick, history));
    }

    private static IEnumerable<Diagnostic> Check(Power power, Dictionary<string, int> history)
    {
        if (power.RequiresPowers.Count > 0 && !power.RequiresPowers.Any(group => group.All(required => history.TryGetValue(required, out int level) && level <= power.PickLevel)))
        {
            string options = string.Join(" or ", power.RequiresPowers.Select(group => string.Join(" + ", group)));
            string message = $"{PyText.Repr(power.FullName)} requires a prerequisite power ({options}) picked at or before it, "
                + "but none is satisfied";
            yield return new Diagnostic("H-POWER-004", "error", message)
            {
                Fix = "pick the prerequisite power(s) at an earlier level, or drop this power",
            };
        }

        foreach (string forbidden in power.RequiresPowersNot.SelectMany(group => group).Distinct(StringComparer.Ordinal).Where(history.ContainsKey))
        {
            yield return new Diagnostic("H-POWER-005", "error", $"{PyText.Repr(power.FullName)} is mutually exclusive with {PyText.Repr(forbidden)}, which is also in the build")
            {
                Fix = "remove one of the two mutually exclusive powers",
            };
        }
    }
}
