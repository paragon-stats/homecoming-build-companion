using System.Globalization;

using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Profiles;

namespace CohEngine.Core.Registry;

/// <summary>
/// Checks claims about a build against its computed numbers, so a stated number can gate a response. A claim names a
/// registered scoring metric, or a total as <c>defense.&lt;type&gt;</c> or <c>resistance.&lt;type&gt;</c> (a percentage, uncapped).
/// </summary>
public static class Expectations
{
    /// <summary>Compares each claim with the computed number; a claim matches when the number, rounded to the claim's own decimal places, equals it.</summary>
    /// <param name="expectations">The claims.</param>
    /// <param name="build">The computed build.</param>
    /// <param name="enums">The shared enum maps, which name the damage types of a <c>defense.</c> or <c>resistance.</c> claim.</param>
    /// <param name="path">The build path the diagnostics point at.</param>
    /// <returns>One error diagnostic per claim that does not match.</returns>
    /// <exception cref="EngineException">E161: a name is neither a registered metric nor a defense or resistance total; E162: a value does not fit its metric.</exception>
    public static IReadOnlyList<Diagnostic> Verify(IReadOnlyList<Expectation> expectations, ComputedBuild build, EnumMaps enums, string path)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(enums);
        List<Diagnostic> mismatches = [];
        foreach (Expectation expectation in expectations)
        {
            MetricReading reading = build.Metrics.FirstOrDefault(candidate => string.Equals(candidate.Name, expectation.Metric, StringComparison.Ordinal))
                ?? TotalReading(expectation.Metric, build.Totals, enums)
                ?? throw new EngineException(
                    $"{ErrorCodes.E161}: unknown name '{expectation.Metric}' in --expect; use a metric ({string.Join(", ", build.Metrics.Select(metric => metric.Name))}) or defense.<type> or resistance.<type>");
            (bool matches, object claimed) = Compare(reading, expectation);
            if (!matches)
            {
                string fix = "correct the claim, or change the build until the metric holds the claimed value";
                mismatches.Add(new Diagnostic("P-EXPECT-001", "error", $"{ErrorCodes.E160}: {reading.Name} is {reading.Value.ToPython()}, expected {expectation.Value}; {fix}")
                {
                    Path = path,
                    Fix = fix,
                    Expected = claimed,
                    Actual = reading.Value.ToDiagnosticValue(),
                });
            }
        }

        return mismatches;
    }

    /// <summary>Reads <c>defense.&lt;type&gt;</c> or <c>resistance.&lt;type&gt;</c> as a percentage; null when the name is not one.</summary>
    private static MetricReading? TotalReading(string name, TotalStatistics totals, EnumMaps enums)
    {
        string[] parts = name.Split('.');
        IReadOnlyList<double>? values = parts[0].ToLowerInvariant() switch
        {
            "defense" => totals.Def,
            "resistance" => totals.Res,
            _ => null,
        };
        KeyValuePair<string, int> type = enums.Maps.GetValueOrDefault("eDamage")?.FirstOrDefault(member => parts.Length == 2 && string.Equals(member.Key, parts[1], StringComparison.OrdinalIgnoreCase)) ?? default;
        return values is null || type.Key is null ? null : new MetricReading(name, MetricValue.Of(values[type.Value] * 100.0));
    }

    private static (bool Matches, object Claimed) Compare(MetricReading reading, Expectation expectation)
    {
        if (reading.Value.IsFlag)
        {
            return bool.TryParse(expectation.Value, out bool flag)
                ? (flag == reading.Value.IsTrue, flag)
                : throw new EngineException($"{ErrorCodes.E162}: --expect {reading.Name}={expectation.Value} is not true or false; {reading.Name} is a flag metric");
        }

        if (!decimal.TryParse(expectation.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal claim))
        {
            throw new EngineException($"{ErrorCodes.E162}: --expect {reading.Name}={expectation.Value} is not a number; {reading.Name} is a numeric metric");
        }

        decimal rounded = decimal.Round((decimal)reading.Value.Number, claim.Scale, MidpointRounding.AwayFromZero);
        return (rounded == claim, (double)claim);
    }
}
