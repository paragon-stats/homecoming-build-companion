using CohEngine.Core.Diagnostics;

namespace CohEngine.Core.Profiles;

/// <summary>A scoring value: a number or a flag, as Python's <c>float | bool</c> (a flag compares as 1 or 0).</summary>
/// <param name="Number">The number, or 1 and 0 for a flag.</param>
/// <param name="IsFlag">Whether the value is a bool.</param>
public readonly record struct MetricValue(double Number, bool IsFlag)
{
    /// <summary>Gets a value indicating whether a flag is set; <see cref="Flag"/> stores 1 for true and 0 for false.</summary>
    public bool IsTrue => Number > 0.0;

    /// <summary>Wraps a number.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The value.</returns>
    public static MetricValue Of(double number)
    {
        return new MetricValue(number, false);
    }

    /// <summary>Wraps a flag.</summary>
    /// <param name="flag">The flag.</param>
    /// <returns>The value.</returns>
    public static MetricValue Flag(bool flag)
    {
        return new MetricValue(flag ? 1.0 : 0.0, true);
    }

    /// <summary>Gets the value as a diagnostic's expected or actual: a bool or a double.</summary>
    /// <returns>The boxed value.</returns>
    public object ToDiagnosticValue()
    {
        return IsFlag ? IsTrue : Number;
    }

    /// <summary>Gets Python's <c>str</c> of the value.</summary>
    /// <returns>For example <c>True</c> or <c>45.0</c>.</returns>
    public string ToPython()
    {
        return PyText.Str(ToDiagnosticValue());
    }
}
