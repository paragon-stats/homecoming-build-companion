namespace CohEngine.Core.Maths;

/// <summary>
/// Exact floating-point comparison, named so the intent is stated once. The Python port compares floats with
/// <c>==</c>, and the planner's branches and the captured output depend on that exactness, so a tolerance or a
/// bit comparison would change behaviour.
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
