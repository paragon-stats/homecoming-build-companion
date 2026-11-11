using CohEngine.Core.Data;
using CohEngine.Core.Enhancement;
using CohEngine.Core.Maths;
using CohEngine.Core.SetBonuses;
using CohEngine.Core.Totals;
using CohEngine.Core.Totals.Phases;

using static CohEngine.Core.Tests.Totals.TotalsMake;

using Pipeline = CohEngine.Core.Totals.Pipeline;

namespace CohEngine.Core.Tests.Totals;

/// <summary>
/// Port of <c>test_base_totals.py</c> TestEffectRouting, TestGlobalEnhancementFold, TestEnhancementGuards,
/// TestEnhancePass and TestGetEnhancementMagSum.
/// </summary>
public sealed class EnhancementPhasesTests
{
    private static readonly Effect Base = Make.Effect;
    private static readonly SlotRef Io50 = new() { Level = 0, IsInherent = false, Enh = 1, Grade = 0, IoLevel = 49, RelativeLevel = 4 };

    public static TheoryData<Effect, string, string, string> GlobalTerms => new()
    {
        { Base with { EffectType = "Regeneration" }, "effect", "eEffectType", "Regeneration" },
        { Base with { EffectType = "Defense", DamageType = "Cold" }, "defense", "eDamage", "Cold" },
        { Base with { EffectType = "Resistance", DamageType = "Cold" }, "resistance", "eDamage", "Cold" },
        { Base with { EffectType = "Damage", DamageType = "Cold" }, "damage", "eDamage", "Cold" },
        { Base with { EffectType = "Mez", MezType = "Held" }, "mez", "eMez", "Held" },
        { Base with { EffectType = "SpeedFlying", Scale = 1.0 }, "effect", "eEffectType", "SpeedFlying" },
        { Base with { EffectType = "SpeedFlying", Scale = -1.0 }, "effect_aux", "eEffectType", "SpeedFlying" },
        { Base with { EffectType = "Enhancement", EtModifies = "SpeedJumping", Scale = -1.0 }, "effect_aux", "eEffectType", "SpeedJumping" },
        { Base with { EffectType = "Enhancement", EtModifies = "SpeedJumping", Scale = 1.0 }, "effect", "eEffectType", "SpeedJumping" },
    };

    public static TheoryData<Effect> GlobalSkips => new()
    {
        // A scalar aspect is folded into the power, not the effect.
        Base with { EffectType = "RechargeTime" },
        Base with { EffectType = "Regeneration", Buffable = false },
        Base with { EffectType = "SpeedFlying", Scale = 0.0 },
        Base with { EffectType = "Mez", MezType = "Held", AttribType = "Duration" },
    };

    public static TheoryData<Effect, string, int> EnhancePassRoutes => new()
    {
        { Base with { EffectType = "Enhancement", EtModifies = "Mez", MezType = "Held", ToWho = "Self" }, "mez", Mez("Held") },
        { Base with { EffectType = "Enhancement", EtModifies = "Defense", DamageType = "Fire", ToWho = "Self" }, "defense", Damage("Fire") },
        { Base with { EffectType = "Enhancement", EtModifies = "Defense", DamageType = "None", ToWho = "Self" }, "effect", Type("Defense") },
        { Base with { EffectType = "Enhancement", EtModifies = "Resistance", DamageType = "None", ToWho = "Self" }, "effect", Type("Resistance") },
        { Base with { EffectType = "Enhancement", EtModifies = "Resistance", DamageType = "Cold", ToWho = "Self" }, "resistance", Damage("Cold") },
        { Base with { EffectType = "DamageBuff", DamageType = "Fire", ToWho = "Self" }, "damage", Damage("Fire") },
        { Base with { EffectType = "Enhancement", EtModifies = "Heal", ToWho = "Self" }, "effect", Type("Heal") },
        { Base with { EffectType = "Enhancement", EtModifies = "RechargeTime", ToWho = "Self" }, "effect", Type("RechargeTime") },
        { Base with { EffectType = "Enhancement", EtModifies = "SpeedRunning", ToWho = "Self", BuffMode = "Normal" }, "effect", Type("SpeedRunning") },
        { Base with { EffectType = "Enhancement", EtModifies = "SpeedRunning", ToWho = "Self", BuffMode = "Debuff" }, "effect_aux", Type("SpeedRunning") },
        { Base with { EffectType = "Enhancement", EtModifies = "Mez", MezType = "None", ToWho = "Self" }, "mez", Mez("None") },
    };

