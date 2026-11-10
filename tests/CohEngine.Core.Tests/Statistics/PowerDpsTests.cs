using CohEngine.Core.Data;
using CohEngine.Core.Statistics;

namespace CohEngine.Core.Tests.Statistics;

// test_statistics.py: P2's PowerStatisticsTests (Totals folder) hold the other eight.
//   test_global_recharge_is_folded_into_every_power     -> Global_recharge_is_folded_into_every_power
//   test_hasten_buffed_recharge_folds_the_global        -> Hasten_buffed_recharge_folds_the_global
//   test_every_power_buffed_recharge_matches_mids       -> Every_power_buffed_recharge_and_end_cost_match_mids
//   test_every_power_buffed_end_cost_matches_mids       -> Every_power_buffed_recharge_and_end_cost_match_mids
//   test_click_end_per_sec_uses_recharge_plus_cast_cadence -> Click_end_per_sec_uses_recharge_plus_cast_cadence
//   test_toggle_end_per_sec_uses_activate_period        -> Toggle_end_per_sec_uses_activate_period
//   test_one_with_the_shield_recharge_is_not_reduced    -> One_with_the_shield_recharge_is_not_reduced
//   test_zero_cadence_power_reports_zero_rates          -> Zero_cadence_power_reports_zero_rates
public sealed class PowerDpsTests
{
    [Fact]
    public void Dps_is_deferred_and_fails_loud()
    {
        SlotRef slot = new() { Level = 1, IsInherent = false, Enh = 3, Grade = 0, IoLevel = 0, RelativeLevel = 0 };
        SlotRef empty = slot with { Enh = -1 };

        Assert.StartsWith("E15: DPS for Test.Test_Set.Test_Power requires the FXGetDamageValue critical/conditional port (deferred; a plain buffed-damage sum overcounts). Slots supplied: 1", Assert.Throws<NotSupportedException>(() => PowerDps.Compute(Make.Power(), [slot, empty])).Message, StringComparison.Ordinal);
    }
}
