using CohEngine.Core.Data;
using CohEngine.Core.SetBonuses;
using CohEngine.Core.Totals;

namespace CohEngine.Core.Tests.Parity;

/// <summary>
/// Port of <c>test_set_bonus_parity.py</c>: the set-bonus virtual power against Mids'
/// <c>set_bonus_virtual_power.json</c>, effect sequence and magnitudes, then folded into the totals.
/// </summary>
[Trait("Category", "Parity")]
public sealed class SetBonusParityTests
{
    private const string SetBonusBuild = "shield_scrapper_set_bonuses";
    private const string MixedSetsBuild = "shield_scrapper_mixed_sets";
    private const string GlobalEndRdxBuild = "shield_scrapper_global_endrdx";

    public static TheoryData<string> SetBonusBuilds => [SetBonusBuild, MixedSetsBuild, GlobalEndRdxBuild];

    public static TheoryData<string> NoSetBonusBuilds => ["shield_scrapper_slotted", "hasten_2rech", "shield_scrapper_noslots"];

    [Theory]
    [MemberData(nameof(SetBonusBuilds))]
    public void Assembled_virtual_power_matches_golden_sequence(string name)
    {
        Power virtualPower = VirtualPower(name);
        SetBonusVirtualPower golden = Fixtures.Store.Build(name).SetBonusVirtualPower;
        Assert.Equal(golden.EffectCount, virtualPower.Effects.Count);
        Assert.Equal(
            golden.Effects.Select(effect => (effect.EffectType, effect.DamageType, effect.MezType, effect.EtModifies)),
            virtualPower.Effects.Select(effect => (effect.EffectType, effect.DamageType, effect.MezType, effect.EtModifies)));
    }

    [Theory]
    [MemberData(nameof(SetBonusBuilds))]
    public void Assembled_virtual_power_magnitudes_match_golden(string name)
    {
        Power virtualPower = VirtualPower(name);
        int archetype = Fixtures.Store.Archetypes.NidFromUidClass(Fixtures.Store.Build(name).Totals.Class);
        IReadOnlyList<SetBonusEffect> golden = Fixtures.Store.Build(name).SetBonusVirtualPower.Effects;
        ParityAssert.Count(golden.Count, virtualPower.Effects.Count, $"{name}.Effects");
        for (int i = 0; i < golden.Count; i++)
        {
            double mag = EffectMagnitude.Mag(virtualPower.Effects[i], virtualPower, Fixtures.Store.AttribMods, Fixtures.Store.Archetypes, archetype);
            ParityAssert.Equal(golden[i].Mag, mag, $"{name}.Effects[{i}].Mag");
        }
    }

    [Fact]
    public void Unique_and_tier_co_activate_from_two_pieces()
    {
        // Deflection slots the Luck of the Gambler +Recharge special and a second piece: the 2-piece Regeneration tier
        // and the +Recharge special both fire from one set in one power.
        HashSet<(string, string)> kinds = [.. SinglePower(MixedSetsBuild, ".Deflection").Effects.Select(effect => (effect.EffectType, effect.EtModifies))];
        Assert.Contains(("Enhancement", "RechargeTime"), kinds);
        Assert.Contains(("Regeneration", "None"), kinds);
    }

    [Fact]
    public void Three_sets_in_one_power_fire_three_distinct_tiers()
    {
        HashSet<string> effectTypes = [.. SinglePower(MixedSetsBuild, ".Weave").Effects.Select(effect => effect.EffectType)];
        Assert.Superset(new HashSet<string>(["Regeneration", "Recovery", "Resistance"], StringComparer.Ordinal), effectTypes);
    }

    [Fact]
    public void Rule_of_five_caps_luck_of_the_gambler_recharge()
    {
        // The +Recharge special sits in six powers; the exact-id counter drops the sixth, so five 0.075 effects survive.
        int lotgRecharge = VirtualPower(SetBonusBuild).Effects.Count(effect =>
            string.Equals(effect.EffectType, "Enhancement", StringComparison.Ordinal) && string.Equals(effect.EtModifies, "RechargeTime", StringComparison.Ordinal) && Math.Abs(effect.Scale - 0.075) < 1e-6);
        Assert.Equal(5, lotgRecharge);
    }