    public static TheoryData<Effect> EnhancementSumsExcluded => new()
    {
        Base with { EffectType = "Enhancement", EtModifies = "Haste", Probability = 0.0 },
        Base with { EffectType = "Defense", EtModifies = "Haste" },
        Base with { EffectType = "Enhancement", EtModifies = "Haste", ToWho = "Target" },
        Base with { EffectType = "Enhancement", EtModifies = "Haste", SpecialCase = "Domination" },
        Base with { EffectType = "Enhancement", EtModifies = "Haste", PvMode = "PvP" },
        Base with { EffectType = "Enhancement", EtModifies = "Haste", AbsorbedEffect = true, AbsorbedPowerType = "GlobalBoost" },
        Base with { EffectType = "Enhancement", EtModifies = "Recovery" },
    };

    private static MathTables Tables => Fixtures.Store.Maths;

    [Fact]
    public void Granted_aspect_without_a_matching_effect_is_inert()
    {
        EnhancementRecord record = Record(("Resistance", -1, (int)Schedule.B), ("RechargeTime", -1, (int)Schedule.A));
        Assert.Null(Multiplier(Base with { EffectType = "Recovery", Buffable = true }, record, 1));
    }

    [Fact]
    public void Regeneration_debuff_resistance_takes_heal_without_ed()
    {
        // Only the Heal enhancement's allowed-effects list reaches this effect, and ED skips it.
        double single = Tables.MultIo[49][(int)Schedule.A];
        double raw = F32.Round(F32.Round(F32.Round(single + single) + single) + single);
        Effect effect = Base with { EffectType = "ResEffect", EtModifies = "Regeneration", Buffable = true };
        Assert.Equal(F32.Round(1.0 + raw), Multiplier(effect, Record(("Heal", -1, 0)), 4));
    }

    [Theory]
    [InlineData("Held", true)]
    [InlineData("Stunned", false)]
    public void Mez_effect_takes_only_its_own_subtype(string mezType, bool enhanced)
    {
        double? got = Multiplier(Base with { EffectType = "Mez", MezType = mezType, Buffable = true }, Record(("Mez", Mez("Held"), 0)), 1);
        Assert.Equal(enhanced, got is not null);
    }

    [Theory]
    [InlineData("Mez", "Held", "None", "Duration")]
    [InlineData("Damage", "None", "Special", "Magnitude")]
    public void Effects_whose_magnitude_mids_leaves_unenhanced(string effectType, string mezType, string damageType, string attribType)
    {
        Effect effect = Base with { EffectType = effectType, MezType = mezType, DamageType = damageType, AttribType = attribType, Buffable = true };
        Assert.Null(Multiplier(effect, Record(("Mez", Mez("Held"), 0), ("Damage", -1, 0)), 1));
    }

