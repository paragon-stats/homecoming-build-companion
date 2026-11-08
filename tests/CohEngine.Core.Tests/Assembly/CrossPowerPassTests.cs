using CohEngine.Core.Assembly;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;

using static CohEngine.Core.Tests.Assembly.AssemblyMake;

namespace CohEngine.Core.Tests.Assembly;

/// <summary>Port of <c>test_assembly.py</c>: the cross-power GrantPower pass, the build's chance modifiers and apply_assembly.</summary>
public sealed class CrossPowerPassTests
{
    private static readonly Effect Base = Make.Effect;

    public static TheoryData<Func<Effect, Effect>> SourcesThatDoNotCross => new()
    {
        effect => effect with { EffectClass = "Ignored" },
        effect => effect with { EffectType = "Enhancement" },
        effect => effect with { EffectType = "DamageBuff" },
        effect => effect with { IgnoreEd = true },
        effect => effect with { EffectType = "GrantPower", AbsorbedEffect = true, AbsorbedPowerType = "GlobalBoost" },
    };

    public static TheoryData<int[], string> TargetsThatTakeNothing => new()
    {
        { [], "Primary" },
        { [1, 2], "Pet" },
        { [7], "Primary" },
    };

    public static TheoryData<string[], string[], string[]> ApplyCases => new()
    {
        { ["Range", "Accuracy"], ["Accuracy", "Range"], ["Range", "Accuracy"] },
        { [], ["B", "A"], ["A", "B"] },
    };

    [Fact]
    public void Global_boost_effect_is_absorbed_with_no_grant_probability()
    {
        BuildPowerAssembly source = Global([Base with { EffectType = "Recovery", Ticks = 4, IsEnhancementEffect = true }]);
        Dictionary<int, IReadOnlyList<Effect>> output = BuildAll(new() { [0] = Target(), [1] = source });
        Assert.Equal(["Defense", "Recovery"], Types(output[0]));
        Effect absorbed = output[0][1];
        Assert.Equal((1, "Target", true, true), (absorbed.Index, absorbed.ToWho, absorbed.AbsorbedEffect, absorbed.IsEnhancementEffect));
        Assert.Equal(("GlobalBoost", 20, 4), (absorbed.AbsorbedPowerType, absorbed.AbsorbedPowerNid, absorbed.Ticks));

        // No grant of the source covers the position, so Mids reads a grant probability of 0.
        Assert.Equal(0.0, absorbed.Probability);
        Assert.Equal(["Recovery"], Types(output[1]));
    }

    [Fact]
    public void Absorbed_effect_takes_the_grant_probability_it_falls_under()
    {
        AssemblyPower granted = Power([Base with { EffectType = "Recovery", BaseProbability = 0.5 }], nid: 700, powerType: "GlobalBoost", enhancements: [2]);
        BuildPowerAssembly source = Global([PowerAssemblerTests.Grant() with { BaseProbability = 0.8 }], hasGrant: true);
        Effect absorbed = BuildAll(new() { [0] = Target(), [1] = source }, [granted])[0][1];
        Assert.Equal("Recovery", absorbed.EffectType);

        // The source's own grant step left 0.5 * 0.8; the cross pass multiplies by the grant once more.
        Assert.Equal(F32.Round(F32.Round(0.5 * 0.8) * 0.8), absorbed.Probability);
    }

    [Fact]
    public void Grant_probability_map_skips_a_grant_without_a_power()
    {
        BuildPowerAssembly source = Global(
        [
            Base with { EffectType = "Recovery" },
            Base with { EffectType = "GrantPower", NSummon = -1, EffectClass = "Ignored" },
            Base with { EffectType = "GrantPower", NSummon = 701, BaseProbability = 0.25, EffectClass = "Ignored" },
        ]);
        Dictionary<int, IReadOnlyList<Effect>> output = BuildAll(new() { [0] = Target(), [1] = source }, [Power([Base], nid: 701)]);

        // The one absorbed position sits past the source's three database effects, inside the second grant.
        Assert.Equal(0.25, output[0][1].Probability);
    }

