using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Maths;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.DamageLayers;

public sealed class DamageModifiersTests
{
    private static readonly IReadOnlyDictionary<string, double> HighResistance = new Dictionary<string, double>(StringComparer.Ordinal) { ["target.resistance"] = 0.9 };

    private static ChainAttack Debuffing(string name, Role role, double debuff = 0.2, double duration = 100.0)
    {
        return ChainFactory.Attack(name, role) with { TargetResDebuff = debuff, TargetResDebuffDuration = duration };
    }

    [Fact]
    public void Click_buffs_are_absent_without_a_buffing_click()
    {
        Assert.Null(DamageModifiers.ClickBuffModifier(ChainFactory.Context([ChainFactory.Attack("Hit")])));
    }

    [Fact]
    public void A_click_buff_is_additive_and_scales_the_characters_own_damage()
    {
        ChainAttack buff = ChainFactory.Attack("Buff", Role.BuffClick) with { SelfDamageBuff = 0.5, SelfDamageBuffDuration = 5.0 };

        ModifierResult result = DamageModifiers.ClickBuffModifier(ChainFactory.Context([buff]))!;

        Assert.Equal("aura,chain,patch", string.Join(',', result.AppliesTo!.Order(StringComparer.Ordinal)));
    }

    [Fact]
    public void A_click_buff_names_its_uptime_in_the_assumption()
    {
        ChainAttack buff = ChainFactory.Attack("Buff", Role.BuffClick) with { SelfDamageBuff = 0.5, SelfDamageBuffDuration = 5.0 };

        Assert.Equal("click buffs fired on cooldown: Buff +50% at 81% uptime", DamageModifiers.ClickBuffModifier(ChainFactory.Context([buff]))!.Assumption);
    }

    [Fact]
    public void A_click_buffs_multiplier_adds_its_uptime_weighted_bonus()
    {
        ChainAttack buff = ChainFactory.Attack("Buff", Role.Support) with { SelfDamageBuff = 0.5, SelfDamageBuffDuration = 1000.0 };

        Assert.Equal(1.5, DamageModifiers.ClickBuffModifier(ChainFactory.Context([buff]))!.Multiplier);
    }

    [Fact]
    public void Debuffs_are_absent_without_a_debuffing_power()
    {
        Assert.Null(DamageModifiers.TargetDebuffModifier(ChainFactory.Context([ChainFactory.Attack("Hit")])));
    }

    [Fact]
    public void A_chain_attack_in_the_chosen_rotation_debuffs_the_target()
    {
        ModifierResult result = DamageModifiers.TargetDebuffModifier(ChainFactory.Context([Debuffing("Hit", Role.Chain)]))!;

        Assert.Equal(F32.Round(1.2), result.Multiplier);
    }

    [Fact]
    public void The_assumption_text_lists_each_debuff_with_its_uptime()
    {
        ModifierResult result = DamageModifiers.TargetDebuffModifier(ChainFactory.Context([Debuffing("Hit", Role.Chain), Debuffing("Mortar", Role.Patch, 0.1, 30.0)]))!;

        Assert.EndsWith("Hit -20% res; Mortar -10% res (patch) at 100% uptime", result.Assumption, StringComparison.Ordinal);
    }

    [Fact]
    public void A_persistent_debuff_that_needs_melee_is_up_the_melee_share()
    {
        ChainAttack field = Debuffing("Field", Role.PersistentDebuff) with { Profile = ChainFactory.Profile(Role.PersistentDebuff, Delivery.Melee) };

        Assert.Contains("(persistent_debuff) at 100% uptime", DamageModifiers.TargetDebuffModifier(ChainFactory.Context([field]))!.Assumption, StringComparison.Ordinal);
    }

    [Fact]
    public void A_persistent_debuff_that_needs_melee_does_nothing_at_range()
    {
        ChainAttack field = Debuffing("Field", Role.PersistentDebuff) with { Profile = ChainFactory.Profile(Role.PersistentDebuff, Delivery.Melee) };

        Assert.Null(DamageModifiers.TargetDebuffModifier(ChainFactory.Context([field], Stance.Ranged)));
    }

    [Fact]
    public void A_persistent_debuff_from_range_is_always_up()
    {
        Assert.Contains("at 100% uptime", DamageModifiers.TargetDebuffModifier(ChainFactory.Context([Debuffing("Field", Role.PersistentDebuff)], Stance.Ranged))!.Assumption, StringComparison.Ordinal);
    }

    [Fact]
    public void A_target_resistance_outside_the_range_is_e57()
    {
        DamageContext context = ChainFactory.Context([Debuffing("Hit", Role.Chain)], assumptions: new Dictionary<string, double>(StringComparer.Ordinal) { ["target.resistance"] = 1.5 });

        Assert.Equal("E57: assumption target.resistance=1.5 is outside [-1, 0.95]; set it within that range", Assert.Throws<ChainException>(() => DamageModifiers.TargetDebuffModifier(context)).Message);
    }

    [Fact]
    public void A_high_target_resistance_widens_the_debuffs_effect()
    {
        DamageContext context = ChainFactory.Context([Debuffing("Hit", Role.Chain)], assumptions: HighResistance) with { Stance = Stance.Melee };

        Assert.True(DamageModifiers.TargetDebuffModifier(context)!.Multiplier > 1.2);
    }
}
