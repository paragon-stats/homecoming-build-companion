using CohEngine.Core.Chain;
using CohEngine.Core.Damage;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Damage;

// Ported from tests/test_damage_sources.py.
public sealed class DamageSourcesTests
{
    private const double AuraFull = 10.0 * 3 / 2.0;

    private static ChainAttack Attack(string name, Role role = Role.Chain, Delivery delivery = Delivery.Ranged, Shape shape = Shape.SingleTarget, double damage = 100.0, double cast = 1.0, double recharge = 1.0, double end = 5.0, double period = 0.0, double debuff = 0.0, double buff = 0.0, double buffDuration = 0.0)
    {
        AttackProfile profile = new(name, delivery, shape, role, 80.0, 0.0, 0.0, 0, [], []);
        return new ChainAttack { FullName = name, Damage = damage, CastTime = cast, RechargeTime = recharge, EndCost = end, Profile = profile, ActivatePeriod = period, TargetResDebuff = debuff, SelfDamageBuff = buff, SelfDamageBuffDuration = buffDuration };
    }

    private static DamageContext Context(Stance stance = Stance.Melee, IReadOnlyDictionary<string, double>? assumptions = null, double timeline = 1.0, string form = "", Scenario? scenario = null, EnduranceBudget? budget = null, params ChainAttack[] attacks)
    {
        return new DamageContext
        {
            Attacks = attacks,
            Budget = budget ?? new EnduranceBudget(100.0, 10.0, 0.0),
            Stance = stance,
            Scenario = scenario ?? new Scenario(3),
            Movement = new Movement(20.0, 40.0),
            ArchetypeClass = "Class_Test",
            Assumptions = assumptions ?? new Dictionary<string, double>(StringComparer.Ordinal),
            TimelineAvailable = timeline,
            Form = form,
        };
    }

    private static DamageContext Of(Stance stance = Stance.Melee, params ChainAttack[] attacks)
    {
        return Context(stance, attacks: attacks);
    }

    private static ChainAttack Aura(double period = 2.0)
    {
        return Attack("Aura", Role.Aura, Delivery.Melee, Shape.Pbaoe, damage: 10.0, period: period);
    }

    private static ChainAttack MeleeHit(string name = "M")
    {
        return Attack(name, delivery: Delivery.Melee, recharge: 10.0);
    }

    private static ChainAttack RangedHit(string name = "R")
    {
        return Attack(name, recharge: 10.0);
    }

    private static ChainAttack Patch(double damage = 50.0, string name = "Patch")
    {
        return Attack(name, Role.Patch, Delivery.Ranged, Shape.Location, damage, 1.0, 10.0, 12.0);
    }

    private static ChainAttack Form(string name, double damage, params string[] modes)
    {
        ChainAttack attack = Attack(name, damage: damage);
        return attack with { Profile = attack.Profile with { ModesRequired = modes } };
    }

    [Fact]
    public void Every_source_returns_none_when_its_role_is_absent()
    {
        DamageContext context = Of(attacks: Attack("Bolt", Role.PetSummon));

        Assert.True(DamageSources.ChainSource(context) is null && DamageSources.ChosenChain(context) is null && DamageSources.AuraSource(context) is null && DamageSources.PatchSource(context) is null && DamageSources.BuffCastSource(context) is null);
    }

