using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Maths;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.DamageLayers;

// Ported from tests/test_damage_model.py. The decorator registries become explicit lists, so the registry test is the
// default-list test at the end.
public sealed class DamageModelTests
{
    private static readonly Func<DamageContext, SourceResult?> Chain = _ => new SourceResult { Source = "chain", Dps = 100.0 };

    public static TheoryData<int, int, Shape, int> TargetCases => new()
    {
        { 10, 0, Shape.SingleTarget, 1 },
        { 10, 5, Shape.Cone, 5 },
        { 10, 0, Shape.Pbaoe, 10 },
        { 5, 3, Shape.Cone, 3 },
        { 2, 8, Shape.Pbaoe, 2 },
        { 0, 0, Shape.Location, 1 },
    };

    private static AttackProfile Profile(Shape shape = Shape.SingleTarget, Delivery delivery = Delivery.Ranged, int targets = 0)
    {
        return new AttackProfile("X", delivery, shape, Role.Chain, 80.0, 0.0, 0.0, targets, [], []);
    }

    private static DamageContext Context(double baseline = 1.0, params ChainAttack[] attacks)
    {
        return new DamageContext
        {
            Attacks = attacks,
            Budget = new EnduranceBudget(100.0, 2.0, 0.5),
            Stance = Stance.Melee,
            Scenario = new Scenario(),
            Movement = new Movement(20.0, 40.0),
            ArchetypeClass = "Class_Test",
            DamageBuffBaseline = baseline,
        };
    }

    private static SourceRegistration Source(string name, double dps, double share = 0.0, double end = 0.0, bool remainder = false, double? baseline = null)
    {
        return new SourceRegistration(_ => new SourceResult { Source = name, Dps = dps, TimelineShare = share, EndPerSec = end, DamageBuffBaseline = baseline }, remainder);
    }

    private static Func<DamageContext, ModifierResult?> Modifier(string name, double multiplier, double uptime = 1.0, bool additive = false, string[]? appliesTo = null)
    {
        return _ => new ModifierResult { Modifier = name, Multiplier = multiplier, Uptime = uptime, Additive = additive, AppliesTo = appliesTo is null ? null : new HashSet<string>(appliesTo, StringComparer.Ordinal) };
    }

    private static double Scaled(DamageReport report, string source)
    {
        return report.ScaledDps.First(pair => string.Equals(pair.Key, source, StringComparison.Ordinal)).Value;
    }

    [Fact]
    public void Melee_delivery_and_self_centred_areas_need_the_melee_position_and_ranged_cones_do_not()
    {
        Assert.Equal("True True False", $"{Profile(delivery: Delivery.Melee).NeedsMeleePosition} {Profile(Shape.Pbaoe).NeedsMeleePosition} {Profile(Shape.Cone).NeedsMeleePosition}");
    }

    [Fact]
    public void Net_is_recovery_less_toggles_less_extra_and_empty_time_is_max_over_the_deficit()
    {
        EnduranceBudget budget = new(100.0, 2.0, 0.5);

        Assert.Equal($"{F32.Round(1.5)} True {F32.Round(100.0)}", $"{budget.NetPerSec()} {double.IsPositiveInfinity(budget.TimeToEmpty())} {budget.TimeToEmpty(2.5)}");
    }

    [Theory]
    [MemberData(nameof(TargetCases))]
    public void Area_powers_hit_up_to_their_cap_and_an_uncapped_area_hits_every_foe(int targets, int cap, Shape shape, int expected)
    {
        Assert.Equal(expected, new Scenario(targets).TargetsHit(Profile(shape, targets: cap)));
    }

    [Fact]
    public void Crossing_the_standoff_takes_distance_over_speed_and_no_speed_means_never()
    {
        Assert.Equal($"{F32.Round(2.0)} True", $"{new Movement(20.0, 40.0).SwitchTime} {double.IsPositiveInfinity(new Movement(0.0, 40.0).SwitchTime)}");
    }

    [Fact]
    public void By_role_keeps_input_order_and_drops_other_roles()
    {
        ChainAttack aura = new() { FullName = "A", Damage = 1.0, CastTime = 0.0, RechargeTime = 0.0, EndCost = 0.0, Profile = Profile() with { Role = Role.Aura } };
        ChainAttack chain = new() { FullName = "C", Damage = 1.0, CastTime = 1.0, RechargeTime = 1.0, EndCost = 1.0, Profile = Profile() };

        Assert.Same(aura, Assert.Single(Context(1.0, chain, aura).ByRole(Role.Aura)));
    }

