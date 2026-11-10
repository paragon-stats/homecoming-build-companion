using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Tests.Chain;

// Ported from tests/test_attack_chain.py; pytest.approx(x, abs=a) is Assert.Equal(x, y, digits) and rel= is a relative range.
public sealed class AttackChainsTests
{
    private static readonly EnduranceBudget Rich = new(100.0, 100.0, 0.0);
    private static readonly Movement Move = new(10.0, 20.0);

    public static TheoryData<double> BadTimelines => [0.0, -0.1, 1.5];

    public static TheoryData<double> SwitchTimes => [0.7, 1.1, 1.3, 1.7, 2.3, 3.1];

    private static PowerStats Stat(string name, double recharge, double cast, double end, bool attack = true)
    {
        return new PowerStats(name, recharge, end, 0.0, 0.0, cast, attack);
    }

    private static ChainAttack Atk(string name, double damage = 100.0, double cast = 1.0, double recharge = 2.0, double end = 5.0, bool melee = true, Shape shape = Shape.SingleTarget, int maxTargets = 1, Role role = Role.Chain)
    {
        AttackProfile profile = new(name, melee ? Delivery.Melee : Delivery.Ranged, shape, role, melee ? 7.0 : 80.0, 0.0, 0.0, maxTargets, [], []);
        return new ChainAttack { FullName = name, Damage = damage, CastTime = cast, RechargeTime = recharge, EndCost = end, Profile = profile };
    }

    private static ChainPlan Plan(IEnumerable<ChainAttack> attacks, Stance stance = Stance.Melee, Movement? movement = null, EnduranceBudget? budget = null, Scenario? scenario = null, double timeline = 1.0, double otherDrain = 0.0, double minCycles = 4.0, int maxAttacks = 12)
    {
        return AttackChains.Plan(attacks, stance, movement ?? Move, scenario ?? new Scenario(1), budget ?? Rich, timeline, otherDrain, minCycles, maxAttacks);
    }

    private static string Names(ChainOption option)
    {
        return string.Join(',', option.Rotation);
    }

    [Fact]
    public void No_attacks_is_an_empty_chain()
    {
        AttackChain chain = AttackChains.Build([Stat("Toggle", 0.0, 0.0, 0.0, attack: false)]);

        Assert.Equal("0 True 0 0", $"{chain.Rotation.Count} {chain.GapFree} {chain.EndPerSec} {chain.CycleTime}");
    }

    [Fact]
    public void A_gap_free_chain_cycles_on_cast_time()
    {
        AttackChain chain = AttackChains.Build([.. Enumerable.Range(0, 3).Select(index => Stat($"A{index}", 1.0, 1.0, 6.0))]);

        Assert.Equal($"True {F32.Round(3.0)} {F32.Round(3.0)} 0 {F32.Round(18.0 / 3.0)}", $"{chain.GapFree} {chain.CastSum} {chain.CycleTime} {chain.Gap} {chain.EndPerSec}");
    }

    [Fact]
    public void A_slow_attack_creates_a_gap()
    {
        AttackChain chain = AttackChains.Build([Stat("A", 5.0, 1.0, 5.0), Stat("B", 1.0, 1.0, 5.0)]);

        Assert.Equal($"False 2 6 4 {F32.Round(10.0 / 6.0)}", $"{chain.GapFree} {chain.CastSum} {chain.CycleTime} {chain.Gap} {chain.EndPerSec}");
    }

    [Fact]
    public void The_rotation_orders_longest_recharge_first()
    {
        AttackChain chain = AttackChains.Build([Stat("Fast", 2.0, 1.0, 5.0), Stat("Slow", 8.0, 1.0, 5.0), Stat("Mid", 4.0, 1.0, 5.0)]);

        Assert.Equal("Slow,Mid,Fast", string.Join(',', chain.Rotation));
    }

    [Fact]
    public void Non_attacks_are_excluded()
    {
        AttackChain chain = AttackChains.Build([Stat("Attack", 1.0, 1.0, 5.0), Stat("Heal", 10.0, 2.0, 20.0, attack: false)]);

        Assert.Equal($"Attack {F32.Round(2.0)} {F32.Round(5.0 / 2.0)}", $"{string.Join(',', chain.Rotation)} {chain.CycleTime} {chain.EndPerSec}");
    }

    [Fact]
    public void A_chain_with_no_cadence_drains_nothing()
    {
        Assert.Equal(0.0, AttackChains.Build([Stat("Idle", 0.0, 0.0, 5.0)]).EndPerSec);
    }

