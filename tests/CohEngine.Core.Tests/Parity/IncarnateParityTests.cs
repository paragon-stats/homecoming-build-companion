using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// Port of <c>test_incarnate_parity.py</c>: the GrantPower-delivered Alpha enhancement. Agility exercises the scalar
/// recharge path and the effect-borne Defense and Recovery path; Cardiac the DamageBuff resistance and the endurance
/// discount; the slotted build the co-ED of slotted Defense IOs with the incarnate's pre-ED Defense.
/// </summary>
[Trait("Category", "Parity")]
public sealed class IncarnateParityTests
{
    private const string Incarnate = "arsenal_dominator_incarnate";
    private const string Slotted = "arsenal_dominator_incarnate_slotted";
    private const string Cardiac = "arsenal_dominator_cardiac";
    private const string NoSlots = "arsenal_dominator_noslots";

    public static TheoryData<string> RaceFreePowers =>
    [
        "Pool.Fighting.Boxing",
        "Pool.Fighting.Tough",
        "Pool.Fighting.Weave",
        "Pool.Leadership.Defense",
        "Dominator_Assault.Arsenal_Assault.Targeting_Drone",
        "Pool.Leadership.Tactics",
    ];

    public static TheoryData<string, double> IncarnateRecharges => new()
    {
        { "Pool.Speed.Hasten", 338.34588623046875 },
        { "Pool.Fighting.Weave", 7.518797397613525 },
        { "Pool.Fighting.Tough", 7.518797397613525 },
    };

    [Theory]
    [InlineData(Incarnate)]
    [InlineData(Slotted)]
    public void Incarnate_totals_match_mids(string name)
    {
        BaseTotals result = Compute(name);
        MidsTotals expected = Fixtures.Store.Build(name).Totals;
        EngineRun.CompareTotals(expected.Totals, result.Totals, $"{name}.Totals", EngineRun.NotCompared);
        EngineRun.CompareTotals(expected.TotalsCapped, result.TotalsCapped, $"{name}.TotalsCapped", EngineRun.NotCompared);
    }

    [Fact]
    public void Cardiac_totals_match_mids()
    {
        BaseTotals result = Compute(Cardiac);
        MidsTotals expected = Fixtures.Store.Build(Cardiac).Totals;
        HashSet<string> skip = [.. EngineRun.NotCompared, "BuffRange"];
        EngineRun.CompareTotals(expected.Totals, result.Totals, $"{Cardiac}.Totals", skip);
        EngineRun.CompareTotals(expected.TotalsCapped, result.TotalsCapped, $"{Cardiac}.TotalsCapped", skip);

        // Known gap, pinned so it flips loudly: Cardiac's global Range incarnate does not fold into BuffRange yet.
        Assert.Equal(F32.Round(0.2), F32.Round(expected.Totals.BuffRange));
        Assert.Equal(0.0, result.Totals.BuffRange);
    }

    [Fact]
    public void Cardiac_combat_jumping_end_is_race_free()
    {
        // The EnduranceDiscount aggregate is 0.45, so the end cost is base / 1.45.
        PowerStatistics.PowerStats combatJumping = Stats(Cardiac).First(stats => stats.FullName.Contains("Combat_Jumping", StringComparison.Ordinal));
        EnhancedPower dumped = Fixtures.Store.Build(Cardiac).EnhancedPowers.Single(power => string.Equals(power.FullName, combatJumping.FullName, StringComparison.Ordinal));
        ParityAssert.Equal(dumped.EndCost, combatJumping.EndCost, "Combat_Jumping.EndCost");
        Assert.Equal(F32.Round(F32.Round(0.0325) / F32.Round(1.45)), combatJumping.EndCost);
    }

    [Theory]
    [MemberData(nameof(RaceFreePowers))]
    public void Slotted_per_power_recharge_is_race_free(string fullName)
    {
        ParityAssert.Equal(Dumped(Slotted, fullName).RechargeTime, Stats(Slotted).Single(stats => string.Equals(stats.FullName, fullName, StringComparison.Ordinal)).RechargeTime, $"{fullName}.RechargeTime");
    }

    [Theory]
    [MemberData(nameof(IncarnateRecharges))]
    public void Incarnate_per_power_recharge(string fullName, double buffedRecharge)
    {
        double recharge = Stats(Incarnate).Single(stats => string.Equals(stats.FullName, fullName, StringComparison.Ordinal)).RechargeTime;
        Assert.Equal(F32.Round(buffedRecharge), recharge);
        ParityAssert.Equal(Dumped(Incarnate, fullName).RechargeTime, recharge, $"{fullName}.RechargeTime");
    }

    [Fact]
    public void Incarnate_changes_totals_versus_no_incarnate()
    {
        BaseTotals withIncarnate = Compute(Incarnate);
        BuildInputs inputs = new(Incarnate);
        BaseTotals without = EngineRun.Totals(inputs, EngineRun.Slotted(inputs));
        Assert.Null(without.IncarnateAddends);
        Assert.True(withIncarnate.Totals.EndRec > without.Totals.EndRec);
        Assert.All(withIncarnate.Totals.Def.Zip(without.Totals.Def), pair => Assert.True(pair.First >= pair.Second));
        Assert.NotEqual(withIncarnate.Totals.Def, without.Totals.Def);

        // The incarnate-stripped Defense and Recovery are the no-incarnate build's.
        TotalStatistics noSlots = Fixtures.Store.Build(NoSlots).Totals.Totals;
        ParityAssert.Equal(noSlots.EndRec, without.Totals.EndRec, "EndRec");
        ParityAssert.Equal(noSlots.Def, without.Totals.Def, "Def");
    }

    [Fact]
    public void Incarnates_without_enhancement_inputs_is_refused()
    {
        BuildInputs inputs = new(Incarnate);
        ArgumentException error = Assert.Throws<ArgumentException>(() => EngineRun.Totals(inputs, new Pipeline.BuildOptions { Incarnates = inputs.Fixture.Incarnates }));
        Assert.StartsWith("E21", error.Message, StringComparison.Ordinal);
    }

    private static BaseTotals Compute(string name)
    {
        BuildInputs inputs = new(name);
        return EngineRun.Totals(inputs, EngineRun.Slotted(inputs) with { Incarnates = inputs.Fixture.Incarnates });
    }

    private static List<PowerStatistics.PowerStats> Stats(string name)
    {
        BuildInputs inputs = new(name);
        BaseTotals result = Compute(name);
        PowerStatistics.StatsContext context = new()
        {
            Config = Fixtures.Store.Config,
            Tables = Fixtures.Store.Maths,
            EnhDb = Fixtures.Store.EnhancementEffects,
            GlobalEnhance = result.GlobalEnhance,
            RechargeCap = Fixtures.Store.Archetypes.Classes[inputs.ArchetypeIndex].RechargeCap,
            IncarnateScalar = result.IncarnateAddends!.Scalar,
        };
        return PowerStatistics.ComputeBuildStats(inputs.Powers, inputs.Slots, context);
    }

    private static EnhancedPower Dumped(string name, string fullName)
    {
        return Fixtures.Store.Build(name).EnhancedPowers.Single(power => string.Equals(power.FullName, fullName, StringComparison.Ordinal));
    }
}
