using CohEngine.Core.Data;
using CohEngine.Core.Maths;

using static CohEngine.Core.Tests.Assembly.AssemblyMake;

namespace CohEngine.Core.Tests.Assembly;

/// <summary>Port of <c>test_assembly.py</c>, steps 1 to 6: one build power assembled on hand-made records.</summary>
public sealed class PowerAssemblerTests
{
    private static readonly Effect Base = Make.Effect;

    public static TheoryData<bool, Func<Effect, Effect>> UnmetOverrides => new()
    {
        { false, effect => effect },
        { true, effect => effect with { Probability = 0.5 } },
        { true, effect => effect with { SpecialCase = "Domination" } },
        { true, effect => effect with { EffectType = "Defense" } },
    };

    public static TheoryData<string[][], int> PetsOnlyCopies => new()
    {
        // One copy per pet power with an effect of the same type, and one more when the last pet power has one.
        { [["Heal"], ["Damage"], ["Heal", "Damage"]], 3 },
        { [["Heal"], ["Heal"], ["Damage"]], 2 },
        { [], 0 },
    };

    public static TheoryData<Func<Effect, Effect>> CreatesNotAbsorbed => new()
    {
        effect => effect with { NSummon = -1 },
        effect => effect with { Probability = 0.5 },
        effect => effect with { EffectType = "Defense" },
    };

    public static TheoryData<string[], string> PetAttributes => new()
    {
        // Hitting only its caster, the power copies every pet power in turn; the last one affects only foes.
        { ["Caster"], "Target" },
        { [], "Target" },

        // Hitting others, it stops at the first, which affects everyone.
        { ["Foe"], "All" },
    };

    public static TheoryData<Func<Effect, Effect>> GrantNothing => new()
    {
        effect => effect with { EffectClass = "Ignored" },
        effect => effect with { NSummon = -1 },
        effect => effect with { EffectType = "Defense" },
    };

    public static TheoryData<string[], string, string, int> GrantedTargets => new()
    {
        { ["Friend"], "Click", "Target", 0 },
        { ["MyPet"], "Click", "Target", 0 },
        { ["Friend", "Caster"], "Click", "Self", 0 },
        { ["Caster"], "Auto_", "Self", 3 },
        { ["Caster"], "Toggle", "Self", 3 },
    };

    [Fact]
    public void Plain_power_keeps_its_effects_reindexed()
    {
        IReadOnlyList<Effect> output = Assemble(Build(Base with { Index = 4, EffectType = "Defense" }, Base with { Index = 9, EffectType = "Resistance" }));
        Assert.Equal(["Defense", "Resistance"], Types(output));
        Assert.Equal([0, 1], output.Select(effect => effect.Index));
    }