    [Fact]
    public void Arcana_cast_time_matches_mids()
    {
        Assert.Equal($"1.188 1.848 {F32.Round(AttackChains.ArcanaCastTime(1.0))}", $"{Math.Round(AttackChains.ArcanaCastTime(1.0), 6)} {Math.Round(AttackChains.ArcanaCastTime(1.67), 6)} {AttackChains.ArcanaCastTime(1.0)}");
    }

    [Fact]
    public void A_weak_third_attack_lengthens_the_cycle_so_the_best_chain_drops_it()
    {
        ChainPlan plan = Plan([Atk("A", 100), Atk("B", 60), Atk("C", 10)]);
        ChainOption best = plan.BestOverall!;

        Assert.Equal("A,B False 7 0 True", $"{Names(best)} {best.GapFree} {plan.Ranked.Count} {plan.Dropped.Count} {ReferenceEquals(plan.Ranked[0], best)}");
        Assert.Equal(new[] { 3.188, 0.812, 160.0 / 3.188, 10.0 / 3.188 }, new[] { best.CycleTime, best.Gap, best.Dps, best.EndPerSec }, new TolerantComparer());
    }

    [Fact]
    public void Three_casts_cover_a_two_second_recharge()
    {
        ChainOption full = Plan([Atk("A"), Atk("B"), Atk("C")]).Ranked.First(option => option.Rotation.Count == 3);

        Assert.Equal("True 3.564", $"{full.GapFree && full.Gap <= 0.0} {Math.Round(full.CycleTime, 5)}");
    }

    [Fact]
    public void An_expensive_attack_tops_dps_but_the_sustainable_pick_avoids_it()
    {
        ChainPlan plan = Plan([Atk("X", 500, end: 40.0), Atk("Y", 50, end: 2.0)], budget: new EnduranceBudget(100.0, 2.0, 0.0));

        Assert.Equal("X,Y False True Y True True", $"{Names(plan.BestOverall!)} {plan.BestOverall!.Sustainable} {plan.BestOverall!.NetEndPerSec < 0.0} {Names(plan.BestSustainable!)} {plan.BestSustainable!.Sustainable} {double.IsPositiveInfinity(plan.BestSustainable!.TimeToEmpty)}");
        Assert.InRange(plan.BestOverall!.TimeToEmpty, (100.0 / 11.17) * 0.99, (100.0 / 11.17) * 1.01);
        Assert.Same(plan.BestSustainable, plan.Ranked[0]);
    }

    [Fact]
    public void An_affordable_draining_chain_outranks_a_sustainable_idle_one()
    {
        ChainPlan plan = Plan([Atk("A", 200, end: 2.6), Atk("B", 200, end: 2.6)], budget: new EnduranceBudget(100.0, 2.0, 1.0));
        ChainOption affordable = plan.BestAffordable!;

        Assert.Equal("A,B False True 1 True True", $"{Names(affordable)} {affordable.Sustainable} {affordable.TimeToEmpty >= 4 * affordable.CycleTime} {plan.BestSustainable!.Rotation.Count} {affordable.Dps > plan.BestSustainable.Dps} {ReferenceEquals(plan.Ranked[0], affordable)}");
    }

    [Fact]
    public void A_debuffing_attack_earns_its_slot_through_the_other_attacks()
    {
        ChainAttack main = Atk("Main", 100, recharge: 1.0);
        ChainAttack plain = Atk("Plain", 60, recharge: 1.0);
        ChainAttack debuff = Atk("Debuff", 40, recharge: 1.0) with { TargetResDebuff = 0.5, TargetResDebuffDuration = 60.0 };
        ChainPlan plan = Plan([main, plain, debuff]);
        ChainOption withDebuff = plan.Ranked.First(option => option.Rotation.Count == 2 && Names(option).Contains("Debuff", StringComparison.Ordinal) && Names(option).Contains("Main", StringComparison.Ordinal));
        ChainOption withPlain = plan.Ranked.First(option => option.Rotation.Count == 2 && Names(option).Contains("Plain", StringComparison.Ordinal) && Names(option).Contains("Main", StringComparison.Ordinal));

        Assert.Equal("0.5 0 True True True", $"{withDebuff.ResDebuff} {withPlain.ResDebuff} {withDebuff.Dps < withPlain.Dps} {withDebuff.Dps * (1 + withDebuff.ResDebuff) > withPlain.Dps} {plan.Ranked.ToList().IndexOf(withDebuff) < plan.Ranked.ToList().IndexOf(withPlain)}");
    }