    [Theory]
    [MemberData(nameof(SourcesThatDoNotCross))]
    public void Source_effects_that_do_not_cross(Func<Effect, Effect> over)
    {
        BuildPowerAssembly source = Global([over(Base with { EffectType = "Recovery" })]);
        Assert.Single(BuildAll(new() { [0] = Target(), [1] = source })[0]);
    }

    [Fact]
    public void Non_global_source_crosses_only_global_absorbed_effects()
    {
        AssemblyPower owner = Power([], nid: 30, enhancements: [1]);
        BuildPowerAssembly source = Global(
            [
                Base with { EffectType = "Recovery" },
                Base with { EffectType = "Heal", AbsorbedEffect = true, AbsorbedPowerType = "GlobalBoost", AbsorbedPowerNid = 30 },
                Base with { EffectType = "Endurance", AbsorbedEffect = true, AbsorbedPowerType = "GlobalBoost" },
            ],
            powerType: "Auto_",
            enhancements: [9]);
        Dictionary<int, IReadOnlyList<Effect>> output = BuildAll(new() { [0] = Target(), [1] = source }, [owner]);

        // Heal is allowed through its owner's enhancements; Endurance's owner is the source, which shares none.
        Assert.Equal(["Defense", "Heal"], Types(output[0]));
        Assert.Equal(0, output[0][1].Ticks);
    }

    [Theory]
    [MemberData(nameof(TargetsThatTakeNothing))]
    public void Targets_that_take_nothing(int[] enhancements, string powerSetType)
    {
        BuildPowerAssembly source = Global([Base with { EffectType = "Recovery" }]);
        BuildPowerAssembly target = Target(enhancements: enhancements, powerSetType: powerSetType);
        Assert.Single(BuildAll(new() { [0] = target, [1] = source })[0]);
    }

    [Fact]
    public void Empty_or_inactive_sources_give_nothing()
    {
        BuildPowerAssembly target = Target();
        Assert.Single(BuildAll(new() { [0] = target, [1] = Global([]) })[0]);
        BuildPowerAssembly source = Global([Base with { EffectType = "Recovery" }]);
        Assert.Single(BuildAll(new() { [0] = target, [1] = source }, statInclude: new() { [0] = true, [1] = false })[0]);
    }

    [Fact]
    public void Global_grant_rewrites_every_target_effect()
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal", BaseProbability = 0.5 }], nid: 700);
        Effect grant = PowerAssemblerTests.Grant() with { BaseProbability = 0.5, Ticks = 6, IsEnhancementEffect = true };
        IReadOnlyList<Effect> output = BuildAll(new() { [0] = Target(), [1] = Global([grant]) }, [granted])[0];
        Assert.Equal(["Defense", "Heal"], Types(output));
        Assert.All(output, effect => Assert.Equal(("Target", true, true, 6), (effect.ToWho, effect.AbsorbedEffect, effect.IsEnhancementEffect, effect.Ticks)));