    [Theory]
    [MemberData(nameof(NoSetBonusBuilds))]
    public void Generic_io_builds_have_empty_virtual_power(string name)
    {
        Assert.Equal(0, Fixtures.Store.Build(name).SetBonusVirtualPower.EffectCount);
        Assert.Empty(VirtualPower(name).Effects);
    }

    [Theory]
    [MemberData(nameof(SetBonusBuilds))]
    public void Set_bonus_totals_match_mids(string name)
    {
        BaseTotals result = SetTotals(name);
        MidsTotals expected = Fixtures.Store.Build(name).Totals;
        EngineRun.CompareTotals(expected.Totals, result.Totals, $"{name}.Totals", EngineRun.NotCompared);
        EngineRun.CompareTotals(expected.TotalsCapped, result.TotalsCapped, $"{name}.TotalsCapped", EngineRun.NotCompared);
    }

    [Fact]
    public void Global_endurance_discount_folds_into_toggle_end_use()
    {
        // Reactive Defenses' 5-piece grants a global endurance discount that folds into every running toggle's cost.
        BaseTotals result = SetTotals(GlobalEndRdxBuild);
        TotalStatistics expected = Fixtures.Store.Build(GlobalEndRdxBuild).Totals.Totals;
        ParityAssert.Equal(expected.BuffEndRdx, result.Totals.BuffEndRdx, "BuffEndRdx");
        Assert.True(result.Totals.BuffEndRdx > 0.0);
        ParityAssert.Equal(expected.EndUse, result.Totals.EndUse, "EndUse");
    }

    [Fact]
    public void Set_db_is_a_no_op_without_set_ios()
    {
        BuildInputs inputs = new("shield_scrapper_slotted");
        BaseTotals without = EngineRun.Totals(inputs, EngineRun.Slotted(inputs));
        BaseTotals withSetDb = EngineRun.Totals(inputs, EngineRun.Slotted(inputs) with { SetDb = Fixtures.Store.SetBonuses });
        BaseTotalsParityTests.AssertSame(without.Totals, withSetDb.Totals);
        BaseTotalsParityTests.AssertSame(without.TotalsCapped, withSetDb.TotalsCapped);
    }

    [Fact]
    public void Set_db_without_slots_is_refused()
    {
        BuildInputs inputs = new(SetBonusBuild);
        ArgumentException error = Assert.Throws<ArgumentException>(() => EngineRun.Totals(inputs, new Pipeline.BuildOptions { SetDb = Fixtures.Store.SetBonuses }));
        Assert.StartsWith("E14: set_db was supplied but slots and enh_db is None", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_illegal_set_piece_is_refused_before_any_total()
    {
        BuildInputs inputs = new(SetBonusBuild);
        Power[] rejecting = [.. inputs.Powers.Select(power => power with { SetTypes = [] })];
        Pipeline.BuildOptions options = EngineRun.Slotted(inputs) with { SetDb = Fixtures.Store.SetBonuses };
        InvalidDataException error = Assert.Throws<InvalidDataException>(() => Pipeline.ComputeBaseTotals(rejecting, inputs.ClassName, EngineRun.Data, options));
        Assert.StartsWith("H-ENH-001", error.Message, StringComparison.Ordinal);
    }

    private static BaseTotals SetTotals(string name)
    {
        BuildInputs inputs = new(name);
        return EngineRun.Totals(inputs, EngineRun.Slotted(inputs) with { SetDb = Fixtures.Store.SetBonuses });
    }

    private static Power VirtualPower(string name)
    {
        BuildInputs inputs = new(name);
        return Accumulator.BuildSetBonusPower(inputs.Powers, inputs.Slots, Fixtures.Store.EnhancementEffects, Fixtures.Store.SetBonuses, 50, false);
    }

    private static Power SinglePower(string name, string suffix)
    {
        BuildInputs inputs = new(name);
        Power power = inputs.Powers.First(candidate => candidate.FullName.EndsWith(suffix, StringComparison.Ordinal));
        Dictionary<int, IReadOnlyList<SlotRef>> slots = new() { [power.BuildIndex] = inputs.Slots[power.BuildIndex] };
        return Accumulator.BuildSetBonusPower([power], slots, Fixtures.Store.EnhancementEffects, Fixtures.Store.SetBonuses, 50, false);
    }
}