    [Fact]
    public void With_recovery_below_every_drain_there_is_no_sustainable_option()
    {
        ChainPlan plan = Plan([Atk("X", end: 40.0)], budget: new EnduranceBudget(100.0, 0.1, 0.0));

        Assert.True(plan.BestSustainable is null && plan.BestOverall is not null);
    }

    [Fact]
    public void A_chain_lasting_its_minimum_cycles_outranks_a_faster_one_that_does_not()
    {
        ChainPlan plan = Plan([Atk("Hot", 500, end: 100.0), Atk("Cool", 10, end: 1.0)], budget: new EnduranceBudget(100.0, 0.0, 0.0));

        Assert.Equal("Cool Cool,Hot", $"{Names(plan.Ranked[0])} {Names(plan.BestOverall!)}");
    }

    [Fact]
    public void Raising_min_cycles_past_a_chains_endurance_makes_it_unaffordable()
    {
        EnduranceBudget budget = new(100.0, 0.0, 0.0);
        ChainAttack attack = Atk("A", end: 10.0);

        Assert.Equal("True True", $"{Plan([attack], budget: budget, minCycles: 4.0).BestAffordable is not null} {Plan([attack], budget: budget, minCycles: 12.0).BestAffordable is null}");
    }

    [Fact]
    public void Other_drain_is_added_before_the_net_rate_is_taken()
    {
        EnduranceBudget budget = new(100.0, 3.0, 0.0);

        Assert.Equal("True True", $"{Plan([Atk("A")], budget: budget).BestSustainable is not null} {Plan([Atk("A")], budget: budget, otherDrain: 5.0).BestSustainable is null}");
    }

    [Fact]
    public void Ranged_stance_drops_melee_attacks_without_marking_them_dropped()
    {
        ChainPlan plan = Plan([Atk("Melee"), Atk("Bolt", melee: false), Atk("Burst", melee: false, shape: Shape.Pbaoe)], Stance.Ranged);

        Assert.Equal("Bolt 0", $"{string.Join('|', plan.Ranked.Select(Names))} {plan.Dropped.Count}");
    }

    [Fact]
    public void Hybrid_mixing_pays_two_switches_and_a_pure_subset_pays_none()
    {
        ChainPlan plan = Plan([Atk("M"), Atk("R", melee: false, recharge: 9.0)], Stance.Hybrid);
        Dictionary<string, ChainOption> byRotation = plan.Ranked.ToDictionary(Names, StringComparer.Ordinal);
        ChainOption mixed = byRotation["M,R"];

        Assert.Equal($"2 4 {Math.Round(Math.Max(2.376 + 4.0, 1.188 + 9.0), 5)} Hybrid 0 0 0", $"{mixed.Switches} {mixed.MoveTime} {Math.Round(mixed.CycleTime, 5)} {mixed.Stance} {byRotation["R"].Switches} {byRotation["R"].MoveTime} {byRotation["M"].Switches}");
    }

    [Fact]
    public void The_melee_stance_never_pays_switches()
    {
        Assert.All(Plan([Atk("M"), Atk("R", melee: false)]).Ranked, option => Assert.True(option.Switches == 0 && option.MoveTime == 0.0));
    }

    [Fact]
    public void A_stationary_character_cannot_use_mixed_hybrid_subsets()
    {
        Movement stuck = new(0.0, 20.0);
        ChainAttack[] attacks = [Atk("M"), Atk("R", melee: false)];

        Assert.Equal("M|R 3", string.Join('|', Plan(attacks, Stance.Hybrid, stuck).Ranked.Select(Names).Order(StringComparer.Ordinal)) + " " + Plan(attacks, Stance.Melee, stuck).Ranked.Count);
    }

    [Fact]
    public void Area_damage_scales_with_targets_hit_up_to_the_cap()
    {
        ChainAttack aoe = Atk("Nova", 100.0, shape: Shape.TargetedAoe, maxTargets: 10);

        Assert.Equal("100 500", $"{Plan([aoe], scenario: new Scenario(1)).BestOverall!.DamagePerCycle} {Plan([aoe], scenario: new Scenario(5)).BestOverall!.DamagePerCycle}");
    }

