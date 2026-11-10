using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.DamageLayers;

// Ported from tests/test_damage_modifiers.py.
public sealed class DamageModifiersTests
{
    private static readonly double Cycle = AttackChains.ArcanaCastTime(1.0) + 9.0;

    public static TheoryData<double> BadResistances => [-1.5, 0.99];

    public static TheoryData<Stance> Stances => [Stance.Melee, Stance.Ranged, Stance.Hybrid];

    private static ChainAttack Attack(string name, Role role, double buff = 0.0, double buffDuration = 0.0, double debuff = 0.0, double debuffDuration = 0.0)
    {
        AttackProfile profile = new(name, Delivery.Ranged, Shape.SingleTarget, role, 80.0, 0.0, 0.0, 0, [], []);
        return new ChainAttack { FullName = name, Damage = 100.0, CastTime = 1.0, RechargeTime = 9.0, EndCost = 5.0, Profile = profile, SelfDamageBuff = buff, SelfDamageBuffDuration = buffDuration, TargetResDebuff = debuff, TargetResDebuffDuration = debuffDuration };
    }

    private static ChainAttack Pbaoe(string name, Role role, double debuff)
    {
        AttackProfile profile = new(name, Delivery.Ranged, Shape.Pbaoe, role, 0.0, 15.0, 0.0, 16, [], []);
        return new ChainAttack { FullName = name, Damage = 0.0, CastTime = 1.0, RechargeTime = 1.0, EndCost = 0.0, Profile = profile, TargetResDebuff = debuff };
    }

    private static ChainAttack Melt()
    {
        return Attack("Melt", Role.Support, debuff: 0.3, debuffDuration: 40.0) with { Damage = 0.0, CastTime = 1.5, RechargeTime = 100.0, EndCost = 10.0 };
    }

    private static DamageContext Context(Stance stance = Stance.Melee, IReadOnlyDictionary<string, double>? assumptions = null, double timeline = 1.0, params ChainAttack[] attacks)
    {
        return new DamageContext
        {
            Attacks = attacks,
            Budget = new EnduranceBudget(100.0, 10.0, 0.0),
            Stance = stance,
            Scenario = new Scenario(),
            Movement = new Movement(20.0, 40.0),
            ArchetypeClass = "Class_Test",
            Assumptions = assumptions ?? new Dictionary<string, double>(StringComparer.Ordinal),
            TimelineAvailable = timeline,
        };
    }

    private static DamageContext Of(params ChainAttack[] attacks)
    {
        return Context(attacks: attacks);
    }

    private static Dictionary<string, double> Assume(string key, double value)
    {
        return new Dictionary<string, double>(StringComparer.Ordinal) { [key] = value };
    }

    [Fact]
    public void Zero_strength_riders_make_no_modifier()
    {
        DamageContext context = Of(Attack("Plain", Role.Chain), Attack("Dud", Role.BuffClick));

        Assert.True(DamageModifiers.ClickBuffModifier(context) is null && DamageModifiers.TargetDebuffModifier(context) is null);
    }

    [Fact]
    public void Click_buff_uptime_is_duration_over_cycle_and_the_buff_spares_pets()
    {
        ModifierResult result = DamageModifiers.ClickBuffModifier(Of(Attack("Full", Role.BuffClick, 0.25, 100.0), Attack("Part", Role.BuffClick, 0.5, Cycle / 2)))!;

        Assert.Equal("True 1 aura,chain,patch", $"{result.Additive} {result.Uptime} {string.Join(',', result.AppliesTo!.Order(StringComparer.Ordinal))}");
        Near.Rel(1.0 + 0.25 + 0.25, result.Multiplier, 1e-5);
        Assert.Contains("Full +25% at 100% uptime", result.Assumption, StringComparison.Ordinal);
        Assert.Contains("Part +50% at 50% uptime", result.Assumption, StringComparison.Ordinal);
    }

