using CohEngine.Core.Data;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// Port of <c>test_base_totals_parity.py</c>: the empty-slot builds' totals with no enhancements, and the hazard gate
/// that keeps the unported CanInclude switch from reaching a reported total. The two <c>.mbd</c> cross-checks are
/// build-file tests and port with the build-file stage.
/// </summary>
[Trait("Category", "Parity")]
public sealed class BaseTotalsParityTests
{
    public static TheoryData<string> Builds => ["shield_scrapper_noslots", "arsenal_dominator_noslots"];

    [Theory]
    [MemberData(nameof(Builds))]
    public void Base_totals_match_mids(string name)
    {
        BuildInputs inputs = new(name);
        BaseTotals result = EngineRun.Totals(inputs);
        EngineRun.CompareTotals(inputs.Fixture.Totals.Totals, result.Totals, $"{name}.Totals");
        EngineRun.CompareTotals(inputs.Fixture.Totals.TotalsCapped, result.TotalsCapped, $"{name}.TotalsCapped");
    }

    [Fact]
    public void Hazard_gate_is_non_vacuous()
    {
        // The Shield Scrapper's Critical Hit inherent carries conditional GlobalChanceMod effects.
        int excluded = new BuildInputs("shield_scrapper_noslots").Powers
            .Where(power => power.StatInclude)
            .SelectMany(power => power.Effects)
            .Count(effect => effect.ActiveConditionalsCount > 0 || !string.Equals(effect.SpecialCase, "None", StringComparison.Ordinal));
        Assert.True(excluded > 0);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Excluded_conditionals_do_not_affect_totals(string name)
    {
        BuildInputs inputs = new(name);
        BaseTotals excluded = EngineRun.Totals(inputs);
        BaseTotals forced = EngineRun.Totals(inputs, new Pipeline.BuildOptions { IncludeAllConditionals = true });
        AssertSame(excluded.Totals, forced.Totals);
        AssertSame(excluded.TotalsCapped, forced.TotalsCapped);
    }

    /// <summary>Two engine totals hold the same values, as the Python dataclass equality compares them.</summary>
    internal static void AssertSame(TotalStatistics expected, TotalStatistics actual)
    {
        Assert.Equal(expected.Def, actual.Def);
        Assert.Equal(expected.Res, actual.Res);
        Assert.Equal(expected.Mez, actual.Mez);
        Assert.Equal(expected.MezRes, actual.MezRes);
        Assert.Equal(expected.DebuffRes, actual.DebuffRes);
        Assert.Equal(expected.Elusivity, actual.Elusivity);
        Assert.Equal(EngineRun.Scalars(expected, actual).Select(item => item.Want), EngineRun.Scalars(expected, actual).Select(item => item.Got));
    }
}