    [Fact]
    public void Two_effects_of_one_key_share_the_memo_and_incarnates_add_their_terms()
    {
        Power power = Make.Power(Base with { EffectType = "Defense", Buffable = true }, Base with { Index = 1, EffectType = "Defense", Buffable = true }) with { BuildIndex = 0 };
        Dictionary<(int BuildIndex, int EffectIndex), (double PreEd, double PostEd)> incarnate = new() { [(0, 1)] = (0.1, 0.2) };
        Dictionary<(int BuildIndex, int EffectIndex), double> mults = PerPowerEnhancementMathPhase.ComputeEnhMultipliers([power], Slots(1), EnhDb(Record(("Defense", -1, 1))), Tables, 50, Context(), incarnate);
        double slotted = mults[(0, 0)];
        double preEd = F32.Round(Tables.MultIo[49][1] + 0.1);
        Assert.Equal(F32.Round(1.0 + F32.Round(F32.Round(Ed.ApplyEd(Schedule.B, preEd, Tables) + 0.0) + 0.2)), mults[(0, 1)]);
        Assert.Equal(F32.Round(1.0 + Tables.MultIo[49][1]), slotted);
    }

    [Theory]
    [MemberData(nameof(GlobalTerms))]
    public void Global_term_is_added_after_ed(Effect effect, string bucket, string enumName, string member)
    {
        BuffsX selfEnhance = new(Enums);
        Bucket(selfEnhance, bucket)[Enums.Maps[enumName][member]] = F32.Round(0.25);
        Assert.Equal(F32.Round(1.25), Multiplier(effect with { Buffable = true }, Record(), 0, selfEnhance));
    }

    [Theory]
    [MemberData(nameof(GlobalSkips))]
    public void Global_term_skips(Effect effect)
    {
        BuffsX selfEnhance = new(Enums);
        foreach (string name in new[] { "RechargeTime", "Regeneration", "SpeedFlying" })
        {
            selfEnhance.Effect[Type(name)] = F32.Round(0.25);
            selfEnhance.EffectAux[Type(name)] = F32.Round(0.25);
        }

        selfEnhance.Mez[Mez("Held")] = F32.Round(0.25);
        Assert.Null(Multiplier(effect, Record(), 0, selfEnhance));
    }

    [Fact]
    public void Set_bonus_power_takes_no_global_term()
    {
        BuffsX selfEnhance = new(Enums);
        selfEnhance.Effect[Type("Regeneration")] = F32.Round(0.25);
        Power power = Make.Power(Base with { EffectType = "Regeneration", Buffable = true }) with { BuildIndex = Accumulator.SetBonusBuildIndex };
        Assert.Empty(PerPowerEnhancementMathPhase.ComputeEnhMultipliers([power], new Dictionary<int, IReadOnlyList<SlotRef>>(), EnhDb(), Tables, 50, Context(), selfEnhance: selfEnhance));
    }

    [Fact]
    public void Global_speed_buff_reaches_fly_speed()
    {
        // A self Enhancement(SpeedFlying) in one power raises another power's fly speed.
        Power booster = Make.Power(Base with { EffectType = "Enhancement", EtModifies = "SpeedFlying", ToWho = "Self", Scale = 0.5 }) with { BuildIndex = 0 };
        Power flight = Make.Power(Base with { EffectType = "SpeedFlying", ToWho = "Self", Buffable = true }, Base with { Index = 1, EffectType = "Fly", ToWho = "Self" }) with { BuildIndex = 1, PowerType = "Toggle" };
        Pipeline.BuildOptions options = new() { Slots = new Dictionary<int, IReadOnlyList<SlotRef>>(), EnhDb = EnhDb(), Tables = Tables };
        Assert.Equal(F32.Round(F32.Round(1 + F32.Round(1.5)) * Fixtures.Store.ServerData.BaseFlySpeed), Compute([booster, flight], options).Totals.FlySpd);
    }

