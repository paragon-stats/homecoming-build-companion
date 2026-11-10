using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.Maths;

public sealed class ExactTests
{
    public static TheoryData<double, double, bool> Pairs => new()
    {
        { 1.5, 1.5, true },
        { 1.5, 1.5000000000000002, false },
        { 0.0, -0.0, true },
        { -0.0, 0.0, true },
        { double.NaN, double.NaN, false },
        { double.NaN, 1.0, false },
        { double.PositiveInfinity, double.PositiveInfinity, true },
        { double.PositiveInfinity, double.NegativeInfinity, false },
    };

    [Theory]
    [MemberData(nameof(Pairs))]
    public void Same_matches_pythons_equality(double left, double right, bool expected)
    {
        Assert.Equal(expected, Exact.Same(left, right));
    }
}
