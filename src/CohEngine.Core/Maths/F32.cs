using System.Globalization;

namespace CohEngine.Core.Maths;

/// <summary>
/// IEEE-754 single-precision quantization, the C# <c>float</c> storage every Mids number lives in.
/// Same contract as the Python port's <c>maths.f32</c>: the value stays a <see cref="double"/>, rounded
/// to the nearest single (ties to even), so both sides of a parity test compare exactly.
/// </summary>
public static class F32
{
    // Half of float.Epsilon, the smallest single subnormal (2^-149).
    private const double HalfSmallestSingle = float.Epsilon / 2.0;

    /// <summary>Whether a single-precision value, or a sum of them, is zero of either sign.</summary>
    /// <remarks>
    /// Every such value is a whole multiple of 2^-149, so a non-zero one lies at least 2^-149 from zero; half that
    /// separates zero from all of them, and the test is exact. NaN and the infinities are not zero.
    /// </remarks>
    /// <param name="value">A value <see cref="Round"/> produced, or a sum of such values.</param>
    /// <returns>True for 0.0 and -0.0.</returns>
    public static bool IsZero(double value)
    {
        return Math.Abs(value) < HalfSmallestSingle;
    }

    /// <summary>Quantizes a value to the nearest single-precision value.</summary>
    /// <param name="value">The value to quantize.</param>
    /// <returns>The value as C# <c>float</c> storage holds it, widened back to double.</returns>
    /// <exception cref="OverflowException">A finite value rounds past the single range; Python's <c>struct.pack("f")</c> refuses it too.</exception>
    public static double Round(double value)
    {
        float single = (float)value;
        if (float.IsInfinity(single) && !double.IsInfinity(value))
        {
            throw new OverflowException(string.Create(CultureInfo.InvariantCulture, $"{value:R} is too large for single precision"));
        }

        return single;
    }
}
