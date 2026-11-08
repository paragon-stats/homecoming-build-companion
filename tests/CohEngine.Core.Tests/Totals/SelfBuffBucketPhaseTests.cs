using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Totals;

using static CohEngine.Core.Tests.Totals.TotalsMake;

namespace CohEngine.Core.Tests.Totals;

/// <summary>Port of <c>test_base_totals.py</c> TestGetEffectMagSum and TestBuffRouting: the gather filters and the buff-pass buckets.</summary>
public sealed class SelfBuffBucketPhaseTests
{
    private static readonly Effect Base = Make.Effect;

    [Fact]
    public void Unspecified_to_who_is_excluded()
    {
        Assert.Empty(MagSum(Make.Power(Base with { ToWho = "Unspecified" }), "Defense"));
    }

    [Fact]
    public void Only_self_drops_target_effects()
    {
        Power power = Make.Power(Base with { ToWho = "Target" });
        Assert.Empty(Context().EffectMagSum(power, "Defense", onlySelf: true));
        Assert.Single(MagSum(power, "Defense"));
    }

    [Fact]
    public void Only_target_drops_self_effects()
    {
        Assert.Empty(Context().EffectMagSum(Make.Power(Base with { ToWho = "Self" }), "Defense", onlyTarget: true));
    }

    [Fact]
    public void Max_aspect_speed_effects_need_max_mode()
    {
        Power power = Make.Power(Base with { EffectType = "SpeedRunning", Aspect = "Max" });
        Assert.Empty(MagSum(power, "SpeedRunning"));
        Assert.Single(Context().EffectMagSum(power, "SpeedRunning", maxMode: true));
    }

    [Fact]
    public void Max_mode_drops_non_max_aspects()
    {
        Assert.Empty(Context().EffectMagSum(Make.Power(Base with { EffectType = "SpeedRunning", Aspect = "Cur" }), "SpeedRunning", maxMode: true));
    }

    [Fact]
    public void Active_suppression_bits_exclude()
    {
        Power power = Make.Power(Base with { Suppression = 7 });
        Assert.Empty(Context(Pve with { Suppression = 4 }).EffectMagSum(power, "Defense"));
        Assert.Single(MagSum(power, "Defense"));
    }

    [Theory]
    [InlineData("probability")]
    [InlineData("type")]
    [InlineData("Ignored")]
    [InlineData("Special")]
    [InlineData("conditional")]
    [InlineData("Hidden")]
    [InlineData("PvP")]
    public void Effects_that_are_filtered_out(string filter)
    {
        Effect effect = filter switch
        {
            "probability" => Base with { Probability = 0.0 },
            "type" => Base with { EffectType = "Resistance" },
            "Ignored" or "Special" => Base with { EffectClass = filter },
            "conditional" => Base with { ActiveConditionalsCount = 1 },
            "Hidden" => Base with { SpecialCase = "Hidden" },
            _ => Base with { PvMode = "PvP" },
        };
        Assert.Empty(MagSum(Make.Power(effect), "Defense"));
    }

    [Fact]
    public void Delayed_effects_excluded_unless_requested()
    {
        Power power = Make.Power(Base with { DelayedTime = 10.0 });
        Assert.Empty(MagSum(power, "Defense"));
        Assert.Single(Context().EffectMagSum(power, "Defense", includeDelayed: true));
    }

    [Fact]
    public void Pve_only_effects_dropped_in_pvp()
    {
        Power power = Make.Power(Base with { PvMode = "PvE" }, Base with { Index = 1, PvMode = "PvP" });
        Assert.Equal([1], Context(Pvp).EffectMagSum(power, "Defense").Select(pair => pair.Effect.Index));
    }

    [Fact]
    public void Class_restricted_effects_gate_on_archetype()
    {
        Assert.Empty(MagSum(Make.Power(Base with { NIdClassName = 1 }), "Defense"));
        Assert.Single(MagSum(Make.Power(Base with { NIdClassName = 0 }), "Defense"));
    }

    [Fact]
    public void Stacking_dot_multiplies_by_ticks()
    {
        Power power = Make.Power(Base with { Scale = 2.0, Ticks = 5, Stacking = "Yes" }, Base with { Index = 1, Scale = 2.0, Ticks = 5, Stacking = "No" });
        Assert.Equal([F32.Round(10.0), F32.Round(2.0)], MagSum(power, "Defense").Select(pair => pair.Mag));
    }