    [Fact]
    public void Half_the_timeline_halves_dps_and_drain()
    {
        ChainOption full = Plan([Atk("A")]).BestOverall!;
        ChainOption half = Plan([Atk("A")], timeline: 0.5).BestOverall!;

        Assert.Equal(new[] { full.Dps / 2.0, full.EndPerSec / 2.0, full.CycleTime }, new[] { half.Dps, half.EndPerSec, half.CycleTime }, new TolerantComparer());
    }

    [Theory]
    [MemberData(nameof(BadTimelines))]
    public void A_timeline_out_of_range_is_e45(double timeline)
    {
        Assert.StartsWith(ErrorCodes.E45, Assert.Throws<EngineException>(() => Plan([Atk("A")], timeline: timeline)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Above_max_attacks_the_lowest_damage_per_cast_are_dropped()
    {
        ChainPlan plan = Plan([Atk("Lo", 10), Atk("Hi", 90), Atk("Mid", 50)], maxAttacks: 2);

        Assert.Equal("Lo Hi,Mid 3", $"{string.Join(',', plan.Dropped)} {string.Join(',', plan.Ranked.SelectMany(option => option.Rotation).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal))} {plan.Ranked.Count}");
    }

    [Fact]
    public void Non_chain_roles_leave_an_empty_plan()
    {
        ChainPlan plan = Plan([Atk("Aura", role: Role.Aura)], Stance.Hybrid);

        Assert.Equal("True True 0 0 Hybrid", $"{plan.BestSustainable is null} {plan.BestOverall is null} {plan.Ranked.Count} {plan.Dropped.Count} {plan.Stance}");
    }

    [Fact]
    public void Equal_dps_ties_break_on_gap_then_size_then_name()
    {
        ChainPlan plan = Plan([Atk("B", 0.0), Atk("A", 0.0)]);

        Assert.Equal("A,B|A|B True", string.Join('|', plan.Ranked.Select(Names)) + " " + ReferenceEquals(plan.BestOverall, plan.Ranked[0]));
    }

    [Fact]
    public void Twelve_attacks_search_all_4095_subsets()
    {
        Assert.Equal(4095, Plan([.. Enumerable.Range(0, 12).Select(index => Atk($"P{index:00}", 10.0 + index, recharge: 2.0 + index))]).Ranked.Count);
    }

    [Fact]
    public void A_debuff_lasting_one_cycle_is_up_half_as_often_under_half_the_timeline()
    {
        ChainAttack attack = Atk("A") with { TargetResDebuff = 0.5, TargetResDebuffDuration = 3.0 };
        ChainOption full = Plan([attack]).BestOverall!;
        ChainOption half = Plan([attack], timeline: 0.5).BestOverall!;

        Assert.Equal(full.ResDebuff / 2.0, half.ResDebuff, 5);
    }

    [Fact]
    public void Affordability_uses_the_effective_period_under_a_reduced_timeline()
    {
        EnduranceBudget budget = new(100.0, 0.0, 0.0);
        ChainAttack attack = Atk("A", end: 10.0);

        Assert.Equal("True True", $"{Plan([attack], budget: budget, otherDrain: 1.0, minCycles: 7.0).BestAffordable is not null} {Plan([attack], budget: budget, otherDrain: 1.0, minCycles: 7.0, timeline: 0.5).BestAffordable is null}");
    }

    [Theory]
    [MemberData(nameof(SwitchTimes))]
    public void A_cast_bound_hybrid_option_has_exactly_zero_gap(double switchTime)
    {
        Movement move = new(10.0, 10.0 * switchTime);
        ChainPlan plan = Plan([Atk("M", recharge: 0.1), Atk("R", melee: false, recharge: 0.1)], Stance.Hybrid, move);
        ChainOption mixed = plan.Ranked.First(option => option.Switches == 2);

        Assert.True(mixed.Gap.Equals(0.0) && mixed.GapFree);
    }

    [Fact]
    public void A_large_switch_time_prefers_a_pure_subset()
    {
        ChainPlan plan = Plan([Atk("M"), Atk("R", melee: false)], Stance.Hybrid, new Movement(1.0, 60.0));

        Assert.Equal("0 1", $"{plan.BestOverall!.Switches} {plan.BestOverall.Rotation.Count}");
    }

    private sealed class TolerantComparer : IEqualityComparer<double>
    {
        public bool Equals(double left, double right)
        {
            return Math.Abs(left - right) <= 1e-5 * Math.Max(1.0, Math.Abs(right));
        }

        public int GetHashCode(double obj)
        {
            return 0;
        }
    }
}