        // The target's own effect: base 1 * 0.5, then * 0.5. The absorbed one: base 0.5 * 0.5, then * 0.5, then the
        // grant it falls under (0.5) once more.
        Assert.Equal(0.25, output[0].Probability);
        Assert.Equal(0.0625, output[1].Probability);
    }

    [Fact]
    public void Pet_powerset_source_refuses()
    {
        BuildPowerAssembly source = Global([Base with { EffectType = "Recovery" }], powerSetType: "Pet");
        Assert.Contains("E89", Assert.Throws<NotSupportedException>(() => BuildAll(new() { [0] = Target(), [1] = source })).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Sources_carry_what_they_absorbed()
    {
        BuildPowerAssembly first = Target(buildIndex: 0, nid: 10);
        BuildPowerAssembly source = Global([Base with { EffectType = "Recovery" }]);
        BuildPowerAssembly last = Target(buildIndex: 2, nid: 11);
        Dictionary<int, IReadOnlyList<Effect>> output = BuildAll(new() { [0] = first, [1] = source, [2] = last });

        // Power 0 absorbed Recovery from 1, so power 2 takes it from both 0 and 1.
        Assert.Equal(["Defense", "Recovery"], Types(output[0]));
        Assert.Equal(["Defense", "Recovery", "Recovery"], Types(output[2]));
        Assert.Equal([10, 20], output[2].Skip(1).Select(effect => effect.AbsorbedPowerNid));
    }

    [Fact]
    public void Absorbed_chance_ids_take_the_source_rewards()
    {
        AssemblyPower granted = Power(
            [Base with { EffectType = "Damage", BaseProbability = 0.0, EffectId = "DoT" }, Base with { EffectType = "Heal", BaseProbability = 0.0, EffectId = "  " }],
            nid: 700,
            powerType: "GlobalBoost");
        BuildPowerAssembly source = Global(
        [
            PowerAssemblerTests.Grant() with { EffectId = "Root" },
            Base with { EffectType = "GlobalChanceMod", Reward = "DoT", Scale = 0.5, EffectClass = "Ignored" },
            Base with { EffectType = "GlobalChanceMod", Reward = "DoT", Scale = 0.25, EffectClass = "Ignored" },
        ]);
        BuildPowerAssembly other = Build(Power([Base with { EffectType = "GlobalChanceMod", Reward = "Root", Scale = 0.1 }], nid: 30), buildIndex: 2);
        BuildPowerAssembly second = Target(buildIndex: 3, nid: 11);
        Dictionary<int, IReadOnlyList<Effect>> output = BuildAll(new() { [0] = Target(), [1] = source, [2] = other, [3] = second }, [granted]);

        // The ids enter the table at the sum of the source's rewards for them, before any GlobalChanceMod: DoT at
        // 0.75 and Root at 0, so the grant it falls under reads 1, and Root keeps 0 though power 2 rewards it.
        // The second target finds both ids already entered.
        foreach (int target in new[] { 0, 3 })
        {
            (Effect damage, Effect heal) = (output[target][1], output[target][2]);
            Assert.Equal(("Damage", 0.75), (damage.EffectType, damage.Probability));
            Assert.Equal(("Heal", 0.0), (heal.EffectType, heal.Probability));
        }
    }

    [Fact]
    public void Global_chance_mods_raise_probability()
    {
        BuildPowerAssembly raised = Build(Base with { EffectType = "Regeneration", BaseProbability = 0.0, EffectId = "Regen" });
        BuildPowerAssembly rewards = Build(
            Power(
            [
                Base with { EffectType = "GlobalChanceMod", Reward = "Regen", Scale = 1.0 },
                Base with { EffectType = "GlobalChanceMod", Reward = "Regen", Scale = -1.0 },
                Base with { EffectType = "GlobalChanceMod", Reward = string.Empty, Scale = 1.0 },
                Base with { EffectType = "Defense", Reward = "Regen", Scale = -1.0 },
            ]),
            buildIndex: 1);
        BuildPowerAssembly inactive = Build(Power([Base with { EffectType = "GlobalChanceMod", Reward = "Regen", Scale = -1.0 }]), buildIndex: 2);
        Dictionary<int, IReadOnlyList<Effect>> output = BuildAll(new() { [0] = raised, [1] = rewards, [2] = inactive }, statInclude: new() { [0] = true, [1] = true, [2] = false });
        Assert.Equal(1.0, output[0][0].Probability);
    }

    [Fact]
    public void Set_bonus_chance_mods_follow_the_build_powers()
    {
        BuildPowerAssembly raised = Build(Base with { EffectType = "Regeneration", BaseProbability = 0.0, EffectId = "Regen" });
        BuildPowerAssembly other = Build(Power([Base with { EffectType = "Heal", BaseProbability = 0.0, EffectId = "Other" }]), buildIndex: 1);
        BuildPowerAssembly rewards = Build(Power([Base with { EffectType = "GlobalChanceMod", Reward = "Regen", Scale = 1.0 }]), buildIndex: 2);
        Effect[] bonus =
        [
            Base with { EffectType = "GlobalChanceMod", Reward = "Regen", Scale = -1.0 },
            Base with { EffectType = "GlobalChanceMod", Reward = "Other", Scale = 0.25 },
            Base with { EffectType = "GlobalChanceMod", Reward = "Other", Scale = 0.5 },
            Base with { EffectType = "Defense", Reward = "Other", Scale = 1.0 },
        ];
        Dictionary<int, IReadOnlyList<Effect>> output = BuildAll(new() { [0] = raised, [1] = other, [2] = rewards }, setBonusEffects: bonus);
        Assert.Equal((1.0, 0.25), (output[0][0].Probability, output[1][0].Probability));
    }

    [Fact]
    public void Pet_attributes_reach_the_assembled_power()
    {
        AssemblyPower pet = Power([], nid: 300, ignoreEnh: ["RechargeTime"]);
        AssemblyEntities entities = Entities(Entity(40, [30]), Powerset(30, 300));
        BuildPowerAssembly absorber = Build(Power([Base with { EffectType = "EntCreate", NSummon = 40, Duration = 30.0, Probability = 1.0 }], absorbAttributes: true));
        AssembledEffects assembled = PowerAssembler.AssemblePower(absorber, [], EnhDb, SetDb, new Dictionary<int, AssemblyPower> { [300] = pet }, entities, Class, Never);
        Assert.True(assembled.IgnoreEnh.SetEquals(["RechargeTime"]));
    }

    [Theory]
    [MemberData(nameof(ApplyCases))]
    public void Apply_assembly_hands_the_powers_their_assembled_state(string[] own, string[] absorbed, string[] want)
    {
        Power power = Make.Power(Base with { EffectType = "Defense" }) with { BuildIndex = 2, IgnoreEnh = own };
        BuildPowerAssembly entry = Build(Power([], aoeModifier: 1.5, baseRechargeTime: 8.0), buildIndex: 2);
        Effect[] effects = [Base with { EffectType = "Heal" }];
        AssembledEffects result = new(effects, absorbed.ToHashSet(StringComparer.Ordinal));
        Power output = Assert.Single(PowerAssembler.ApplyAssembly([power], new Dictionary<int, BuildPowerAssembly> { [2] = entry }, new Dictionary<int, AssembledEffects> { [2] = result }));
        Assert.Equal(effects, output.Effects);
        Assert.Equal(want, output.IgnoreEnh);
        Assert.Equal((1.5, 8.0), (output.AoeModifier, output.BaseRechargeTime));
    }

    private static Dictionary<int, IReadOnlyList<Effect>> BuildAll(
        Dictionary<int, BuildPowerAssembly> entries,
        AssemblyPower[]? powers = null,
        Dictionary<int, bool>? statInclude = null,
        Effect[]? setBonusEffects = null)
    {
        Dictionary<int, AssembledEffects> output = CrossPowerPass.AssembleBuild(
            entries,
            new Dictionary<int, IReadOnlyList<SlotRef>>(),
            statInclude ?? entries.Keys.ToDictionary(key => key, _ => true),
            EnhDb,
            SetDb,
            (powers ?? []).ToDictionary(power => power.Nid),
            NoEntities,
            Class,
            Never,
            setBonusEffects ?? []);
        return output.ToDictionary(entry => entry.Key, entry => entry.Value.Effects);
    }

    private static BuildPowerAssembly Target(int buildIndex = 0, int nid = 10, int[]? enhancements = null, string powerSetType = "Primary")
    {
        return Build(Power([Base with { EffectType = "Defense" }], nid: nid, enhancements: enhancements ?? [1, 2], powerSetType: powerSetType), buildIndex: buildIndex);
    }

    private static BuildPowerAssembly Global(Effect[] effects, string powerType = "GlobalBoost", string powerSetType = "Boost", int[]? enhancements = null, bool hasGrant = false)
    {
        return Build(Power(effects, nid: 20, powerType: powerType, powerSetType: powerSetType, enhancements: enhancements ?? [2], autoHit: ["Friend"], hasGrant: hasGrant), buildIndex: 1);
    }
}
