using CohEngine.Core.Chain;

namespace CohEngine.Core.Tests.Chain;

// The expected strings were printed once by CPython 3.14: format(value, ".Nf"), ".N%", "g" and repr(value), and sum(...).
public sealed class PyFloatTests
{
    public static TheoryData<double, int, string> FixedCases => new()
    {
        { 0.25, 1, "0.2" },
        { 0.35, 1, "0.3" },
        { 2.5, 0, "2" },
        { 3.5, 0, "4" },
        { -1.005, 2, "-1.00" },
        { 1234.5678, 3, "1234.568" },
        { 0.0, 2, "0.00" },
        { -0.0, 1, "-0.0" },
        { 0.125, 2, "0.12" },
        { 99.95, 1, "100.0" },
        { 5e-324, 3, "0.000" },
        { 1e22, 0, "10000000000000000000000" },
        { double.PositiveInfinity, 1, "inf" },
        { double.NegativeInfinity, 1, "-inf" },
        { double.NaN, 1, "nan" },
    };

    public static TheoryData<double, string> PlusCases => new()
    {
        { 1.5, "+1.50" },
        { -1.5, "-1.50" },
        { 0.0, "+0.00" },
        { -0.0, "-0.00" },
    };

    public static TheoryData<double, int, string> PercentCases => new()
    {
        { 0.85, 0, "85%" },
        { 0.005, 0, "0%" },
        { 0.3499999940395355, 0, "35%" },
        { 0.125, 1, "12.5%" },
        { 2.0, 0, "200%" },
    };

    public static TheoryData<double, string> GeneralCases => new()
    {
        { 45.0, "45" },
        { 0.1, "0.1" },
        { 1e-5, "1e-05" },
        { 1234567.0, "1.23457e+06" },
        { 100000.0, "100000" },
        { 0.0001, "0.0001" },
        { 123456.7, "123457" },
        { -2.5, "-2.5" },
        { 0.0, "0" },
        { -0.0, "-0" },
        { 999999.5, "1e+06" },
        { 0.00012345678, "0.000123457" },
        { 1e100, "1e+100" },
        { 7.000001, "7" },
        { double.PositiveInfinity, "inf" },
    };

    public static TheoryData<double, string> ReprCases => new()
    {
        { 1.0, "1.0" },
        { 0.1, "0.1" },
        { 1e16, "1e+16" },
        { 1e-5, "1e-05" },
        { 1.2345678901234568e17, "1.2345678901234568e+17" },
        { 0.0001, "0.0001" },
        { 1.5e-7, "1.5e-07" },
        { -2.5, "-2.5" },
        { 0.0, "0.0" },
        { -0.0, "-0.0" },
        { 1e15, "1000000000000000.0" },
        { 5e-324, "5e-324" },
        { 123456.789, "123456.789" },
        { 0.30000000000000004, "0.30000000000000004" },
        { 100.0, "100.0" },
        { double.NaN, "nan" },
        { double.NegativeInfinity, "-inf" },
    };

    public static TheoryData<double[], double> SumCases => new()
    {
        { [0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1, 0.1], 1.0 },
        { [1e100, 1.0, -1e100], 1.0 },
        { [], 0.0 },
        { [0.1, 0.2, 0.3], 0.6 },
    };

    [Theory]
    [MemberData(nameof(FixedCases))]
    public void Fixed_rounds_the_exact_value_half_even(double value, int digits, string expected)
    {
        Assert.Equal(expected, PyFloat.Fixed(value, digits));
    }

    [Theory]
    [MemberData(nameof(PlusCases))]
    public void Fixed_with_a_plus_flag_signs_zero_and_positives(double value, string expected)
    {
        Assert.Equal(expected, PyFloat.Fixed(value, 2, plus: true));
    }

    [Theory]
    [MemberData(nameof(PercentCases))]
    public void Percent_multiplies_by_100_then_rounds(double value, int digits, string expected)
    {
        Assert.Equal(expected, PyFloat.Percent(value, digits));
    }

    [Theory]
    [MemberData(nameof(GeneralCases))]
    public void General_keeps_six_significant_digits(double value, string expected)
    {
        Assert.Equal(expected, PyFloat.General(value));
    }

    [Theory]
    [MemberData(nameof(ReprCases))]
    public void Repr_is_the_shortest_round_trip_text(double value, string expected)
    {
        Assert.Equal(expected, PyFloat.Repr(value));
    }

    [Theory]
    [MemberData(nameof(SumCases))]
    public void Sum_compensates_like_python(double[] values, double expected)
    {
        Assert.Equal(expected, PyFloat.Sum(values));
    }

    [Fact]
    public void Sum_of_a_negative_zero_is_positive_zero()
    {
        Assert.False(double.IsNegative(PyFloat.Sum([-0.0])));
    }
}