    [Fact]
    public void Enhanced_magnitude_reads_the_multiplier()
    {
        Power power = Make.Power(Base with { Scale = 2.0 });
        MagContext context = Context() with { EnhMult = new Dictionary<(int BuildIndex, int EffectIndex), double> { [(0, 0)] = 1.5 } };
        Assert.Equal(F32.Round(3.0), Assert.Single(context.EffectMagSum(power, "Defense")).Mag);
    }

    [Theory]
    [InlineData("GlobalBoost", "Self", false)]
    [InlineData("Toggle", "Target", false)]
    [InlineData("Toggle", "Self", true)]
    public void Global_boost_powers_and_target_effects_do_not_route(string powerType, string toWho, bool absorbedGlobal)
    {
        Effect effect = absorbedGlobal ? Base with { AbsorbedEffect = true, AbsorbedPowerType = "GlobalBoost" } : Base with { ToWho = toWho };
        BuffsX buffs = BuffPass(Make.Power(effect) with { PowerType = powerType });
        Assert.All(buffs.Defense, value => Assert.Equal(0.0, value));
    }

    [Fact]
    public void Enhancement_effects_route_to_boosts()
    {
        Power power = Make.Power(
            Base with { EffectType = "Enhancement", EtModifies = "RechargeTime", Scale = 0.2 },
            Base with { Index = 1, EffectType = "Enhancement", EtModifies = "Mez", MezType = "Held", Scale = 0.3 },
            Base with { Index = 2, EffectType = "Enhancement", EtModifies = "None", Scale = 0.4 });
        BuffsX buffs = BuffPass(power);
        Assert.Equal(F32.Round(0.2), buffs.Boosts[Type("RechargeTime")]);
        Assert.Equal(F32.Round(0.3), buffs.BoostsMez[Mez("Held")]);
        Assert.DoesNotContain(F32.Round(0.4), buffs.Boosts);
    }

    [Fact]
    public void Enhancement_range_and_heal_redirect_to_effect_buckets()
    {
        Power power = Make.Power(
            Base with { EffectType = "Enhancement", EtModifies = "Range", Scale = 0.25 },
            Base with { Index = 1, EffectType = "Enhancement", EtModifies = "Heal", Scale = 0.15 });
        BuffsX buffs = BuffPass(power);
        Assert.Equal(F32.Round(0.25), buffs.Effect[Type("Range")]);
        Assert.Equal(F32.Round(0.15), buffs.Effect[Type("Heal")]);
    }

    [Fact]
    public void Heal_effects_are_not_summed()
    {
        Assert.Equal(0.0, BuffPass(Make.Power(Base with { EffectType = "Heal", Scale = 2.0 })).Effect[Type("Heal")]);
    }

    [Fact]
    public void Max_endurance_accumulates()
    {
        Assert.Equal(F32.Round(10.0), BuffPass(Make.Power(Base with { EffectType = "Endurance", Aspect = "Max", Scale = 10.0 })).MaxEnd);
    }

    [Fact]
    public void Et_modifies_mez_routes_to_mez_bucket()
    {
        Power power = Make.Power(
            Base with { EffectType = "MovementControl", EtModifies = "Mez", MezType = "Held", Scale = 2.0 },
            Base with { Index = 1, EffectType = "MovementControl", EtModifies = "MezResist", MezType = "Sleep", Scale = 3.0 });
        BuffsX buffs = BuffPass(power);
        Assert.Equal(F32.Round(2.0), buffs.Mez[Mez("Held")]);
        Assert.Equal(F32.Round(3.0), buffs.MezRes[Mez("Sleep")]);
    }

    [Fact]
    public void Status_buckets_take_mez_mez_resist_and_res_effect()
    {
        Power power = Make.Power(
            Base with { EffectType = "Mez", MezType = "Held", Scale = 2.0 },
            Base with { Index = 1, EffectType = "MezResist", MezType = "Sleep", Scale = 0.5 },
            Base with { Index = 2, EffectType = "ResEffect", EtModifies = "Recovery", Scale = 0.25 });
        BuffsX buffs = BuffPass(power);
        Assert.Equal(F32.Round(2.0), buffs.StatusProtection[Mez("Held")]);
        Assert.Equal(F32.Round(0.5), buffs.StatusResistance[Mez("Sleep")]);
        Assert.Equal(F32.Round(0.25), buffs.DebuffResistance[Type("Recovery")]);
    }

    [Fact]
    public void Typed_elusivity_routes()
    {
        Assert.Equal(F32.Round(0.2), BuffPass(Make.Power(Base with { EffectType = "Elusivity", DamageType = "Melee", Scale = 0.2 })).Elusivity[Damage("Melee")]);
    }

