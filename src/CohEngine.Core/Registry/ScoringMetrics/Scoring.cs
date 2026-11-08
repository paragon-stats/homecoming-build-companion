using System.Globalization;

using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;
using CohEngine.Core.Profiles;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Registry.ScoringMetrics;

/// <summary>
/// Goal scoring: turns "is it optimized?" into numbers against a profile. Every score also runs two adherence checks:
/// an uncapped total above its AT cap is game-clamped (a P-CAP-001 warning), and toggle plus chain drain above
/// recovery is a P-END-001 warning. Both are warnings: the build stays valid.
/// </summary>
internal static class Scoring
{
    /// <summary>The Hasten duration in seconds that perma-Hasten compares the buffed recharge with.</summary>
    public const double HastenDuration = 120.0;

    /// <summary>The Domination duration in seconds that perma-Domination compares the buffed recharge with.</summary>
    public const double DominationDuration = 90.0;

    private static readonly int[] Positional = [10, 11, 12];

    private static readonly double BaseMagic = F32.Round(1.666667);

    /// <summary>Lowest of Melee, Ranged and AoE defense as a percentage.</summary>
    /// <param name="totals">The totals.</param>
    /// <returns>The percentage.</returns>
    public static double DefensePositionalMin(TotalStatistics totals)
    {
        return F32.Round(Positional.Min(index => totals.Def[index]) * 100.0);
    }

    /// <summary>Lowest typed resistance (Smashing to Psionic) as a percentage.</summary>
    /// <param name="totals">The totals.</param>
    /// <returns>The percentage.</returns>
    public static double ResistMin(TotalStatistics totals)
    {
        return F32.Round(Enumerable.Range(1, 8).Min(index => totals.Res[index]) * 100.0);
    }

    /// <summary>Whether the named power's buffed recharge is at or under the duration; false when the power is absent.</summary>
    /// <param name="powerStats">The per-power statistics.</param>
    /// <param name="suffix">The end of the power's full name.</param>
    /// <param name="duration">The duration.</param>
    /// <returns>True when the power is perma.</returns>
    public static bool PermaRecharge(IReadOnlyList<PowerStats> powerStats, string suffix, double duration)
    {
        ArgumentNullException.ThrowIfNull(powerStats);
        PowerStats? found = powerStats.FirstOrDefault(stats => stats.FullName.EndsWith(suffix, StringComparison.Ordinal));
        return found is not null && found.RechargeTime <= duration;
    }

    /// <summary>Recovery per second over the capped totals.</summary>
    /// <param name="totalsCapped">The capped totals.</param>
    /// <param name="archetype">The archetype.</param>
    /// <returns>The recovery.</returns>
    public static double EnduranceRecoveryPerSec(TotalStatistics totalsCapped, Archetype archetype)
    {
        double factor = F32.Round((totalsCapped.EndRec + 1.0) * F32.Round(archetype.BaseRecovery * BaseMagic));
        return F32.Round(factor * F32.Round((totalsCapped.EndMax / 100.0) + 1.0));
    }

    /// <summary>A P-END-001 warning when total drain per second (toggles plus the attack chain) exceeds recovery per second.</summary>
    /// <param name="totals">The uncapped totals.</param>
    /// <param name="totalsCapped">The capped totals.</param>
    /// <param name="archetype">The archetype.</param>
    /// <param name="chainDrain">The attack chain's endurance per second.</param>
    /// <returns>The warning, or null when the build is sustainable.</returns>
    public static Diagnostic? EnduranceDiagnostic(TotalStatistics totals, TotalStatistics totalsCapped, Archetype archetype, double chainDrain = 0.0)
    {
        ArgumentNullException.ThrowIfNull(totals);
        double recovery = EnduranceRecoveryPerSec(totalsCapped, archetype);
        double drain = F32.Round(totals.EndUse + chainDrain);
        if (drain <= recovery)
        {
            return null;
        }

        string detail = chainDrain > 0.0 ? $"toggles {Fixed(totals.EndUse)} + chain {Fixed(chainDrain)}" : "toggles";
        string message = $"endurance drain {Fixed(drain)}/s ({detail}) exceeds recovery {Fixed(recovery)}/s; "
            + "the build is not endurance-sustainable";
        return new Diagnostic("P-END-001", "warning", message)
        {
            Fix = "add +Recovery/+MaxEnd, reduce toggle EndRdx cost, drop a toggle, or lengthen the attack chain",
            Expected = Math.Round(recovery, 3, MidpointRounding.ToEven),
            Actual = Math.Round(drain, 3, MidpointRounding.ToEven),
        };
    }