    [Fact]
    public void The_debuff_factor_is_one_minus_r_plus_d_over_one_minus_r()
    {
        ChainAttack chain = Attack("Chain", Role.Chain, debuff: 0.2, debuffDuration: 100.0);
        ChainAttack patch = Attack("Patch", Role.Patch, debuff: 0.1, debuffDuration: Cycle / 2);
        ChainAttack ignored = Attack("Click", Role.BuffClick, debuff: 0.5, debuffDuration: 100.0);
        double stacked = 0.2 + (0.1 * 0.5);

        ModifierResult baseline = DamageModifiers.TargetDebuffModifier(Of(chain, patch, ignored))!;
        ModifierResult tough = DamageModifiers.TargetDebuffModifier(Context(assumptions: Assume("target.resistance", 0.5), attacks: [chain, patch]))!;

        Assert.True(!baseline.Additive && baseline.AppliesTo is null);
        Near.Rel(1.0 + stacked, baseline.Multiplier, 1e-5);
        Assert.Contains("Chain -20% res", baseline.Assumption, StringComparison.Ordinal);
        Near.Rel((0.5 + stacked) / 0.5, tough.Multiplier, 1e-5);
    }

    [Fact]
    public void A_debuff_from_an_attack_outside_the_chosen_chain_does_not_count()
    {
        ChainAttack hitter = Attack("Hitter", Role.Chain) with { RechargeTime = 1.0, EndCost = 1.0 };
        ChainAttack slow = Attack("Slow", Role.Chain, debuff: 0.2, debuffDuration: 10.0) with { Damage = 1.0, RechargeTime = 60.0, EndCost = 1.0 };

        Assert.Null(DamageModifiers.TargetDebuffModifier(Of(hitter, slow)));
    }

    [Fact]
    public void A_chain_debuff_is_up_duration_over_the_chosen_cycle()
    {
        ModifierResult result = DamageModifiers.TargetDebuffModifier(Of(Attack("Chain", Role.Chain, debuff: 0.2, debuffDuration: 4.0)))!;

        Near.Rel(1.0 + (0.2 * 4.0 / Cycle), result.Multiplier, 1e-5);
    }

