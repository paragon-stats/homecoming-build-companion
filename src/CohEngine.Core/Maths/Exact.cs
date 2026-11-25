namespace CohEngine.Core.Maths;

/// <summary>
/// Exact IEEE-754 equality, for the comparisons whose meaning is equality on arbitrary doubles. A comparison that
/// means something narrower says so instead: a sign test for a quantity that cannot be negative,
/// <see cref="F32.IsZero"/> for a value on the float32 grid, and a bit comparison where the representation is the question.
/// </summary>
public static class Exact
{
    /// <summary>Compares two doubles the way Python's <c>==</c> does: NaN equals nothing (not even NaN), and 0.0 equals -0.0.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns>True when the values compare equal under IEEE-754 equality.</returns>
    public static bool Same(double left, double right)
    {
        return left == right;
    }
}
