using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Tests.Parity;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Tests.Totals;

/// <summary>
/// Port of <c>test_statistics.py</c> over <c>shield_scrapper_set_bonuses</c>, whose global recharge folds into every
/// power. The deferred DPS refusal (E15) is P3b work and ports with it.
/// </summary>
public sealed class PowerStatisticsTests
{
    private const string Build = "shield_scrapper_set_bonuses";

    private static readonly Lazy<(BuildInputs Inputs, PowerStatistics.StatsContext Context)> Shared = new(() =>
    {
        BuildInputs inputs = new(Build);
        BaseTotals totals = EngineRun.Totals(inputs, EngineRun.Slotted(inputs) with { SetDb = Fixtures.Store.SetBonuses });
        PowerStatistics.StatsContext context = new()
        {
            Config = Fixtures.Store.Config,
            Tables = Fixtures.Store.Maths,
            EnhDb = Fixtures.Store.EnhancementEffects,
            GlobalEnhance = totals.GlobalEnhance,
            RechargeCap = Fixtures.Store.Archetypes.Classes[inputs.ArchetypeIndex].RechargeCap,
        };
        return (inputs, context);
    });

    private static PowerStatistics.StatsContext Context => Shared.Value.Context;

    private static BuildInputs Inputs => Shared.Value.Inputs;

    [Fact]
    public void Global_recharge_is_folded_into_every_power()
    {
        Assert.Equal(F32.Round(0.425), Context.GlobalEnhance.Recharge);
    }

    [Fact]
    public void Hasten_buffed_recharge_folds_the_global()
    {
        // Unslotted Hasten: 450 / (1 + 0.425).
        PowerStatistics.PowerStats stats = Stats(".Hasten");
        ParityAssert.Equal(Dumped("Pool.Speed.Hasten").RechargeTime, stats.RechargeTime, "Hasten.RechargeTime");
        Assert.Equal(F32.Round(315.7895), stats.RechargeTime);
    }

    [Fact]
    public void Every_power_buffed_recharge_and_end_cost_match_mids()
    {
        Dictionary<string, PowerStatistics.PowerStats> stats = PowerStatistics.ComputeBuildStats(Inputs.Powers, Inputs.Slots, Context).ToLastWinsDictionary(item => item.FullName);
        foreach (EnhancedPower dumped in Inputs.Fixture.EnhancedPowers)
        {
            ParityAssert.Equal(dumped.RechargeTime, stats[dumped.FullName].RechargeTime, $"{dumped.FullName}.RechargeTime");
            ParityAssert.Equal(dumped.EndCost, stats[dumped.FullName].EndCost, $"{dumped.FullName}.EndCost");
        }
    }

    [Fact]
    public void Click_end_per_sec_uses_recharge_plus_cast_cadence()
    {
        EnhancedPower dumped = Dumped("Scrapper_Melee.Dark_Melee.Smite");
        double expected = F32.Round(F32.Round(dumped.EndCost) / F32.Round(F32.Round(dumped.RechargeTime) + F32.Round(F32.Round(dumped.CastTime) + F32.Round(dumped.InterruptTime))));
        PowerStatistics.PowerStats smite = Stats(".Smite");
        Assert.Equal(expected, smite.EndPerSec);
        Assert.True(smite.IsAttack);
    }

    [Fact]
    public void Toggle_end_per_sec_uses_activate_period()
    {
        Power weave = Inputs.Powers.First(power => power.FullName.EndsWith(".Weave", StringComparison.Ordinal));
        Assert.Equal(F32.Round(F32.Round(Dumped("Pool.Fighting.Weave").EndCost) / weave.ActivatePeriod), Stats(".Weave").EndPerSec);
    }

    [Fact]
    public void One_with_the_shield_recharge_is_not_reduced()
    {
        Power owts = Inputs.Powers.First(power => power.FullName.EndsWith(".One_with_the_Shield", StringComparison.Ordinal));
        Assert.Contains("RechargeTime", owts.IgnoreEnh, StringComparer.Ordinal);
        PowerStatistics.PowerStats stats = Stats(".One_with_the_Shield");
        ParityAssert.Equal(Dumped("Scrapper_Defense.Shield_Defense.One_with_the_Shield").RechargeTime, stats.RechargeTime, "OwtS.RechargeTime");
        Assert.Equal(F32.Round(360.0), stats.RechargeTime);
    }

    [Fact]
    public void Zero_cadence_power_reports_zero_rates()
    {
        Power inert = Make.Power() with { FullName = "Inert", PowerType = "Auto", NidPower = 0, StaticIndex = 0 };
        PowerStatistics.PowerStats stats = PowerStatistics.ComputePowerStats(inert, [], Context);
        Assert.Equal(0.0, stats.EndPerSec);
        Assert.Equal(0.0, stats.RechargeTime);
        Assert.False(stats.IsAttack);
    }

    private static PowerStatistics.PowerStats Stats(string suffix)
    {
        Power power = Inputs.Powers.First(candidate => candidate.FullName.EndsWith(suffix, StringComparison.Ordinal));
        return PowerStatistics.ComputePowerStats(power, Inputs.Slots.GetValueOrDefault(power.BuildIndex, []), Context);
    }

    private static EnhancedPower Dumped(string fullName)
    {
        return Inputs.Fixture.EnhancedPowers.Single(power => string.Equals(power.FullName, fullName, StringComparison.Ordinal));
    }
}