    [Fact]
    public void Met_override_refuses()
    {
        BuildPowerAssembly build = Build(Power([Base with { EffectType = "PowerRedirect" }], hasOverride: true));
        Assert.Contains("E88", Assert.Throws<InvalidDataException>(() => Assemble(build)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(UnmetOverrides))]
    public void Unmet_override_is_ignored(bool flag, Func<Effect, Effect> over)
    {
        Effect effect = over(Base with { EffectType = "PowerRedirect" });
        Assert.Single(Assemble(Build(Power([effect], hasOverride: flag))));
    }

    [Fact]
    public void Enhancement_effects_are_copied()
    {
        IReadOnlyList<Effect> output = Assemble(Build(Base), EnhPowers(), [Slot(50)]);
        Assert.Equal(["Defense", "Recovery", "Regeneration"], Types(output));
        Effect recovery = output[1];
        Assert.Equal((true, false, true, false), (recovery.IsEnhancementEffect, recovery.IgnoreScaling, recovery.AbsorbedEffect, recovery.Buffable));

        // Mids' copy of the effect does not carry Absorbed_Power_nID.
        Assert.Equal(("Target", 3, -1), (recovery.ToWho, recovery.Ticks, recovery.AbsorbedPowerNid));
    }

    [Fact]
    public void Proc_enhancement_with_boosts_drops_boost_tables_and_ignores_scaling()
    {
        IReadOnlyList<Effect> output = Assemble(Build(), EnhPowers(), [Slot(52)]);
        Assert.Equal(["Regeneration"], Types(output));
        Assert.True(output[0].IgnoreScaling);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(51)]
    [InlineData(53)]
    [InlineData(54)]
    [InlineData(55)]
    public void Enhancements_that_add_nothing(int enh)
    {
        Assert.Empty(Assemble(Build(), EnhPowers(), [Slot(enh)]));
    }

    [Fact]
    public void Proc_include_skips_proc_enhancements()
    {
        Assert.Empty(Assemble(Build(Power([]), procInclude: true), EnhPowers(), [Slot(52)]));
    }

    [Theory]
    [InlineData(57)]
    [InlineData(58)]
    [InlineData(59)]
    public void Enhancement_pointing_at_a_missing_record_refuses(int enh)
    {
        Assert.Contains("E90", Assert.Throws<InvalidDataException>(() => Assemble(Build(), EnhPowers(), [Slot(enh)])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Enhancement_grant_runs_the_grant_step()
    {
        IReadOnlyList<Effect> output = Assemble(Build(), EnhPowers(), [Slot(56)]);
        Assert.Equal(["GrantPower", "Heal"], Types(output));
        Assert.Equal("Ignored", output[0].EffectClass);
    }

    [Theory]
    [MemberData(nameof(PetsOnlyCopies))]
    public void Pets_only_enhancement_on_a_summon_copies_per_matching_pet_power(string[][] petPowers, int copies)
    {
        AssemblyPower[] powers = [Power([Base with { EffectType = "Heal", Reward = "Set_Bonus.Pet" }], nid: 500)];
        AssemblyEntity pet = Entity(41, [], uid: "Pets_Decoy", firstPowersetEffectTypes: petPowers);
        BuildPowerAssembly summon = Build(Base with { EffectType = "EntCreate", Probability = 0.5, Summon = "Pets_Decoy" });
        IReadOnlyList<Effect> output = Assemble(summon, powers, [Slot(60)], entities: Entities(pet));
        Assert.Equal(["EntCreate", .. Enumerable.Repeat("Heal", copies)], Types(output));
        Assert.All(output.Skip(1), effect => Assert.True(effect.IsEnhancementEffect && !effect.Buffable));
    }

    [Fact]
    public void Pets_only_enhancement_with_an_unknown_entity_refuses()
    {
        AssemblyPower[] powers = [Power([Base with { Reward = "Set_Bonus.Pet" }], nid: 500)];
        BuildPowerAssembly summon = Build(Base with { EffectType = "EntCreate", Probability = 0.5, Summon = "Pets_Missing" });
        Assert.Contains("E89", Assert.Throws<NotSupportedException>(() => Assemble(summon, powers, [Slot(60)])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pets_only_grant_runs_the_grant_step()
    {
        Effect grant = Base with { EffectType = "GrantPower", Reward = "Set_Bonus.Pet", NSummon = 600 };
        AssemblyPower[] powers = [Power([grant], nid: 500), Power([Base with { EffectType = "Heal" }], nid: 600)];
        AssemblyEntity pet = Entity(41, [], uid: "Pets_Decoy", firstPowersetEffectTypes: [["GrantPower"]]);
        BuildPowerAssembly summon = Build(Base with { EffectType = "EntCreate", Probability = 0.5, Summon = "Pets_Decoy" });
        IReadOnlyList<Effect> output = Assemble(summon, powers, [Slot(60)], entities: Entities(pet));
        Assert.Equal(["EntCreate", "GrantPower", "GrantPower", "Heal", "Heal"], Types(output));
    }

    [Theory]
    [InlineData(true, 50)]
    [InlineData(false, 60)]
    public void Set_bonus_reward_without_pets_only_is_copied(bool summon, int enh)
    {
        AssemblyPower[] powers = [Power([Base with { EffectType = "Heal", Reward = "Set_Bonus.Pet" }], nid: 500)];
        Effect[] own = summon ? [Base with { EffectType = "EntCreate", Probability = 0.5 }] : [];
        Assert.Equal("Heal", Types(Assemble(Build(own), powers, [Slot(enh)]))[^1]);
    }

    [Fact]
    public void Pet_effects_are_absorbed()
    {
        AssemblyPower[] pets =
        [
            Power([Base with { EffectType = "Damage", ToWho = "Target" }, Base with { EffectType = "Heal", DelayedTime = 0.5 }], nid: 600, powerType: "Auto_", activatePeriod: 2.0, affected: ["Foe"], autoHit: ["Foe"]),
            Power([Base with { EffectType = "Resistance" }], nid: 601),
            Power([Base with { EffectType = "Endurance" }], nid: 602, affected: ["Foe"]),
        ];
        AssemblyEntities entities = Entities(Entity(40, [70, -1, 71]), Powerset(70, 600, 601), Powerset(71, 602));
        BuildPowerAssembly build = Build(Power([Create() with { DelayedTime = 1.5 }], absorbEffects: true));
        IReadOnlyList<Effect> output = Assemble(build, pets, entities: entities);

        // 601 hits only its caster, so only its EntCreate effects would cross; it has none.
        Assert.Equal(["EntCreate", "Damage", "Heal", "Endurance"], Types(output));
        Effect damage = output[1];
        Assert.Equal((18, 600, "Auto_"), (damage.AbsorbedClassNId, damage.AbsorbedPowerNid, damage.AbsorbedPowerType));
        Assert.Equal((16, 1.5, "Target"), (damage.Ticks, damage.DelayedTime, damage.ToWho));
    }

    [Fact]
    public void Pet_creates_are_followed_one_level()
    {
        AssemblyPower[] pets =
        [
            Power([Create(41), Create(42), Create(43), Base with { EffectType = "EntCreate", NSummon = -1 }], nid: 600),
            Power([Base with { EffectType = "Heal" }], nid: 610, affected: ["Foe"]),
        ];
        AssemblyEntities entities = Entities(Entity(40, [70]), Entity(41, [71]), Entity(42, []), Entity(43, [-1]), Powerset(70, 600), Powerset(71, 610));
        IReadOnlyList<Effect> output = Assemble(Build(Power([Create()], absorbEffects: true)), pets, entities: entities);
        Assert.Equal([.. Enumerable.Repeat("EntCreate", 5), "Heal"], Types(output));
        Assert.Equal(18, output[^1].AbsorbedClassNId);
    }

    [Theory]
    [MemberData(nameof(CreatesNotAbsorbed))]
    public void Creates_that_are_not_absorbed(Func<Effect, Effect> over)
    {
        Assert.Single(Assemble(Build(Power([over(Create())], absorbEffects: true))));
    }

    [Fact]
    public void Absorption_needs_a_flag()
    {
        Assert.Single(Assemble(Build(Create())));
    }

    [Fact]
    public void Entity_without_powersets_adds_nothing()
    {
        BuildPowerAssembly build = Build(Power([Create()], absorbEffects: true, absorbAttributes: true));
        Assert.Single(Assemble(build, entities: Entities(Entity(40, []))));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Missing_entity_or_powerset_refuses(bool withEntity)
    {
        AssemblyEntities entities = withEntity ? Entities(Entity(40, [70])) : NoEntities;
        BuildPowerAssembly build = Build(Power([Create()], absorbEffects: true));
        Assert.Contains("E89", Assert.Throws<NotSupportedException>(() => Assemble(build, entities: entities)).Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(PetAttributes))]
    public void Pet_attributes_are_copied(string[] autoHit, string toWho)
    {
        AssemblyPower[] pets =
        [
            Power([], nid: 600, affected: ["Caster", "Friend", "Foe"], ignoreEnh: ["Range"]),
            Power([], nid: 601, affected: ["Foe"]),
            Power([Base with { EffectType = "Heal", ToWho = "Target" }], nid: 700),
        ];
        BuildPowerAssembly build = Build(Power([Create(), AllGrant()], absorbAttributes: true, hasGrant: true, autoHit: autoHit));
        IReadOnlyList<Effect> output = Assemble(build, pets, entities: Entities(Entity(40, [70]), Powerset(70, 600, 601)));
        Assert.Equal(toWho, output[^1].ToWho);
    }

    [Fact]
    public void Attributes_skip_an_empty_first_powerset()
    {
        AssemblyPower[] pets = [Power([Base with { EffectType = "Heal", ToWho = "Target" }], nid: 700)];
        BuildPowerAssembly build = Build(Power([Create(), AllGrant()], absorbAttributes: true, hasGrant: true));
        Assert.Equal("Target", Assemble(build, pets, entities: Entities(Entity(40, [-1])))[^1].ToWho);
    }

    [Fact]
    public void Pet_duration_rule()
    {
        AssemblyPower[] pets =
        [
            Power([Base with { EffectType = "Heal", NDuration = 99.0 }], nid: 600, powerSetType: "Pet", affected: []),
            Power(
                [
                    Base with { EffectType = "SilentKill", ToWho = "Self", DelayedTime = 20.0 },
                    Base with { EffectType = "SilentKill", ToWho = "Target", DelayedTime = 50.0 },
                    Base with { EffectType = "SilentKill", ToWho = "Self" },
                    Base with { EffectType = "Defense", ToWho = "Self", DelayedTime = 60.0 },
                ],
                nid: 601,
                powerSetType: "Pet",
                affected: []),
        ];
        AssemblyEntities entities = Entities(Entity(40, [70]), Powerset(70, -1, 600, 601));

        // The create's 30 s is longer than the pet's 20 s life, so the absorbed effects last 20 s.
        IReadOnlyList<Effect> output = Assemble(Build(Power([Create()], absorbEffects: true)), pets, entities: entities);
        Assert.Equal(Enumerable.Repeat(20.0, 5), output.Skip(1).Select(effect => effect.NDuration));
    }

    [Theory]
    [InlineData(0.0, 20.0)]
    [InlineData(10.0, 10.0)]
    public void Pet_duration_rule_keeps_a_shorter_create(double createDuration, double expected)
    {
        AssemblyPower[] pets =
        [
            Power([Base with { EffectType = "Heal", NDuration = 99.0 }, Base with { EffectType = "SilentKill", ToWho = "Self", DelayedTime = 20.0 }], nid: 600, powerSetType: "Pet", affected: []),
        ];
        AssemblyEntities entities = Entities(Entity(40, [70]), Powerset(70, 600));
        BuildPowerAssembly build = Build(Power([Create() with { Duration = createDuration }], absorbEffects: true));
        Assert.Equal(expected, Assemble(build, pets, entities: entities)[1].NDuration);
    }

    [Fact]
    public void Grant_is_replaced_by_the_granted_effects()
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal", ToWho = "Target", Ticks = 9 }, Base with { EffectType = "Endurance", ToWho = "Target" }], nid: 700, powerType: "Click");
        BuildPowerAssembly build = Build(Power([Grant() with { ToWho = "Self", IsEnhancementEffect = true }], hasGrant: true));
        IReadOnlyList<Effect> output = Assemble(build, [granted]);
        Assert.Equal(["GrantPower", "Heal", "Endurance"], Types(output));
        Assert.Equal("Ignored", output[0].EffectClass);
        (Effect heal, Effect endurance) = (output[1], output[2]);
        Assert.Equal((true, "Click", 700), (heal.AbsorbedEffect, heal.AbsorbedPowerType, heal.AbsorbedPowerNid));
        Assert.Equal((Class, "Target", 9, false), (heal.AbsorbedClassNId, heal.ToWho, heal.Ticks, heal.IsEnhancementEffect));

        // Only the last absorbed effect takes the grant's target and enhancement flag.
        Assert.Equal(("Self", true), (endurance.ToWho, endurance.IsEnhancementEffect));
    }

    [Fact]
    public void Grant_needs_the_power_flag()
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal" }], nid: 700);
        Assert.Equal(["GrantPower"], Types(Assemble(Build(Grant()), [granted])));
    }

    [Theory]
    [MemberData(nameof(GrantNothing))]
    public void Effects_that_grant_nothing(Func<Effect, Effect> over)
    {
        Assert.Single(Assemble(Build(Power([over(Grant())], hasGrant: true))));
    }

    [Theory]
    [MemberData(nameof(GrantedTargets))]
    public void Granted_power_target_and_ticks(string[] autoHit, string powerType, string toWho, int ticks)
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal" }, Base with { EffectType = "Heal" }], nid: 700, autoHit: autoHit, powerType: powerType, activatePeriod: 2.0);
        IReadOnlyList<Effect> output = Assemble(Build(Power([Grant()], hasGrant: true)), [granted]);
        Assert.Equal((toWho, ticks), (output[1].ToWho, output[1].Ticks));
    }

    [Fact]
    public void Granted_activate_period_zero_gives_no_ticks()
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal", Ticks = 4 }], nid: 700, powerType: "Auto_");
        Assert.Equal(0, Assemble(Build(Power([Grant()], hasGrant: true)), [granted])[1].Ticks);
    }

    [Fact]
    public void Last_effect_rewrites()
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal", ToWho = "Target" }], nid: 700, powerType: "Click");
        Effect grant = Grant() with { ToWho = "All", AbsorbedPowerNid = 12, AbsorbedPowerType = "Boost" };
        IReadOnlyList<Effect> output = Assemble(Build(Power([grant], hasGrant: true)), [granted]);
        Assert.Equal(("Boost", "Target"), (output[1].AbsorbedPowerType, output[1].ToWho));
    }

    [Fact]
    public void All_stays_all_when_the_power_affects_everyone()
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal" }], nid: 700);
        BuildPowerAssembly build = Build(Power([Grant() with { ToWho = "All" }], hasGrant: true, affected: ["Caster", "Friend", "Foe"]));
        Assert.Equal("All", Assemble(build, [granted])[1].ToWho);
    }

    [Fact]
    public void Nested_grants_run_in_rounds()
    {
        AssemblyPower middle = Power([Base with { EffectType = "Heal" }, Grant() with { NSummon = 701, ToWho = "Target" }], nid: 700);
        AssemblyPower inner = Power([Base with { EffectType = "Endurance" }], nid: 701);
        IReadOnlyList<Effect> output = Assemble(Build(Power([Grant()], hasGrant: true)), [middle, inner]);
        Assert.Equal(["GrantPower", "Heal", "GrantPower", "Endurance"], Types(output));

        // A granted GrantPower keeps its own target.
        Assert.Equal("Target", output[2].ToWho);
        Assert.Equal(["Ignored", "Ignored"], [.. output.Where(effect => string.Equals(effect.EffectType, "GrantPower", StringComparison.Ordinal)).Select(effect => effect.EffectClass)]);
    }

    [Fact]
    public void Grant_cycle_stops_after_a_hundred_rounds()
    {
        AssemblyPower loop = Power([Grant()], nid: 700);
        IReadOnlyList<Effect> output = Assemble(Build(Power([Grant()], hasGrant: true)), [loop]);
        Assert.Equal(101, output.Count);
        Assert.Equal("Primary", output[^1].EffectClass);
    }

    [Fact]
    public void Grant_probability_below_one_multiplies()
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal", Probability = 0.5, BaseProbability = 0.5 }], nid: 700);
        Effect grant = Grant() with { Probability = 0.4, BaseProbability = 0.4 };
        IReadOnlyList<Effect> output = Assemble(Build(Power([grant], hasGrant: true)), [granted]);
        Assert.Equal(F32.Round(0.5 * 0.4), output[1].Probability);
        Assert.Equal(output[1].Probability, output[1].BaseProbability);
    }

    [Fact]
    public void Ppm_and_expression_probability_are_not_overwritten()
    {
        AssemblyPower granted = Power(
            [
                Base with { EffectType = "Heal", Probability = 0.5, ProcsPerMinute = 3.0 },
                Base with { EffectType = "Heal", Probability = 0.5, AttribType = "Expression", ExprProbability = "1" },
            ],
            nid: 700);
        Effect grant = Grant() with { Probability = 0.4, BaseProbability = 0.4 };
        IReadOnlyList<Effect> output = Assemble(Build(Power([grant, grant], hasGrant: true)), [granted]);
        Assert.Equal(Enumerable.Repeat(0.5, 4), output.Skip(2).Select(effect => effect.Probability));
        Assert.Equal(F32.Round(0.5 * 0.4), output[3].BaseProbability);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Grant_conditions_are_asked(bool met)
    {
        AssemblyPower granted = Power([Base with { EffectType = "Heal" }], nid: 700);
        Effect grant = Grant() with { Conditionals = [Make.Condition("Active:Pool.Fighting.Boxing", "True")], ActiveConditionalsCount = 1 };
        List<Effect> asked = [];
        IReadOnlyList<Effect> output = Assemble(Build(Power([grant], hasGrant: true)), [granted], conditions: effect =>
        {
            asked.Add(effect);
            return met;
        });
        Assert.Equal(grant, asked[0]);
        Assert.Equal(met ? 2 : 1, output.Count);
    }

    [Fact]
    public void Grant_special_case_refuses()
    {
        BuildPowerAssembly build = Build(Power([Grant() with { SpecialCase = "Domination" }], hasGrant: true));
        Assert.Contains("E91", Assert.Throws<NotSupportedException>(() => Assemble(build)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_granted_power_refuses()
    {
        Assert.Contains("E90", Assert.Throws<InvalidDataException>(() => Assemble(Build(Power([Grant()], hasGrant: true)))).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Pet_power_without_its_powerset_refuses()
    {
        AssemblyPower granted = Power([Base], nid: 700, powerSetType: "Pet");
        Assert.Contains("E89", Assert.Throws<NotSupportedException>(() => Assemble(Build(Power([Grant()], hasGrant: true)), [granted])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Execute_is_replaced_in_place()
    {
        AssemblyPower executed = Power([Base with { EffectType = "Heal" }, Base with { EffectType = "Endurance" }], nid: 800, fullName: "Executed");
        BuildPowerAssembly build = Build(Base with { EffectType = "Defense" }, Execute(), Base with { EffectType = "Recovery" });
        Assert.Equal(["Defense", "Heal", "Endurance", "Recovery"], Types(Assemble(build, [executed])));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Unknown")]
    public void Execute_without_a_power_is_dropped(string summon)
    {
        Assert.Empty(Assemble(Build(Execute(summon))));
    }

    [Fact]
    public void Executed_effects_inherit_from_the_caller()
    {
        AssemblyPower executed = Power(
            [
                Base with { EffectType = "Heal", NDuration = 3.0, DelayedTime = 0.25, BaseProbability = 0.9, Conditionals = [Make.Condition("Active:A", "True")], ActiveConditionalsCount = 1 },
                Base with { EffectType = "Heal", BaseProbability = 0.3, Ticks = 2 },
            ],
            nid: 800,
            fullName: "Executed");
        Effect caller = Execute() with { NDuration = 10.0, DelayedTime = 0.5, BaseProbability = 0.6, Conditionals = [Make.Condition("Active:B", "True")], ActiveConditionalsCount = 1, Ticks = 7 };
        IReadOnlyList<Effect> output = Assemble(Build(caller), [executed]);
        (Effect first, Effect second) = (output[0], output[1]);
        Assert.Equal((10.0, 0.75, 0.6, 0.6), (first.NDuration, first.DelayedTime, first.Probability, first.BaseProbability));
        Assert.Equal([Make.Condition("Active:A", "True"), Make.Condition("Active:B", "True")], first.Conditionals);
        Assert.Equal((2, 7), (first.ActiveConditionalsCount, first.Ticks));
        Assert.Equal((0.3, 2), (second.Probability, second.Ticks));
        Assert.Equal(2, output.Count);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(1.0)]
    public void Executed_probability_kept_for_a_certain_or_zero_caller(double callerProbability)
    {
        AssemblyPower executed = Power([Base with { EffectType = "Heal", NDuration = 3.0, BaseProbability = 0.4 }], nid: 800, fullName: "Executed");
        Effect output = Assert.Single(Assemble(Build(Execute() with { BaseProbability = callerProbability }), [executed]));
        Assert.Equal((0.4, 3.0), (output.Probability, output.NDuration));
    }

    [Fact]
    public void Execute_cycle_stops_at_depth_five()
    {
        AssemblyPower loop = Power([Base with { EffectType = "Heal" }, Execute()], nid: 800, fullName: "Executed");
        Assert.Equal([.. Enumerable.Repeat("Heal", 5)], Types(Assemble(Build(Execute()), [loop])));
    }

    [Fact]
    public void Stat_include_sub_powers_are_appended()
    {
        AssemblyPower sub = Power([Base with { EffectType = "Heal" }], nid: 900, powerType: "Toggle");
        BuildPowerAssembly build = Build(Power([Base]), subPowers: [new SubPower(900, true), new SubPower(900, false), new SubPower(-1, true)]);
        IReadOnlyList<Effect> output = Assemble(build, [sub]);
        Assert.Equal(["Defense", "Heal"], Types(output));
        Assert.Equal((true, 900, "Toggle"), (output[1].AbsorbedEffect, output[1].AbsorbedPowerNid, output[1].AbsorbedPowerType));
    }

    [Fact]
    public void Missing_sub_power_refuses()
    {
        BuildPowerAssembly build = Build(Power([]), subPowers: [new SubPower(900, true)]);
        Assert.Contains("E90", Assert.Throws<InvalidDataException>(() => Assemble(build)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_never_condition_fails_loud()
    {
        Assert.Throws<InvalidOperationException>(() => Never(Base));
    }

    internal static Effect Grant()
    {
        return Base with { EffectType = "GrantPower", NSummon = 700, Summon = "Granted", Duration = 5.0 };
    }

    private static Effect Create(int entity = 40)
    {
        return Base with { EffectType = "EntCreate", NSummon = entity, Duration = 30.0 };
    }

    private static Effect AllGrant()
    {
        return Base with { EffectType = "GrantPower", NSummon = 700, ToWho = "All" };
    }

    private static Effect Execute(string summon = "Executed")
    {
        return Base with { EffectType = "ExecutePower", Summon = summon };
    }

    private static AssemblyPower[] EnhPowers()
    {
        return
        [
            Power(
                [
                    Base with { EffectType = "Recovery", ModifierTable = "Melee_Boosts_Rec", ToWho = "Target", Ticks = 3, AbsorbedPowerNid = 4 },
                    Base with { EffectType = "Regeneration", ModifierTable = "Melee_Ones" },
                ],
                nid: 500,
                fullName: "Boosts.Test.Test"),
            Power([Base with { EffectType = "GrantPower", NSummon = 700 }], nid: 501),
            Power([Base with { EffectType = "Heal" }], nid: 700, powerType: "Click"),
        ];
    }
}
