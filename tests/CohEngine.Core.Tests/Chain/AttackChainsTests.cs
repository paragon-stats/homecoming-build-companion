using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Maths;
using CohEngine.TestSupport;

using PowerStats = CohEngine.Core.Totals.PowerStatistics.PowerStats;

namespace CohEngine.Core.Tests.Chain;

public sealed class AttackChainsTests
{
    private static readonly EnduranceBudget Roomy = new(100.0, 4.0, 1.0);
    private static readonly Movement Walk = new(20.0, 40.0);

    private static ChainPlan Plan(IEnumerable<ChainAttack> attacks, Stance stance = Stance.Melee, Movement? movement = null, EnduranceBudget? budget = null, double timeline = 1.0)
    {
        return AttackChains.Plan(attacks, stance, movement ?? Walk, new Scenario(), budget ?? Roomy, timeline);
    }

    private static PowerStats Stats(string name, double cast, double recharge, double end, bool attack = true)
    {
        return new PowerStats(name, recharge, end, 0.0, 0.0, cast, attack);
    }

    [Fact]
    public void Arcana_cast_time_rounds_up_to_the_tick_and_adds_one()
    {
        double tick = F32.Round(0.132);

        Assert.Equal(F32.Round(F32.Round(8 + 1) * tick), AttackChains.ArcanaCastTime(1.0));
    }

    [Fact]
    public void A_build_with_no_attacks_has_an_empty_gap_free_chain()
    {
        AttackChain chain = AttackChains.Build([Stats("Toggle", 1.0, 5.0, 2.0, attack: false)]);

        Assert.Equal("0 True 0", chain.Rotation.Count + " " + chain.GapFree + " " + chain.EndPerSec);
    }

    [Fact]
    public void The_cycle_stretches_to_the_slowest_attack_and_the_difference_is_the_gap()
    {
        AttackChain chain = AttackChains.Build([Stats("Quick", 1.0, 3.0, 2.0), Stats("Slow", 1.0, 5.0, 2.0)]);

        Assert.Equal("Slow,Quick 2 6 4 False", string.Join(',', chain.Rotation) + " " + chain.CastSum + " " + chain.CycleTime + " " + chain.Gap + " " + chain.GapFree);
    }

    [Fact]
    public void A_chain_whose_casts_cover_every_recharge_is_gap_free()
    {
        AttackChain chain = AttackChains.Build([Stats("A", 3.0, 1.0, 3.0), Stats("B", 3.0, 1.0, 3.0)]);

        Assert.Equal(F32.Round(6.0 / 6.0), chain.EndPerSec);
    }

    [Fact]
    public void A_chain_with_no_cadence_drains_nothing()
    {
        Assert.Equal(0.0, AttackChains.Build([Stats("Idle", 0.0, 0.0, 5.0)]).EndPerSec);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.5)]
    public void A_timeline_share_outside_zero_to_one_is_e45(double timeline)
    {
        Assert.StartsWith("E45: timeline_available is", Assert.Throws<ChainException>(() => Plan([], timeline: timeline)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_attacks_gives_no_options()
    {
        ChainPlan plan = Plan([ChainFactory.Attack("Aura", Role.Aura)]);

        Assert.Equal("0 True", plan.Ranked.Count + " " + (plan.BestAffordable is null && plan.BestOverall is null && plan.BestSustainable is null));
    }

    [Fact]
    public void Every_non_empty_subset_is_an_option()
    {
        Assert.Equal(7, Plan([ChainFactory.Attack("A"), ChainFactory.Attack("B"), ChainFactory.Attack("C")]).Ranked.Count);
    }

    [Fact]
    public void Ranged_stance_leaves_out_attacks_that_need_melee()
    {
        ChainPlan plan = Plan([ChainFactory.Attack("Close", delivery: Delivery.Melee), ChainFactory.Attack("Far")], Stance.Ranged);

        Assert.Equal("Far", Assert.Single(plan.Ranked).Rotation[0]);
    }

    [Fact]
    public void A_hybrid_subset_mixing_positions_pays_two_stance_switches()
    {
        ChainPlan plan = Plan([ChainFactory.Attack("Close", delivery: Delivery.Melee), ChainFactory.Attack("Far")], Stance.Hybrid);

        ChainOption both = plan.Ranked.Single(option => option.Rotation.Count == 2);

        Assert.Equal("2 4", both.Switches + " " + both.MoveTime);
    }

    [Fact]
    public void A_mixed_subset_is_unusable_when_the_character_cannot_move()
    {
        ChainPlan plan = Plan([ChainFactory.Attack("Close", delivery: Delivery.Melee), ChainFactory.Attack("Far")], Stance.Hybrid, new Movement(0.0, 40.0));

        Assert.All(plan.Ranked, option => Assert.Equal(0, option.Switches));
    }

    [Fact]
    public void More_than_the_attack_cap_drops_the_lowest_damage_per_cast()
    {
        ChainAttack[] attacks = [.. Enumerable.Range(0, 13).Select(index => ChainFactory.Attack($"A{index:00}", damage: 100.0 + index))];

        ChainPlan plan = AttackChains.Plan(attacks, Stance.Melee, Walk, new Scenario(), Roomy);

        Assert.Equal("A00", Assert.Single(plan.Dropped));
    }

    [Fact]
    public void An_endurance_hungry_chain_is_not_sustainable_and_has_no_sustainable_pick()
    {
        ChainPlan plan = Plan([ChainFactory.Attack("Hog", end: 50.0)], budget: new EnduranceBudget(100.0, 0.0, 0.0));

        Assert.Null(plan.BestSustainable);
    }

    [Fact]
    public void A_sustainable_option_is_affordable_and_picked()
    {
        ChainPlan plan = Plan([ChainFactory.Attack("Easy", end: 0.1)]);

        Assert.True(plan.BestAffordable!.Sustainable && ReferenceEquals(plan.BestAffordable, plan.BestSustainable));
    }

    [Fact]
    public void Debuffs_add_to_the_ranking_score_at_their_uptime()
    {
        ChainAttack debuffer = ChainFactory.Attack("Debuff") with { TargetResDebuff = 0.5, TargetResDebuffDuration = 1000.0 };

        ChainOption option = Plan([debuffer, ChainFactory.Attack("Plain")]).BestOverall!;

        Assert.Contains("Debuff", string.Join(",", option.Rotation), StringComparison.Ordinal);
    }

    [Fact]
    public void A_smaller_timeline_share_scales_dps_down()
    {
        ChainAttack[] attacks = [ChainFactory.Attack("Hit")];

        Assert.Equal(F32.Round(Plan(attacks).BestOverall!.Dps * 0.5), Plan(attacks, timeline: 0.5).BestOverall!.Dps);
    }

    [Fact]
    public void Options_rank_by_affordability_then_score_gap_size_and_names()
    {
        ChainPlan plan = Plan([ChainFactory.Attack("B"), ChainFactory.Attack("A")]);

        Assert.Equal("A,B", string.Join(',', plan.Ranked[0].Rotation));
    }

    [Fact]
    public void Equal_scores_break_ties_on_the_rotation_names()
    {
        ChainPlan plan = Plan([ChainFactory.Attack("B", damage: 0.0), ChainFactory.Attack("A", damage: 0.0)]);

        Assert.Equal("A", plan.BestOverall!.Rotation[0]);
    }
}