    [Fact]
    public void The_remainder_source_sees_one_minus_the_others_share_and_their_drain()
    {
        List<(double, double)> seen = [];
        SourceRegistration patch = Source("patch", 10.0, 0.25, 0.5);
        Func<DamageContext, SourceResult?> record = context =>
        {
            seen.Add((context.TimelineAvailable, context.OtherDrainPerSec));
            return new SourceResult { Source = "chain", Dps = 100.0, EndPerSec = 1.0 };
        };
        SourceRegistration chain = new(record, FillsRemainder: true);

        DamageReport report = DamageModel.Evaluate(Context(), [chain, patch, new(_ => null, FillsRemainder: false)], []);

        Assert.Equal($"{F32.Round(0.75)},{F32.Round(0.5)} patch,chain {F32.Round(110.0)} {F32.Round(1.5)} True {F32.Round(0.75)} {F32.Round(0.5)}", $"{seen[0].Item1},{seen[0].Item2} {string.Join(',', report.Sources.Select(source => source.Source))} {report.TotalDps} {report.TotalDrainPerSec} {double.IsPositiveInfinity(report.TimeToEmpty)} {report.RemainderContext.TimelineAvailable} {report.RemainderContext.OtherDrainPerSec}");
    }

    [Fact]
    public void A_scoped_modifier_scales_only_its_sources_an_unscoped_one_scales_all_and_none_is_skipped()
    {
        DamageReport report = DamageModel.Evaluate(Context(), [new(Chain, true), Source("pet", 50.0)], [Modifier("debuff", 1.2, 0.5), Modifier("fury", 2.0, appliesTo: ["chain"]), _ => null]);

        Assert.Equal($"{F32.Round(100.0 * 1.1 * 2.0)} {F32.Round(50.0 * 1.1)} debuff,fury", $"{Scaled(report, "chain")} {Scaled(report, "pet")} {string.Join(',', report.Modifiers.Select(modifier => modifier.Modifier))}");
    }

    [Fact]
    public void An_additive_buff_divides_by_the_strength_already_applied_and_a_true_doubling_stays_doubled()
    {
        ModifierResult buff = new() { Modifier = "fury", Multiplier = 2.0, Additive = true };
        ModifierResult doubling = new() { Modifier = "containment", Multiplier = 2.0 };

        Assert.Equal($"{F32.Round(1.5)} {F32.Round(2.0)}", $"{buff.Scale(2.0)} {doubling.Scale(2.0)}");
    }

    [Fact]
    public void Additive_buffs_add_into_one_factor_and_a_multiplicative_modifier_multiplies_it()
    {
        Func<DamageContext, ModifierResult?> fury = Modifier("fury", 2.5, additive: true);
        Func<DamageContext, ModifierResult?> buildUp = Modifier("build_up", 2.0, additive: true);

        DamageReport both = DamageModel.Evaluate(Context(2.5), [new(Chain, true)], [fury, buildUp]);
        DamageReport mixed = DamageModel.Evaluate(Context(2.5), [new(Chain, true)], [fury, buildUp, Modifier("contain", 1.5)]);

        Assert.Equal($"{F32.Round(100.0 * 2.0)} {F32.Round(100.0 * 3.0)}", $"{Scaled(both, "chain")} {Scaled(mixed, "chain")}");
    }

    [Fact]
    public void A_source_with_its_own_baseline_is_scaled_against_it()
    {
        DamageReport report = DamageModel.Evaluate(Context(2.0), [new(Chain, true), Source("pet", 100.0, baseline: 1.0)], [Modifier("buff", 1.5, additive: true)]);

        Assert.Equal($"{F32.Round(125.0)} {F32.Round(150.0)}", $"{Scaled(report, "chain")} {Scaled(report, "pet")}");
    }

