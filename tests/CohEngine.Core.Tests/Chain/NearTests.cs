using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Chain;

public sealed class NearTests
{
    [Fact]
    public void A_value_inside_the_tolerance_passes()
    {
        Near.Rel(100.0, 100.00005, 1e-6);
        Near.Abs(1.0, 1.0004, 1e-3);
    }

    [Fact]
    public void A_value_outside_the_tolerance_fails_with_both_numbers()
    {
        Assert.Equal("expected 1 within 1E-06, got 1.1", Assert.Throws<InvalidOperationException>(() => Near.Rel(1.0, 1.1)).Message);
    }

    [Fact]
    public void An_absolute_miss_fails()
    {
        Assert.Throws<InvalidOperationException>(() => Near.Abs(1.0, 2.0, 0.5));
    }
}
