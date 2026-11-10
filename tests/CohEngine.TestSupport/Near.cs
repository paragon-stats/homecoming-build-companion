using System.Globalization;

namespace CohEngine.TestSupport;

/// <summary>The <c>pytest.approx</c> checks the Python suite uses, as failing exceptions.</summary>
public static class Near
{
    /// <summary>Fails unless a value is within a relative tolerance of the expected one.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="relative">The relative tolerance; 1e-6 is pytest's default.</param>
    public static void Rel(double expected, double actual, double relative = 1e-6)
    {
        Within(expected, actual, Math.Abs(expected * relative));
    }

    /// <summary>Fails unless a value is within an absolute tolerance of the expected one.</summary>
    /// <param name="expected">The expected value.</param>
    /// <param name="actual">The actual value.</param>
    /// <param name="absolute">The absolute tolerance.</param>
    public static void Abs(double expected, double actual, double absolute)
    {
        Within(expected, actual, absolute);
    }

    private static void Within(double expected, double actual, double tolerance)
    {
        double difference = Math.Abs(expected - actual);
        if (double.IsNaN(difference) || difference > tolerance)
        {
            throw new InvalidOperationException(string.Create(CultureInfo.InvariantCulture, $"expected {expected:R} within {tolerance:R}, got {actual:R}"));
        }
    }
}
