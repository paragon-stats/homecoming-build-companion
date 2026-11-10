using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.DamageLayers;

public sealed class DamageSourcesTests
{
    private static readonly IReadOnlyDictionary<string, double> HybridShare = new Dictionary<string, double>(StringComparer.Ordinal) { ["hybrid.melee_share"] = 0.4 };

    public static TheoryData<string, double> BudgetLabels => new()
    {
        { "sustainable", 100.0 },
        { "affordable: empties in", 0.0 },
    };

    private static ChainAttack Melee(string name)
    {
        return ChainFactory.Attack(name, delivery: Delivery.Melee);
    }

    private static DamageContext Hybrid(params ChainAttack[] attacks)
    {
        return ChainFactory.Context(attacks, Stance.Hybrid);
    }

    [Fact]
    public void The_base_form_uses_attacks_with_no_required_mode()
    {
        ChainAttack nova = ChainFactory.Attack("Nova") with { Profile = ChainFactory.Profile(modes: ["Nova"]) };

        Assert.Equal("Plain", string.Join(',', DamageSources.AttacksForForm(ChainFactory.Context([ChainFactory.Attack("Plain"), nova])).Select(attack => attack.FullName)));
    }

    [Fact]
    public void A_form_uses_the_attacks_that_require_it()
    {
        ChainAttack nova = ChainFactory.Attack("Nova") with { Profile = ChainFactory.Profile(modes: ["Nova"]) };

        Assert.Equal("Nova", string.Join(',', DamageSources.AttacksForForm(ChainFactory.Context([ChainFactory.Attack("Plain"), nova]) with { Form = "Nova" }).Select(attack => attack.FullName)));
    }

    [Fact]
    public void The_chosen_chain_is_null_without_a_usable_attack()
    {
        Assert.Null(DamageSources.ChosenChain(ChainFactory.Context([ChainFactory.Attack("Aura", Role.Aura)])));
    }

    [Fact]
    public void The_chain_source_is_null_without_a_usable_attack()
    {
        Assert.Null(DamageSources.ChainSource(ChainFactory.Context([])));
    }

    [Theory]
    [MemberData(nameof(BudgetLabels))]
    public void The_chain_label_says_how_affordable_the_rotation_is(string expected, double recovery)
    {
        DamageContext context = ChainFactory.Context([ChainFactory.Attack("Hit")]) with { Budget = new EnduranceBudget(100.0, recovery, 0.0) };

        Assert.Contains(expected, DamageSources.ChainSource(context)!.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void A_chain_that_empties_before_the_minimum_cycles_is_not_affordable()
    {
        DamageContext context = ChainFactory.Context([ChainFactory.Attack("Hit")]) with { Budget = new EnduranceBudget(10.0, 0.0, 0.0) };

        Assert.Contains("not affordable (best overall): empties in 15.5 s, 2.5 cycles", DamageSources.ChainSource(context)!.Label, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Stance.Melee, 1.0)]
    [InlineData(Stance.Ranged, 0.0)]
    public void Melee_and_ranged_stances_fix_the_melee_share(Stance stance, double expected)
    {
        Assert.Equal(expected, DamageSources.MeleeShare(ChainFactory.Context([], stance)));
    }

    [Fact]
    public void A_hybrid_share_assumption_overrides_the_rotation()
    {
        Assert.Equal(0.4, DamageSources.MeleeShare(ChainFactory.Context([], Stance.Hybrid, assumptions: HybridShare)));
    }

    [Fact]
    public void A_hybrid_share_outside_zero_to_one_is_e56()
    {
        DamageContext context = ChainFactory.Context([], Stance.Hybrid, assumptions: new Dictionary<string, double>(StringComparer.Ordinal) { ["hybrid.melee_share"] = 1.5 });

        Assert.Equal("E56: assumption hybrid.melee_share=1.5 is outside [0, 1]; set it within that range", Assert.Throws<EngineException>(() => DamageSources.MeleeShare(context)).Message);
    }

    [Fact]
    public void A_hybrid_stance_with_no_chain_stands_nowhere()
    {
        Assert.Equal(0.0, DamageSources.MeleeShare(Hybrid()));
    }

    [Fact]
    public void A_hybrid_rotation_with_no_melee_attack_has_no_melee_share()
    {
        Assert.Equal(0.0, DamageSources.MeleeShare(Hybrid(ChainFactory.Attack("Ranged"))));
    }

    [Fact]
    public void A_hybrid_rotation_of_only_melee_attacks_is_all_melee()
    {
        Assert.Equal(1.0, DamageSources.MeleeShare(Hybrid(Melee("Close"))));
    }

    [Fact]
    public void A_mixed_hybrid_rotation_splits_the_cycle()
    {
        double share = DamageSources.MeleeShare(Hybrid(Melee("Close"), ChainFactory.Attack("Far")));

        Assert.InRange(share, 0.01, 0.99);
    }

    [Fact]
    public void Auras_are_absent_without_an_aura_power()
    {
        Assert.Null(DamageSources.AuraSource(ChainFactory.Context([ChainFactory.Attack("Hit")])));
    }

    [Fact]
    public void Auras_are_absent_while_standing_at_range()
    {
        Assert.Null(DamageSources.AuraSource(ChainFactory.Context([ChainFactory.Attack("Aura", Role.Aura) with { ActivatePeriod = 2.0 }], Stance.Ranged)));
    }

    [Fact]
    public void An_aura_without_a_tick_period_is_e55()
    {
        Assert.StartsWith("E55: aura Aura has activate_period 0.0 but must be > 0", Assert.Throws<EngineException>(() => DamageSources.AuraSource(ChainFactory.Context([ChainFactory.Attack("Aura", Role.Aura)]))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void An_aura_deals_damage_per_tick_over_the_period()
    {
        SourceResult aura = DamageSources.AuraSource(ChainFactory.Context([ChainFactory.Attack("Aura", Role.Aura, damage: 10.0) with { ActivatePeriod = 2.0 }]))!;

        Assert.Equal("5 damage auras at melee share 1.00; per-tick damage assumed", aura.Dps + " " + aura.Label);
    }

    [Fact]
    public void Patches_without_damage_are_absent()
    {
        Assert.Null(DamageSources.PatchSource(ChainFactory.Context([ChainFactory.Attack("Decoy", Role.Patch, damage: 0.0)])));
    }

    [Fact]
    public void A_patch_casts_on_cooldown_and_claims_its_cast_share()
    {
        SourceResult patch = DamageSources.PatchSource(ChainFactory.Context([ChainFactory.Attack("Patch", Role.Patch, damage: 60.0, cast: 1.0, recharge: 10.0, end: 5.0)]))!;

        Assert.True(patch.Dps > 0.0 && patch.TimelineShare > 0.0 && patch.EndPerSec > 0.0);
    }

    [Fact]
    public void Buff_casts_are_absent_without_buff_clicks()
    {
        Assert.Null(DamageSources.BuffCastSource(ChainFactory.Context([ChainFactory.Attack("Hit")])));
    }

    [Fact]
    public void Buff_clicks_and_support_powers_spend_time_and_endurance_for_no_damage()
    {
        SourceResult cast = DamageSources.BuffCastSource(ChainFactory.Context([ChainFactory.Attack("Buff", Role.BuffClick), ChainFactory.Attack("Aid", Role.Support)]))!;

        Assert.True(cast.Dps == 0.0 && cast.TimelineShare > 0.0 && cast.EndPerSec > 0.0);
    }
}