    [Fact]
    public void Typed_defense_and_resistance_route_generic_falls_through()
    {
        Power power = Make.Power(
            Base with { EffectType = "Defense", DamageType = "Fire", Scale = 0.1 },
            Base with { Index = 1, EffectType = "Resistance", DamageType = "Cold", Scale = 0.2 },
            Base with { Index = 2, EffectType = "Resistance", DamageType = "None", Scale = 0.3 },
            Base with { Index = 3, EffectType = "Recovery", DamageType = "Fire", Scale = 0.4 });
        BuffsX buffs = BuffPass(power);
        Assert.Equal(F32.Round(0.1), buffs.Defense[Damage("Fire")]);
        Assert.Equal(F32.Round(0.2), buffs.Resistance[Damage("Cold")]);
        Assert.Equal(F32.Round(0.3), buffs.Effect[Type("Resistance")]);
        Assert.Equal(F32.Round(0.4), buffs.Effect[Type("Recovery")]);
    }

    [Fact]
    public void Accuracy_et_modifies_routes_to_tohit()
    {
        Assert.Equal(F32.Round(0.05), BuffPass(Make.Power(Base with { EffectType = "MovementControl", EtModifies = "Accuracy", Scale = 0.05 })).Effect[Stat("ToHit")]);
    }

    [Theory]
    [InlineData("SpeedRunning", "MaxRunSpeed")]
    [InlineData("SpeedJumping", "MaxJumpSpeed")]
    [InlineData("SpeedFlying", "MaxFlySpeed")]
    public void Max_aspect_speed_effects_raise_the_speed_cap(string speed, string statName)
    {
        Power power = Make.Power(Base with { EffectType = speed, EtModifies = speed, Aspect = "Max", Scale = 0.5 }, Base with { Index = 1, EffectType = statName, ToWho = "Self", Scale = 0.25 });

        // The Max-aspect speed effect routes to the cap stat; the scalar MaxXSpeed self effect is added by the gather.
        Assert.Equal(F32.Round(0.75), BuffPass(power).Effect[Stat(statName)]);
    }

    [Fact]
    public void Tertiary_enhancement_tohit_is_ignored()
    {
        Power power = Make.Power(Base with { EffectType = "ToHit", IsEnhancementEffect = true, EffectClass = "Tertiary", Scale = 0.2 });
        Assert.Equal(0.0, BuffPass(power).Effect[Stat("ToHit")]);
        Assert.Equal(F32.Round(0.2), BuffPass(Make.Power(Base with { EffectType = "ToHit", Scale = 0.2 })).Effect[Stat("ToHit")]);
    }

    [Fact]
    public void Percentage_absorb_scales_by_previous_hp_max()
    {
        Assert.Equal(F32.Round(100.0), BuffPass(Make.Power(Base with { EffectType = "Absorb", DisplayPercentage = true, Scale = 0.1 }), prevHpMax: 1000.0).Effect[Stat("Absorb")]);
    }

    [Fact]
    public void Click_powers_subtract_their_base_magnitude()
    {
        // A stacking DoT on a click: 3 * mag is added and raw mag subtracted, so 2 * mag survives.
        Power power = Make.Power(Base with { EffectType = "Recovery", Scale = 1.0, Ticks = 3, Stacking = "Yes" }) with { PowerType = "Click", ClickBuff = false };
        Assert.Equal(F32.Round(2.0), BuffPass(power).Effect[Stat("EndRec")]);
    }

    [Theory]
    [InlineData(true, "BuffAcc")]
    [InlineData(false, "ToHit")]
    public void Accuracy_routes_by_global_source(bool global, string stat)
    {
        Power power = Make.Power(Base with { EffectType = "Enhancement", EtModifies = "Accuracy", Scale = 0.1 });
        Assert.Equal(F32.Round(0.1), BuffPass(power, globalAccSource: global).Effect[Stat(stat)]);
    }

    [Theory]
    [InlineData(-1, "", true)]
    [InlineData(0, "GlobalBoost", true)]
    [InlineData(0, "Toggle", false)]
    public void Is_global_accuracy_source(int buildIndex, string powerType, bool want)
    {
        Assert.Equal(want, MagContext.IsGlobalAccSource(Make.Power() with { BuildIndex = buildIndex, PowerType = powerType }));
    }

    private static List<(Effect Effect, double Mag)> MagSum(Power power, string effectType)
    {
        return Context().EffectMagSum(power, effectType);
    }
}
