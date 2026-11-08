using CohEngine.Core.Maths;

using Xunit.Sdk;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// The parity comparison idiom: both sides through <see cref="F32.Round"/>, then exact equality.
/// A failure names the field path, so a diff can be traced to its Mids reference dump entry.
/// </summary>
internal static class ParityAssert
{
    public static void Equal(double expected, double actual, string path)
    {
        double want = F32.Round(expected);
        double got = F32.Round(actual);

        // Plain ==, as the Python parity tests compare: 0.0 equals -0.0 and NaN equals nothing.
        if (want != got)
        {
            throw new XunitException(FormattableString.Invariant($"{path}: expected {want:R}, got {got:R}"));
        }
    }

    public static void Equal(IReadOnlyList<double> expected, IReadOnlyList<double> actual, string path)
    {
        if (expected.Count != actual.Count)
        {
            throw new XunitException($"{path}: expected {expected.Count} cells, got {actual.Count}");
        }

        for (int i = 0; i < expected.Count; i++)
        {
            Equal(expected[i], actual[i], $"{path}[{i}]");
        }
    }
}
