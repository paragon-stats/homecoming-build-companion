using System.Globalization;

using CohEngine.Core.Diagnostics;
using CohEngine.Core.Profiles;

namespace CohEngine.Core.Registry;

/// <summary>Checks claims about a build against its computed metrics, so a stated number can gate a response.</summary>
public static class Expectations
{
    /// <summary>Compares each claim with the computed metric; a claim matches when the metric, rounded to the claim's own decimal places, equals it.</summary>
    /// <param name="expectations">The claims.</param>
    /// <param name="build">The computed build.</param>
    /// <param name="path">The build path the diagnostics point at.</param>
    /// <returns>One error diagnostic per claim that does not match.</returns>
    /// <exception cref="EngineException">E161: a metric is not registered; E162: a value does not fit its metric.</exception>
    public static IReadOnlyList<Diagnostic> Verify(IReadOnlyList<Expectation> expectations, ComputedBuild build, string path)
    {
        ArgumentNullException.ThrowIfNull(expectations);
        ArgumentNullException.ThrowIfNull(build);
        List<Diagnostic> mismatches = [];
        foreach (Expectation expectation in expectations)
        {
            MetricReading reading = build.Metrics.FirstOrDefault(candidate => string.Equals(candidate.Name, expectation.Metric, StringComparison.Ordinal))
                ?? throw new EngineException(
                    $"{ErrorCodes.E161}: unknown scoring metric '{expectation.Metric}' in --expect; use one of: {string.Join(", ", build.Metrics.Select(metric => metric.Name))}");
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