    /// <summary>A P-CAP-001 warning for each field the engine clamped (uncapped above capped, so the excess is wasted).</summary>
    /// <param name="totals">The uncapped totals.</param>
    /// <param name="totalsCapped">The capped totals.</param>
    /// <returns>The warnings.</returns>
    public static IReadOnlyList<Diagnostic> CapAdherenceDiagnostics(TotalStatistics totals, TotalStatistics totalsCapped)
    {
        ArgumentNullException.ThrowIfNull(totals);
        ArgumentNullException.ThrowIfNull(totalsCapped);
        List<(string Label, double Uncapped, double Capped)> overshoots = [];
        for (int index = 1; index <= 8; index++)
        {
            if (totals.Res[index] > totalsCapped.Res[index])
            {
                overshoots.Add(($"Res[{index}]", totals.Res[index], totalsCapped.Res[index]));
            }
        }

        (string Label, double Uncapped, double Capped)[] scalars =
        [
            ("BuffHaste", totals.BuffHaste, totalsCapped.BuffHaste),
            ("BuffDam", totals.BuffDam, totalsCapped.BuffDam),
            ("HPRegen", totals.HpRegen, totalsCapped.HpRegen),
            ("EndRec", totals.EndRec, totalsCapped.EndRec),
        ];
        overshoots.AddRange(scalars.Where(scalar => scalar.Uncapped > scalar.Capped));
        return
        [
            .. overshoots.Select(overshoot => new Diagnostic(
                "P-CAP-001",
                "warning",
                $"{overshoot.Label} {Fixed(overshoot.Uncapped)} exceeds the AT cap {Fixed(overshoot.Capped)}; the excess is clamped and wasted")
            {
                Fix = "re-budget the over-cap slotting toward an unmet stat",
                Expected = Math.Round(overshoot.Capped, 3, MidpointRounding.ToEven),
                Actual = Math.Round(overshoot.Uncapped, 3, MidpointRounding.ToEven),
            }),
        ];
    }

    /// <summary>Resolves a target's metric name to the build's computed value through the registry.</summary>
    /// <param name="name">The metric name.</param>
    /// <param name="totals">The totals.</param>
    /// <param name="powerStats">The per-power statistics.</param>
    /// <returns>The value.</returns>
    /// <exception cref="EngineException">E17: no metric is registered under the name.</exception>
    public static MetricValue MetricValueOf(string name, TotalStatistics totals, IReadOnlyList<PowerStats> powerStats)
    {
        RegisteredMetric? metric = RuleRegistry.ScoringMetrics.FirstOrDefault(candidate => string.Equals(candidate.Info.Id, name, StringComparison.Ordinal));
        if (metric is null)
        {
            string known = PyText.List(RuleRegistry.ScoringMetrics.Select(candidate => candidate.Info.Id));
            throw new EngineException(
                $"E17: unknown scoring metric {PyText.Repr(name)}; register it with @metric({PyText.Repr(name)}) before using it in a "
                + $"profile (known metrics: {known})");
        }

        return metric.Evaluate(totals, powerStats);
    }

    /// <summary>Whether the value satisfies the target's comparison.</summary>
    /// <param name="actual">The value.</param>
    /// <param name="target">The target.</param>
    /// <returns>True when met.</returns>
    /// <exception cref="EngineException">E18: the comparison is not <c>==</c>, <c>&gt;=</c> or <c>&lt;=</c>.</exception>
    public static bool Meets(MetricValue actual, Target target)
    {
        ArgumentNullException.ThrowIfNull(target);
        return target.Comparison switch
        {
            "==" => actual.Number == target.Value.Number,
            ">=" => actual.Number >= target.Value.Number,
            "<=" => actual.Number <= target.Value.Number,
            _ => throw new EngineException(
                $"E18: unknown comparison op {PyText.Repr(target.Comparison)} in a profile target; use one of '==', '>=', '<=' in "
                + "build_profiles.json (or add the op to _meets before using it)"),
        };
    }

    /// <summary>Scores a build against a profile and collects the adherence warnings.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="totals">The uncapped totals.</param>
    /// <param name="totalsCapped">The capped totals.</param>
    /// <param name="powerStats">The per-power statistics.</param>
    /// <param name="archetype">The archetype.</param>
    /// <param name="chainDrain">The attack chain's endurance per second, which the caller derives from the power statistics.</param>
    /// <returns>The score.</returns>
    public static ScoreResult ScoreBuild(
        BuildProfile profile,
        TotalStatistics totals,
        TotalStatistics totalsCapped,
        IReadOnlyList<PowerStats> powerStats,
        Archetype archetype,
        double chainDrain)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Dictionary<string, bool> met = new(StringComparer.Ordinal);
        int metCount = 0;
        List<Diagnostic> diagnostics = [];
        foreach (Target target in profile.Targets)
        {
            MetricValue actual = MetricValueOf(target.Metric, totals, powerStats);
            bool isMet = Meets(actual, target);
            met[target.Metric] = isMet;
            metCount += isMet ? 1 : 0;
            if (!isMet)
            {
                diagnostics.Add(
                    new Diagnostic(
                        "P-GOAL-002",
                        "warning",
                        $"{profile.DisplayName}: {target.Metric} = {actual.ToPython()} does not meet {target.Comparison} {target.Value.ToPython()}")
                    {
                        Fix = $"raise {target.Metric} to satisfy the {profile.DisplayName} target",
                        Expected = target.Value.ToDiagnosticValue(),
                        Actual = actual.ToDiagnosticValue(),
                    });
            }
        }

        // Targets count, not met entries: two targets on one metric collide in the dictionary but each scores.
        double score = profile.Targets.Count == 0 ? 1.0 : F32.Round((double)metCount / profile.Targets.Count);
        diagnostics.AddRange(CapAdherenceDiagnostics(totals, totalsCapped));
        if (EnduranceDiagnostic(totals, totalsCapped, archetype, chainDrain) is { } endurance)
        {
            diagnostics.Add(endurance);
        }

        return new ScoreResult(score, met, diagnostics);
    }

    private static string Fixed(double value)
    {
        return value.ToString("F3", CultureInfo.InvariantCulture);
    }
}
