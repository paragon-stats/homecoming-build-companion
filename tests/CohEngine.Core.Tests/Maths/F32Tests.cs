using CohEngine.Core.Maths;

namespace CohEngine.Core.Tests.Maths;

// The expected column was produced once by the Python port, printing repr(x) and repr(f32(x)) for each input:
//   PYTHONPATH=src .venv/Scripts/python.exe -c "from coh_engine.maths import f32; print(repr(x), repr(f32(x)))"
// Both columns are the shortest round-trip spelling of an IEEE-754 double, so the literals here are exact.
public sealed class F32Tests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(1.0, 1.0)]
    [InlineData(0.1, 0.10000000149011612)]
    [InlineData(-0.1, -0.10000000149011612)]
    [InlineData(0.3333333333333333, 0.3333333432674408)]
    [InlineData(0.6666666666666666, 0.6666666865348816)]
    [InlineData(0.0500000007450581, 0.05000000074505806)]
    [InlineData(0.100000001490116, 0.10000000149011612)]
    [InlineData(1.25, 1.25)]
    [InlineData(1.67, 1.6699999570846558)]
    [InlineData(0.075, 0.07500000298023224)]
    [InlineData(0.95, 0.949999988079071)]
    [InlineData(49.99, 49.9900016784668)]
    [InlineData(1.0000000596046448, 1.0)]
    [InlineData(1.0000001788139343, 1.000000238418579)]
    [InlineData(0.9999999701976776, 1.0)]
    [InlineData(16777217.0, 16777216.0)]
    [InlineData(123456789.123, 123456792.0)]
    [InlineData(-987654.321, -987654.3125)]
    [InlineData(1e-45, 1.401298464324817e-45)]
    [InlineData(7e-46, 0.0)]
    [InlineData(1e-46, 0.0)]
    [InlineData(1.1754943508222875e-38, 1.1754943508222875e-38)]
    [InlineData(3.4028234663852886e+38, 3.4028234663852886e+38)]
    [InlineData(3.4028235677973362e+38, 3.4028234663852886e+38)]
    [InlineData(1e+38, 9.999999680285692e+37)]
    [InlineData(double.PositiveInfinity, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, double.NegativeInfinity)]
    public void Round_matches_the_python_f32(double input, double expected)
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(expected), BitConverter.DoubleToInt64Bits(F32.Round(input)));
    }

    [Fact]
    public void Round_keeps_negative_zero()
    {
        Assert.Equal(BitConverter.DoubleToInt64Bits(-0.0), BitConverter.DoubleToInt64Bits(F32.Round(-0.0)));
    }

    [Fact]
    public void Round_keeps_nan()
    {
        Assert.True(double.IsNaN(F32.Round(double.NaN)));
    }

    // Python raises OverflowError ("float too large to pack with f format") for each of these.
    [Theory]
    [InlineData(3.4028235677973366e+38)]
    [InlineData(1e+39)]
    [InlineData(-1e+300)]
    public void Round_refuses_a_finite_value_past_the_single_range(double input)
    {
        Assert.Throws<OverflowException>(() => F32.Round(input));
    }
}
