using CohEngine.Core.Maths;

using Xunit.Sdk;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// The parity comparison idiom of the Python suite, <c>f32(want) == got</c>: only the dumped side is quantized,
/// so engine output that skips its final quantization fails. A failure names the field path, so a diff can be
/// traced to its Mids reference dump entry.
/// </summary>
internal static class ParityAssert
{
    public static void Equal(double expected, double actual, string path)
    {
        double want;
        try
        {
            want = F32.Round(expected);
        }
        catch (OverflowException exception)
        {
            throw new XunitException($"{path}: the dumped value does not fit single precision ({exception.Message})");
        }

        // Plain ==, as the Python parity tests compare: 0.0 equals -0.0 and NaN equals nothing.
        if (!Exact.Same(want, actual))
        {
            throw new XunitException(FormattableString.Invariant($"{path}: expected {want:R}, got {actual:R}"));
        }
    }

    public static void Equal(IReadOnlyList<double> expected, IReadOnlyList<double> actual, string path)
    {
        Count(expected.Count, actual.Count, path);
        for (int i = 0; i < expected.Count; i++)
        {
            Equal(expected[i], actual[i], $"{path}[{i}]");
        }
    }

    public static void Count(int expected, int actual, string path)
    {
        if (expected != actual)
        {
            throw new XunitException($"{path}: expected {expected} entries, got {actual}");
        }
    }
}