    [Theory]
    [MemberData(nameof(BadResistances))]
    public void A_target_resistance_out_of_range_is_e57(double bad)
    {
        DamageContext context = Context(assumptions: Assume("target.resistance", bad), attacks: [Attack("Chain", Role.Chain, debuff: 0.2, debuffDuration: 10.0)]);

        Assert.StartsWith(ErrorCodes.E57, Assert.Throws<EngineException>(() => DamageModifiers.TargetDebuffModifier(context)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_chain_part_is_the_chosen_options_own_res_debuff_under_a_reduced_timeline()
    {
        DamageContext context = Context(timeline: 0.5, attacks: [Attack("Chain", Role.Chain, debuff: 0.4, debuffDuration: Cycle)]);
        ChainOption option = DamageSources.ChosenChain(context)!;
        ModifierResult result = DamageModifiers.TargetDebuffModifier(context)!;

        Near.Rel(0.5 * 0.4, option.ResDebuff, 1e-5);
        Near.Rel(1.0 + option.ResDebuff, result.Multiplier, 1e-5);
        Assert.Contains("Chain", result.Assumption, StringComparison.Ordinal);
    }

    [Fact]
    public void A_support_debuff_is_up_duration_over_arcana_cast_plus_recharge()
    {
        double uptime = 40.0 / (AttackChains.ArcanaCastTime(1.5) + 100.0);

        ModifierResult result = DamageModifiers.TargetDebuffModifier(Of(Melt()))!;

        Near.Rel(1.0 + (0.3 * uptime), result.Multiplier, 1e-5);
        Assert.Contains($"Melt -30% res (support) at {PyFloat.Percent(uptime, 0)} uptime", result.Assumption, StringComparison.Ordinal);
    }

    [Fact]
    public void A_support_self_damage_buff_is_an_additive_click_buff_at_cooldown_uptime()
    {
        ModifierResult result = DamageModifiers.ClickBuffModifier(Of(Attack("Siphon", Role.Support, 0.2, 5.0)))!;

        Assert.True(result.Additive && result.Assumption.Contains("Siphon", StringComparison.Ordinal));
        Near.Rel(1.0 + (0.2 * 5.0 / Cycle), result.Multiplier, 1e-5);
    }

    [Theory]
    [MemberData(nameof(Stances))]
    public void An_anchored_persistent_debuff_is_always_up(Stance stance)
    {
        ModifierResult result = DamageModifiers.TargetDebuffModifier(Context(stance, attacks: [Attack("Field", Role.PersistentDebuff, debuff: 0.15)]))!;

        Near.Rel(1.15, result.Multiplier, 1e-5);
        Assert.Contains("Field -15% res (persistent_debuff) at 100% uptime", result.Assumption, StringComparison.Ordinal);
    }

    [Fact]
    public void A_self_centred_persistent_debuff_follows_the_stance()
    {
        ChainAttack gas = Pbaoe("Gas", Role.PersistentDebuff, 0.2);
        DamageContext hybridContext = Context(Stance.Hybrid, Assume("hybrid.melee_share", 0.25), attacks: [gas]);

        ModifierResult melee = DamageModifiers.TargetDebuffModifier(Context(Stance.Melee, attacks: [gas]))!;
        ModifierResult hybrid = DamageModifiers.TargetDebuffModifier(hybridContext)!;

        Near.Rel(1.2, melee.Multiplier, 1e-5);
        Assert.Null(DamageModifiers.TargetDebuffModifier(Context(Stance.Ranged, attacks: [gas])));
        Assert.Equal(0.25, DamageSources.MeleeShare(hybridContext));
        Near.Rel(1.0 + (0.2 * 0.25), hybrid.Multiplier, 1e-5);
        Assert.Contains("Gas -20% res (persistent_debuff) at 25% uptime", hybrid.Assumption, StringComparison.Ordinal);
    }

    [Fact]
    public void A_hybrid_persistent_debuff_uses_the_derived_melee_share()
    {
        DamageContext context = Context(Stance.Hybrid, attacks: [Pbaoe("Gas", Role.PersistentDebuff, 0.2), Attack("Far", Role.Chain)]);

        Assert.Equal("0 True", $"{DamageSources.MeleeShare(context)} {DamageModifiers.TargetDebuffModifier(context) is null}");
    }

    [Fact]
    public void The_support_cast_shrinks_the_chains_timeline_and_the_stacked_debuffs_scale_the_chain()
    {
        DamageContext context = Of(Attack("Hit", Role.Chain), Melt(), Attack("Field", Role.PersistentDebuff, debuff: 0.15));
        SourceRegistration[] sources = [new(DamageSources.ChainSource, FillsRemainder: true), new(DamageSources.BuffCastSource, FillsRemainder: false)];

        DamageReport report = DamageModel.Evaluate(context, sources, [DamageModifiers.TargetDebuffModifier]);
        double cast = AttackChains.ArcanaCastTime(1.5);
        double share = cast / (cast + 100.0);
        double chainDps = report.Sources.First(source => string.Equals(source.Source, "chain", StringComparison.Ordinal)).Dps;
        double factor = 1.0 + (0.3 * 40.0 / (cast + 100.0)) + 0.15;

        Near.Rel(1.0 - share, report.RemainderContext.TimelineAvailable, 1e-5);
        Near.Rel(100.0 / Cycle * (1.0 - share), chainDps, 1e-4);
        Near.Rel(chainDps * factor, report.ScaledDps.First(pair => string.Equals(pair.Key, "chain", StringComparison.Ordinal)).Value, 1e-4);
    }
}