    [Fact]
    public void The_chain_reports_the_planners_rotation_per_attack_dps_and_a_sustainable_label()
    {
        DamageContext context = Context(timeline: 0.5, attacks: [Attack("A", damage: 100.0), Attack("B", damage: 60.0, recharge: 3.0)]);
        ChainOption option = DamageSources.ChosenChain(context)!;

        SourceResult result = DamageSources.ChainSource(context)!;

        Assert.True(option.Sustainable);
        Assert.Equal("chain", result.Source);
        Assert.Equal((option.Dps, option.EndPerSec), (result.Dps, result.EndPerSec));
        Assert.Equal(string.Join(',', option.Rotation), string.Join(',', result.Detail.Select(item => item.Name)));
        Near.Rel(100.0 / option.CycleTime * 0.5, result.Detail.Single(item => string.Equals(item.Name, "A", StringComparison.Ordinal)).Dps, 1e-5);
        Near.Rel(option.Dps, result.Detail.Sum(item => item.Dps), 1e-5);
        Assert.Contains("sustainable", result.Label, StringComparison.Ordinal);
        Assert.DoesNotContain("not sustainable", result.Label, StringComparison.Ordinal);
        Assert.Contains("#31", result.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void When_no_option_lasts_its_minimum_cycles_the_pick_is_the_best_overall()
    {
        DamageContext context = Context(budget: new EnduranceBudget(100.0, 1.0, 0.0), attacks: Attack("Heavy", end: 500.0));

        Assert.False(DamageSources.ChosenChain(context)!.Sustainable);
        Assert.Contains("not affordable (best overall)", DamageSources.ChainSource(context)!.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void A_draining_pick_that_lasts_its_minimum_cycles_is_labelled_affordable_with_its_cycle_count()
    {
        DamageContext context = Context(budget: new EnduranceBudget(100.0, 2.0, 0.0), attacks: Attack("A"));

        string label = DamageSources.ChainSource(context)!.Label;

        Assert.Contains("affordable: empties in", label, StringComparison.Ordinal);
        Assert.Contains("cycles", label, StringComparison.Ordinal);
    }

    [Fact]
    public void Melee_counts_the_aura_fully_and_ranged_drops_it()
    {
        SourceResult full = DamageSources.AuraSource(Of(attacks: Aura()))!;

        Near.Rel(AuraFull, full.Dps);
        Assert.Equal((0.0, 0.0), (full.EndPerSec, full.TimelineShare));
        Assert.Equal(("Aura", full.Dps), Assert.Single(full.Detail));
        Assert.Null(DamageSources.AuraSource(Of(Stance.Ranged, Aura())));
    }

    [Fact]
    public void A_hybrid_aura_share_is_zero_for_an_all_ranged_rotation()
    {
        Assert.Null(DamageSources.AuraSource(Of(Stance.Hybrid, Aura(), RangedHit())));
    }

    [Fact]
    public void A_hybrid_aura_share_is_zero_without_any_chain_attack()
    {
        Assert.Null(DamageSources.AuraSource(Of(Stance.Hybrid, Aura())));
    }

    [Fact]
    public void A_hybrid_aura_share_is_one_for_an_all_melee_rotation()
    {
        Near.Rel(AuraFull, DamageSources.AuraSource(Of(Stance.Hybrid, Aura(), MeleeHit()))!.Dps);
    }

    [Fact]
    public void A_hybrid_aura_share_of_a_mixed_rotation_is_melee_time_over_cycle()
    {
        DamageContext context = Of(Stance.Hybrid, Aura(), MeleeHit(), RangedHit());
        ChainOption chain = DamageSources.ChosenChain(context)!;
        double cast = AttackChains.ArcanaCastTime(1.0);
        double expected = (cast + (chain.MoveTime / 2.0)) / chain.CycleTime;

        Assert.Equal("M,R", string.Join(',', chain.Rotation));
        Assert.InRange(expected, 0.0, 1.0);
        Near.Rel(AuraFull * expected, DamageSources.AuraSource(context)!.Dps, 1e-4);
    }

    [Fact]
    public void The_hybrid_melee_share_assumption_overrides_the_rotation()
    {
        DamageContext context = Context(Stance.Hybrid, new Dictionary<string, double>(StringComparer.Ordinal) { ["hybrid.melee_share"] = 0.25 }, attacks: [Aura(), RangedHit()]);

        Near.Rel(AuraFull * 0.25, DamageSources.AuraSource(context)!.Dps);
    }

    [Fact]
    public void A_zero_tick_period_is_e55_and_a_melee_share_outside_zero_to_one_is_e56()
    {
        DamageContext bad = Context(Stance.Hybrid, new Dictionary<string, double>(StringComparer.Ordinal) { ["hybrid.melee_share"] = 1.5 }, attacks: Aura());

        Assert.StartsWith("E55", Assert.Throws<EngineException>(() => DamageSources.AuraSource(Of(attacks: Aura(0.0)))).Message, StringComparison.Ordinal);
        Assert.StartsWith("E56", Assert.Throws<EngineException>(() => DamageSources.AuraSource(bad)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_patch_casts_on_cooldown_with_dps_over_the_full_cycle()
    {
        SourceResult result = DamageSources.PatchSource(Of(Stance.Ranged, Patch()) with { Scenario = new Scenario(3) })!;
        double cast = AttackChains.ArcanaCastTime(1.0);
        double cycle = cast + 10.0;

        Assert.Equal(("patch", "Patch"), (result.Source, result.Detail[0].Name));
        Near.Rel(50.0 * 3 / cycle, result.Dps, 1e-5);
        Near.Rel(cast / cycle, result.TimelineShare, 1e-5);
        Near.Rel(12.0 / cycle, result.EndPerSec, 1e-5);
    }

    [Fact]
    public void A_patch_without_damage_is_not_charged_timeline_or_endurance()
    {
        ChainAttack decoy = Patch(0.0, "Decoy");
        ChainAttack real = Patch();

        SourceResult both = DamageSources.PatchSource(Of(attacks: [decoy, real]))!;
        SourceResult alone = DamageSources.PatchSource(Of(attacks: real))!;

        Assert.Null(DamageSources.PatchSource(Of(attacks: decoy)));
        Assert.Equal("Patch", Assert.Single(both.Detail).Name);
        Assert.Equal((alone.Dps, alone.TimelineShare, alone.EndPerSec), (both.Dps, both.TimelineShare, both.EndPerSec));
    }

    [Fact]
    public void A_buff_click_deals_no_damage_but_takes_its_cast_share_and_endurance()
    {
        SourceResult result = DamageSources.BuffCastSource(Of(attacks: Attack("Buff", Role.BuffClick, end: 8.0, cast: 1.0, recharge: 10.0)))!;
        double cast = AttackChains.ArcanaCastTime(1.0);

        Assert.Equal(("buff_cast", 0.0), (result.Source, result.Dps));
        Near.Rel(cast / (cast + 10.0), result.TimelineShare, 1e-5);
        Near.Rel(8.0 / (cast + 10.0), result.EndPerSec, 1e-5);
    }

    [Fact]
    public void A_support_power_takes_the_same_cooldown_cast_share_in_the_one_buff_cast_source()
    {
        double cast = AttackChains.ArcanaCastTime(1.5);
        double fastCast = AttackChains.ArcanaCastTime(1.0);

        SourceResult result = DamageSources.BuffCastSource(Of(attacks: [Attack("Melt", Role.Support, cast: 1.5, recharge: 100.0, end: 10.0), Attack("Fury", Role.BuffClick, cast: 1.0, recharge: 10.0, end: 8.0)]))!;

        Assert.Equal("buff_cast", result.Source);
        Near.Rel((cast / (cast + 100.0)) + (fastCast / (fastCast + 10.0)), result.TimelineShare, 1e-5);
        Near.Rel((10.0 / (cast + 100.0)) + (8.0 / (fastCast + 10.0)), result.EndPerSec, 1e-5);
    }

    [Fact]
    public void The_form_filter_keeps_mode_free_attacks_in_the_base_form_and_the_forms_attacks_in_a_form()
    {
        ChainAttack human = Form("Blast", 100.0);
        ChainAttack nova = Form("Nova Bolt", 100.0, "Nova");

        Assert.Equal("Blast|Nova Bolt", string.Join('|', DamageSources.AttacksForForm(Of(attacks: [human, nova])).Select(attack => attack.FullName)) + "|" + string.Join(',', DamageSources.AttacksForForm(Context(form: "Nova", attacks: [human, nova])).Select(attack => attack.FullName)));
    }

    [Fact]
    public void Each_forms_chain_picks_only_its_own_attacks_though_the_other_forms_hit_harder()
    {
        DamageContext context = Of(attacks: [Form("Blast", 100.0), Form("Nova Bolt", 500.0, "Nova")]);
        DamageContext nova = context with { Form = "Nova" };

        Assert.Equal("Blast Nova Bolt True", $"{string.Join(',', DamageSources.ChosenChain(context)!.Rotation)} {string.Join(',', DamageSources.ChosenChain(nova)!.Rotation)} {DamageSources.ChainSource(nova) is not null}");
    }

    [Fact]
    public void The_melee_share_is_the_public_helper_other_layers_reuse()
    {
        Assert.Equal(1.0, DamageSources.MeleeShare(Of()));
    }

    [Fact]
    public void The_end_to_end_fold_gives_the_chain_the_timeline_left_by_patch_and_buff()
    {
        ChainAttack chain = Attack("Smite", damage: 120.0, debuff: 0.2) with { TargetResDebuffDuration = 4.0 };
        ChainAttack buff = Attack("Fury", Role.BuffClick, cast: 1.0, recharge: 10.0, end: 8.0, buff: 0.5, buffDuration: 5.5);
        DamageContext context = Context(scenario: new Scenario(1), attacks: [chain, Patch(), buff]);
        SourceRegistration[] sources =
        [
            new(DamageSources.ChainSource, FillsRemainder: true),
            new(DamageSources.AuraSource, FillsRemainder: false),
            new(DamageSources.PatchSource, FillsRemainder: false),
            new(DamageSources.BuffCastSource, FillsRemainder: false),
            new(_ => new SourceResult { Source = "pet", Dps = 100.0 }, FillsRemainder: false),
        ];

        DamageReport report = DamageModel.Evaluate(context, sources, [DamageModifiers.ClickBuffModifier, DamageModifiers.TargetDebuffModifier]);
        double cast = AttackChains.ArcanaCastTime(1.0);
        double cycle = cast + 10.0;
        double share = 2 * cast / cycle;
        double chainDps = 120.0 / Math.Max(cast, cast + 1.0) * (1.0 - share);
        double click = 1.0 + (0.5 * (5.5 / cycle));
        double Scaled(string name) => report.ScaledDps.Single(pair => string.Equals(pair.Key, name, StringComparison.Ordinal)).Value;

        Near.Rel(chainDps, report.Sources.Single(source => string.Equals(source.Source, "chain", StringComparison.Ordinal)).Dps, 1e-4);
        Near.Rel(chainDps * click * 1.2, Scaled("chain"), 1e-4);
        Near.Rel(50.0 / cycle * click * 1.2, Scaled("patch"), 1e-4);
        Near.Rel(100.0 * 1.2, Scaled("pet"), 1e-4);
        Assert.Equal(0.0, Scaled("buff_cast"));
        Near.Rel(report.Sources.Single(source => string.Equals(source.Source, "chain", StringComparison.Ordinal)).EndPerSec + ((12.0 + 8.0) / cycle), report.TotalDrainPerSec, 1e-4);
    }
}
