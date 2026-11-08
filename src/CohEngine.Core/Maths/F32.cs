namespace CohEngine.Core.Maths;

/// <summary>
/// IEEE-754 single-precision quantization, the C# <c>float</c> storage every Mids number lives in.
/// Same contract as the Python port's <c>maths.f32</c>: the value stays a <see cref="double"/>, rounded
/// to the nearest single (ties to even), so both sides of a parity test compare exactly.
/// </summary>
public static class F32
{
    /// <summary>Quantizes a value to the nearest single-precision value.</summary>
    /// <param name="value">The value to quantize.</param>
    /// <returns>The value as C# <c>float</c> storage holds it, widened back to double.</returns>
    /// <exception cref="OverflowException">A finite value rounds past the single range; Python's <c>struct.pack("f")</c> refuses it too.</exception>
    public static double Round(double value)
    {
        float single = (float)value;
        if (float.IsInfinity(single) && !double.IsInfinity(value))
        {
            throw new OverflowException(FormattableString.Invariant($"{value:R} is too large for single precision"));
        }

        return single;
    }
}
