using CohEngine.Core.Data;
using CohEngine.Core.Exemplar;
using CohEngine.Core.Tests.Parity;

namespace CohEngine.Core.Tests.Exemplar;

public sealed class ExemplarTests
{
    private const string BaseBuild = "shield_scrapper_set_bonuses";
    private const string ExemplarBuild = "shield_scrapper_set_bonuses_exemplar25";
    private const int ExemplarLevel = 25;

    [Theory]
    [InlineData(1, 25, true)]
    [InlineData(25, 25, true)]
    [InlineData(30, 25, true)]
    [InlineData(31, 25, false)]
    [InlineData(50, 25, false)]
    [InlineData(12, 10, true)]
    public void A_power_is_available_up_to_the_exemplar_level_plus_five(int pickLevel, int exemplarLevel, bool expected)
    {
        Assert.Equal(expected, ExemplarWindow.IsPowerAvailable(pickLevel, exemplarLevel));
    }

    [Theory]
    [InlineData(15, true)]
    [InlineData(16, false)]
    public void A_slot_follows_the_same_window(int slotLevel, bool expected)
    {
        Assert.Equal(expected, ExemplarWindow.IsSlotAvailable(slotLevel, 10));
    }

    [Fact]
    public void Available_powers_keep_those_within_the_window_in_order()
    {
        IReadOnlyList<Power> powers = Fixtures.Store.Build(BaseBuild).Powers;

        IReadOnlyList<Power> kept = ExemplarWindow.AvailablePowers(powers, ExemplarLevel);

        Assert.Equal(powers.Where(power => power.PickLevel <= ExemplarLevel + 5), kept);
        Assert.True(kept.Count < powers.Count);
    }

    [Fact]
    public void With_force_level_replaces_only_the_force_level()
    {
        EngineConfig lowered = ExemplarWindow.WithForceLevel(Fixtures.Store.Config, ExemplarLevel);

        Assert.Equal(Fixtures.Store.Config with { ForceLevel = ExemplarLevel }, lowered);
    }

    [Fact]
    public void The_exemplar_gate_changes_the_totals()
    {
        Assert.NotEqual(Fixtures.Store.Build(BaseBuild).Totals.Totals.BuffHaste, Fixtures.Store.Build(ExemplarBuild).Totals.Totals.BuffHaste);
    }

    [Fact]
    public void Totals_at_force_level_25_match_the_mids_exemplar_dump()
    {
        BuildInputs inputs = new(BaseBuild);
        BuildInputs expected = new(ExemplarBuild);

        CohEngine.Core.Totals.BaseTotals result = EngineRun.Totals(
            inputs,
            EngineRun.Slotted(inputs) with { SetDb = Fixtures.Store.SetBonuses },
            forceLevel: ExemplarWindow.WithForceLevel(Fixtures.Store.Config, ExemplarLevel).ForceLevel);

        EngineRun.CompareTotals(expected.Fixture.Totals.Totals, result.Totals, ExemplarBuild, EngineRun.NotCompared);
    }

    [Fact]
    public void Null_arguments_are_refused()
    {
        Assert.Throws<ArgumentNullException>(() => ExemplarWindow.AvailablePowers(null!, 25));
        Assert.Throws<ArgumentNullException>(() => ExemplarWindow.WithForceLevel(null!, 25));
    }
}