    [Fact]
    public void Partial_enhancement_inputs_raise()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() => Compute([], new Pipeline.BuildOptions { Slots = new Dictionary<int, IReadOnlyList<SlotRef>>() }));
        Assert.StartsWith("E11", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Self_damage_buff_feeds_buff_dam()
    {
        Power power = Make.Power(Base with { EffectType = "DamageBuff", ToWho = "Self", DamageType = "Smashing", ModifierTable = "Ones" });
        Assert.Equal(F32.Round(1.0), Compute([power]).Totals.BuffDam);
    }

    [Theory]
    [MemberData(nameof(EnhancePassRoutes))]
    public void Enhance_pass_routes_to_its_bucket(Effect effect, string bucket, int index)
    {
        BuffsX buffs = EnhancePass(Make.Power(effect));
        Assert.Equal(F32.Round(1.0), Bucket(buffs, bucket)[index]);
        if (bucket is "effect" or "effect_aux")
        {
            Assert.Equal(0.0, Bucket(buffs, string.Equals(bucket, "effect", StringComparison.Ordinal) ? "effect_aux" : "effect")[index]);
        }
    }

    [Fact]
    public void Enhance_pass_boost_head_fires()
    {
        Assert.Equal(F32.Round(1.0), EnhancePass(Make.Power(Base with { EffectType = "Enhancement", EtModifies = "RechargeTime", ToWho = "Self" })).Boosts[Type("RechargeTime")]);
        Assert.Equal(F32.Round(1.0), EnhancePass(Make.Power(Base with { EffectType = "Enhancement", EtModifies = "Mez", MezType = "Held", ToWho = "Self" })).BoostsMez[Mez("Held")]);
        Assert.Equal(F32.Round(1.0), EnhancePass(Make.Power(Base with { EffectType = "Enhancement", EtModifies = "Heal", ToWho = "Self" })).Boosts[Type("Heal")]);
    }

    [Fact]
    public void Defiance_damage_buff_is_excluded()
    {
        Effect effect = Base with { EffectType = "DamageBuff", DamageType = "Fire", ToWho = "Self", EffectClass = "Tertiary", IsEnhancementEffect = true };
        Assert.Equal(0.0, EnhancePass(Make.Power(effect)).Damage[Damage("Fire")]);
    }

    [Fact]
    public void Accuracy_enhancement_is_dropped_from_effect()
    {
        BuffsX buffs = EnhancePass(Make.Power(Base with { EffectType = "Enhancement", EtModifies = "Accuracy", ToWho = "Self" }));
        Assert.Equal(F32.Round(1.0), buffs.Boosts[Type("Accuracy")]);
        Assert.Equal(0.0, buffs.Effect[Type("Accuracy")]);
    }

    [Fact]
    public void Mez_none_subtype_takes_no_boost()
    {
        Assert.All(EnhancePass(Make.Power(Base with { EffectType = "Enhancement", EtModifies = "Mez", MezType = "None", ToWho = "Self" })).BoostsMez, value => Assert.Equal(0.0, value));
    }

    [Theory]
    [InlineData("Unspecified", "Toggle")]
    [InlineData("Self", "GlobalBoost")]
    public void Effects_gathered_but_not_routed(string toWho, string powerType)
    {
        BuffsX buffs = EnhancePass(Make.Power(Base with { EffectType = "Enhancement", EtModifies = "RechargeTime", ToWho = toWho }) with { PowerType = powerType });
        Assert.All(buffs.Effect, value => Assert.Equal(0.0, value));
        Assert.All(buffs.Boosts, value => Assert.Equal(0.0, value));
    }

    [Fact]
    public void Absorbed_global_boost_effect_skips_main_routing()
    {
        BuffsX buffs = EnhancePass(Make.Power(Base with { EffectType = "Enhancement", EtModifies = "RechargeTime", ToWho = "Self", AbsorbedPowerType = "GlobalBoost" }));
        Assert.Equal(F32.Round(1.0), buffs.Boosts[Type("RechargeTime")]);
        Assert.Equal(0.0, buffs.Effect[Type("RechargeTime")]);
    }

    [Theory]
    [MemberData(nameof(EnhancementSumsExcluded))]
    public void Enhancement_mag_sum_excludes(Effect effect)
    {
        Assert.Empty(Context().EnhancementMagSum(Make.Power(effect), "Haste"));
    }

    [Fact]
    public void Enhancement_mag_sum_includes_a_matching_effect()
    {
        (Effect _, double mag) = Assert.Single(Context().EnhancementMagSum(Make.Power(Base with { EffectType = "Enhancement", EtModifies = "Haste", ToWho = "Self" }), "Haste"));
        Assert.Equal(F32.Round(1.0), mag);
    }

    [Fact]
    public void Toggle_end_aggregate_stores_only_non_zero_toggles()
    {
        Power toggle = Make.Power() with { BuildIndex = 0 };
        Power click = Make.Power() with { BuildIndex = 1, PowerType = "Click" };
        Power empty = Make.Power() with { BuildIndex = 2 };
        Dictionary<int, IReadOnlyList<SlotRef>> slots = new() { [0] = [Io50], [1] = [Io50] };
        Dictionary<int, double> aggregates = PerPowerEnhancementMathPhase.ComputeToggleEndAgg([toggle, click, empty], slots, EnhDb(Record(("EnduranceDiscount", -1, 0))), Tables, 50, new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>());
        Assert.Equal([0], aggregates.Keys);
    }

    [Fact]
    public void Buffed_end_cost_is_computed_only_for_powers_that_read_the_keyword()
    {
        Power reader = Make.Power(Base with { AttribType = "Expression", ExprMagnitude = "power.base>endcost / 2" }) with { BuildIndex = 0, EndCost = F32.Round(8.5) };
        Power plain = Make.Power() with { BuildIndex = 1, EndCost = F32.Round(8.5) };
        Dictionary<int, IReadOnlyList<SlotRef>> slots = new() { [0] = [Io50], [1] = [Io50] };

        Dictionary<int, double> costs = PerPowerEnhancementMathPhase.ComputeBuffedEndCosts([reader, plain], slots, EnhDb(Record(("EnduranceDiscount", -1, 0))), Tables, 50, new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>(), new PerPowerEnhancementMathPhase.EndFold(0.0, 5.0));

        double aggregate = Ed.ApplyEd(Schedule.A, Tables.MultIo[49][(int)Schedule.A], Tables);
        Assert.Equal([0], costs.Keys);
        Assert.Equal(F32.Round(F32.Round(8.5) / F32.Round(F32.Round(F32.Round(aggregate + 0.0) + 0.0) + 1.0)), costs[0]);
    }

    [Fact]
    public void Buffed_end_cost_takes_the_global_discount_and_the_incarnate_addends()
    {
        Power reader = Make.Power(Base with { AttribType = "Expression", ExprMagnitude = "power.base>endcost" }) with { BuildIndex = 0, EndCost = F32.Round(8.5) };
        Dictionary<int, IReadOnlyList<SlotRef>> slots = new() { [0] = [Io50] };
        Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)> incarnate = new() { [(0, "EnduranceDiscount")] = (0.0, 0.1) };
        IReadOnlyDictionary<int, EnhancementRecord> enhDb = EnhDb(Record(("EnduranceDiscount", -1, 0)));

        double slotted = PerPowerEnhancementMathPhase.ComputeBuffedEndCosts([reader], slots, enhDb, Tables, 50, new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>(), new PerPowerEnhancementMathPhase.EndFold(0.0, 5.0))[0];
        double global = PerPowerEnhancementMathPhase.ComputeBuffedEndCosts([reader], slots, enhDb, Tables, 50, new Dictionary<(int BuildIndex, string Aspect), (double PreEd, double PostEd)>(), new PerPowerEnhancementMathPhase.EndFold(0.25, 5.0))[0];
        double withIncarnate = PerPowerEnhancementMathPhase.ComputeBuffedEndCosts([reader], slots, enhDb, Tables, 50, incarnate, new PerPowerEnhancementMathPhase.EndFold(0.0, 5.0))[0];

        Assert.True(global < slotted);
        Assert.True(withIncarnate < slotted);
        Assert.True(slotted < F32.Round(8.5));
    }

    [Fact]
    public void A_context_with_a_buffed_cost_gives_the_keyword_the_reduced_cost_to_the_powers_that_have_one_only()
    {
        Effect reader = Base with { AttribType = "Expression", ExprMagnitude = "power.base>endcost / 2" };
        Power power = Make.Power(reader) with { BuildIndex = 0, EndCost = F32.Round(8.0) };
        MagContext context = Context() with { BuffedEndCost = new Dictionary<int, double> { [0] = F32.Round(2.0) } };

        Assert.Equal(1.0, context.Mag(reader, power));
        Assert.Equal(4.0, context.Mag(reader, power with { BuildIndex = 1 }));
        Assert.Equal(4.0, Context().Mag(reader, power));
    }

    [Fact]
    public void The_pipeline_reads_the_slotted_cost_for_the_keyword_in_the_totals()
    {
        Effect reader = Base with { EffectType = "Endurance", Aspect = "Max", ToWho = "Self", AttribType = "Expression", ExprMagnitude = "power.base>endcost", Buffable = false };
        Power power = Make.Power(reader) with { BuildIndex = 0, PowerType = "Click", EndCost = F32.Round(8.0) };
        Pipeline.BuildOptions slotted = new() { Slots = Slots(1), EnhDb = EnhDb(Record(("EnduranceDiscount", -1, 0))), Tables = Tables };
        Pipeline.BuildOptions bare = new() { Slots = new Dictionary<int, IReadOnlyList<SlotRef>>(), EnhDb = EnhDb(), Tables = Tables };

        double unslotted = Compute([power], bare).Totals.EndMax;
        double reduced = Compute([power], slotted).Totals.EndMax;

        Assert.Equal(F32.Round(8.0), unslotted);
        Assert.True(reduced < unslotted);
        Assert.Equal(F32.Round(F32.Round(8.0) / F32.Round(F32.Round(Ed.ApplyEd(Schedule.A, Tables.MultIo[49][(int)Schedule.A], Tables) + 0.0) + 1.0)), reduced);
    }

    private static double[] Bucket(BuffsX buffs, string name)
    {
        return name switch
        {
            "effect" => buffs.Effect,
            "effect_aux" => buffs.EffectAux,
            "defense" => buffs.Defense,
            "resistance" => buffs.Resistance,
            "damage" => buffs.Damage,
            _ => buffs.Mez,
        };
    }

    private static EnhancementRecord Record(params (string Aspect, int Sub, int Schedule)[] aspects)
    {
        return new EnhancementRecord
        {
            Nid = 1,
            StaticIndex = 1,
            TypeName = "InventO",
            Superior = false,
            Effects = [.. aspects.Select(aspect => new EnhEffect { Enhance = aspect.Aspect, EnhanceSubId = aspect.Sub, Schedule = aspect.Schedule, Multiplier = 1.0, BuffMode = "Any" })],
            NidSet = -1,
            IsProc = false,
            PowerNid = -1,
        };
    }

    private static Dictionary<int, EnhancementRecord> EnhDb(EnhancementRecord? record = null)
    {
        return record is null ? [] : new() { [1] = record };
    }

    private static Dictionary<int, IReadOnlyList<SlotRef>> Slots(int count)
    {
        return new() { [0] = [.. Enumerable.Repeat(Io50, count)] };
    }

    private static double? Multiplier(Effect effect, EnhancementRecord record, int slotCount, BuffsX? selfEnhance = null)
    {
        Power power = Make.Power(effect) with { BuildIndex = 0 };
        Dictionary<(int BuildIndex, int EffectIndex), double> mults = PerPowerEnhancementMathPhase.ComputeEnhMultipliers([power], Slots(slotCount), EnhDb(record), Tables, 50, Context(), selfEnhance: selfEnhance);
        return mults.TryGetValue((0, effect.Index), out double value) ? value : null;
    }
}