    [Fact]
    public void Modifiers_run_on_the_leftover_timeline_and_drain()
    {
        List<(double, double)> seen = [];

        Func<DamageContext, ModifierResult?> probe = context =>
        {
            seen.Add((context.TimelineAvailable, context.OtherDrainPerSec));
            return null;
        };

        DamageModel.Evaluate(Context(), [Source("patch", 1.0, 0.25, 0.5)], [probe]);

        Assert.Equal((F32.Round(0.75), F32.Round(0.5)), Assert.Single(seen));
    }

    [Fact]
    public void Shares_summing_past_one_fail_loud_as_e30()
    {
        Assert.StartsWith("E30", Assert.Throws<ChainException>(() => DamageModel.Evaluate(Context(), [Source("greedy", 1.0, 1.5)], [])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_e30_message_lists_each_claim()
    {
        ChainException error = Assert.Throws<ChainException>(() => DamageModel.Evaluate(Context(), [Source("b", 1.0, 0.75), Source("c", 1.0, 0.75)], []));

        Assert.Equal(
            "E30: non-chain damage sources claim 1.500 of the timeline (> 1.0): [('b', 0.75), ('c', 0.75)]; a summon, patch or click buff reports a cast share above its recharge cadence",
            error.Message);
    }

    [Fact]
    public void A_probe_source_and_modifier_fold_to_nothing()
    {
        DamageReport report = DamageModel.Evaluate(Context(), [new(_ => null, true)], [_ => null]);

        Assert.Empty(report.ScaledDps);
    }

    [Fact]
    public void A_repeated_source_name_overwrites_as_a_python_dict_does()
    {
        DamageReport report = DamageModel.Evaluate(Context(), [Source("a", 1.0), Source("a", 2.0)], []);

        Assert.Equal("a=2", string.Join(',', report.ScaledDps.Select(pair => $"{pair.Key}={pair.Value}")));
    }

    [Fact]
    public void Evaluate_totals_scaled_dps_and_orders_remainder_sources_last()
    {
        DamageReport report = DamageModel.Evaluate(Context(), [Source("r", 20.0, remainder: true), Source("a", 10.0, 0.25, 1.0), new(_ => null, false), new(_ => null, true)], [Modifier("m1", 1.5, additive: true), Modifier("m2", 2.0, appliesTo: ["a"]), _ => null]);

        Assert.Equal("a=30,r=30 60", string.Join(',', report.ScaledDps.Select(pair => $"{pair.Key}={pair.Value}")) + " " + report.TotalDps);
    }

    [Fact]
    public void Time_to_empty_comes_from_the_total_drain()
    {
        Assert.Equal(F32.Round(100.0 / 3.5), DamageModel.Evaluate(Context(), [Source("a", 1.0, end: 5.0)], []).TimeToEmpty);
    }

    [Fact]
    public void The_default_lists_are_used_when_none_are_given()
    {
        ChainAttack attack = new() { FullName = "Hit", Damage = 100.0, CastTime = 1.0, RechargeTime = 5.0, EndCost = 4.0, Profile = Profile() };

        Assert.Equal("pet,chain", string.Join(',', DamageModel.Evaluate(Context(1.0, attack)).Sources.Select(source => source.Source)));
    }

    [Fact]
    public void Enum_values_are_the_python_spellings()
    {
        Assert.Equal(
            "melee ranged hybrid chain aura patch pet_summon buff_click support persistent_debuff melee ranged",
            string.Join(' ', Stance.Melee.Value(), Stance.Ranged.Value(), Stance.Hybrid.Value(), Role.Chain.Value(), Role.Aura.Value(), Role.Patch.Value(), Role.PetSummon.Value(), Role.BuffClick.Value(), Role.Support.Value(), Role.PersistentDebuff.Value(), Delivery.Melee.Value(), Delivery.Ranged.Value()));
    }

    [Theory]
    [InlineData("ranged", true)]
    [InlineData("all", false)]
    public void A_stance_parses_from_its_value(string text, bool expected)
    {
        Assert.Equal(expected, EnumText.TryParseStance(text, out _));
    }

    [Fact]
    public void Assume_reads_the_assumption_or_its_default()
    {
        DamageContext context = Context() with { Assumptions = new Dictionary<string, double>(StringComparer.Ordinal) { ["k"] = 3.0 } };

        Assert.Equal("3 9", $"{context.Assume("k", 9.0)} {context.Assume("none", 9.0)}");
    }
}
