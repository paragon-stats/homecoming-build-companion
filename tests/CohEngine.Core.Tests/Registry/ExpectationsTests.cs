using CohEngine.Core.Data;
using CohEngine.Core.Registry;

namespace CohEngine.Core.Tests.Registry;

public sealed class ExpectationsTests
{
    private static readonly TotalStatistics Reference = Fixtures.Store.Build("shield_scrapper_noslots").Totals.Totals;

    private static int Ranged => Fixtures.Store.Enums.Maps["eDamage"]["Ranged"];

    private static int Count(double rangedDefense, string claim)
    {
        double[] def = [.. Reference.Def];
        def[Ranged] = rangedDefense;
        ComputedBuild build = new("Class_Scrapper", Reference with { Def = def }, Reference, []);

        return Expectations.Verify([new Expectation("defense.ranged", claim)], build, Fixtures.Store.Enums, "build").Count;
    }

    [Fact]
    public void A_claim_with_every_decimal_of_the_total_holds()
    {
        Assert.Equal(0, Count(0.12345678, "12.345678"));
    }

    [Theory]
    [InlineData("12.34", 0)]
    [InlineData("12.35", 1)]
    [InlineData("12.3", 0)]
    [InlineData("12.4", 1)]
    [InlineData("12", 0)]
    public void A_total_is_rounded_once_to_the_decimals_of_the_claim_with_halves_away_from_zero(string claim, int mismatches)
    {
        Assert.Equal(mismatches, Count(0.1234499, claim));
    }

    [Theory]
    [InlineData("12.35", 0)]
    [InlineData("12.34", 1)]
    public void A_total_just_past_a_half_rounds_up(string claim, int mismatches)
    {
        Assert.Equal(mismatches, Count(0.123451, claim));
    }
}
