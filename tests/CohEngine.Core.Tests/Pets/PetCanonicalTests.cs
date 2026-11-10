using System.Text.Json;
using System.Text.Json.Nodes;

using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Pets;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Pets;

// Ported from tests/test_pet_damage.py: the real-data cases (Spiderlings, Soldiers, Phantom Army) and the synthetic-tree cases.
public sealed class PetCanonicalTests
{
    private const string Spiderlings = "Training_Gadgets.Crab_Spider_Training.Summon_Spiderlings";
    private const string Soldiers = "Mastermind_Summon.Mercenaries.Soldiers";
    private const string Claw = "Villain_Pets.Arachnobot_Spiderling.Claw";
    private const string PhantomArmy = "Controller_Control.Illusion_Control.Decoy";
    private const string Summon = "Tst.Set.Summon";

    private static readonly string Canonical = Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical");
    private static readonly EnduranceBudget Unlimited = new(1.0e9, 0.0, 0.0);
    private static readonly Movement Walk = new(20.0, 40.0);

    public static TheoryData<string, string> UnusableSummons => new()
    {
        { """{"templates":[]}""", "has no Create_Entity template" },
        { Create("""{"params":{"entity_def":null}}"""), "no params.entity_def" },
        { Create("""{"params":null}"""), "no params.entity_def" },
        { Create("""{"duration":"5 minutes"}"""), "duration is '5 minutes'" },
        { Create("""{"duration":"abc seconds"}"""), "duration is 'abc seconds'" },
    };

    public static TheoryData<string> BadDurations => ["5 minutes", "abc seconds"];

    public static TheoryData<string> NoAttackNames => ["[]", "[\"Tst.Set.Aura\"]"];

    private static string Create(string overrides)
    {
        JsonObject template = Template(["Create_Entity"]);
        foreach (KeyValuePair<string, JsonNode?> pair in JsonNode.Parse(overrides)!.AsObject())
        {
            template[pair.Key] = pair.Value?.DeepClone();
        }

        return Effect(template).ToJsonString();
    }

    private static JsonObject Template(string[] attribs, string aspect = "Current", string target = "AnyAffected", string table = "Ranged_Ones", double scale = 1.0, double magnitude = 1.0, string duration = "UntilKilled", double period = 0.0, bool entity = true)
    {
        JsonObject template = new()
        {
            ["attribs"] = new JsonArray([.. attribs.Select(attrib => (JsonNode?)attrib)]),
            ["aspect"] = aspect,
            ["target"] = target,
            ["table"] = table,
            ["scale"] = scale,
            ["magnitude"] = magnitude,
            ["duration"] = duration,
            ["application_period"] = period,
            ["params"] = entity ? new JsonObject { ["entity_def"] = "Tst_Pet" } : null,
        };
        return template;
    }

    private static JsonObject Effect(JsonObject template, double chance = 1.0, double ppm = 0.0, string requires = "", string pvp = "EITHER", JsonArray? children = null)
    {
        return new JsonObject
        {
            ["chance"] = chance,
            ["ppm"] = ppm,
            ["requires_expression"] = requires,
            ["is_pvp"] = pvp,
            ["templates"] = new JsonArray(template),
            ["child_effects"] = children ?? [],
        };
    }

    private static JsonObject Damage(string duration = "UntilKilled", double period = 0.0, double scale = 1.0, double magnitude = 1.0, string target = "AnyAffected", string table = "Ranged_Damage")
    {
        return Template(["Smashing_Dmg"], "Absolute", target, table, scale, magnitude, duration, period, entity: false);
    }

    private static JsonObject PowerJson(string name, IEnumerable<JsonObject> effects, string type = "Click", string area = "SingleTarget", double range = 7.0)
    {
        return new JsonObject
        {
            ["full_name"] = name,
            ["type"] = type,
            ["effect_area"] = area,
            ["range"] = range,
            ["radius"] = 0.0,
            ["arc"] = 0.0,
            ["max_targets_hit"] = 0,
            ["allowed_boostset_cats"] = new JsonArray("Pet Damage"),
            ["activation_time"] = 1.0,
            ["recharge_time"] = 2.0,
            ["activate_period"] = 0.0,
            ["endurance_cost"] = 5.0,
            ["effects"] = new JsonArray([.. effects.Select(effect => (JsonNode?)effect)]),
        };
    }

    private static void WritePower(TempTree tree, string name, JsonObject power)
    {
        tree.Write("powers/" + name.ToLowerInvariant().Replace('.', '/') + ".json", power.ToJsonString());
    }

    private static ChainAttack RealSummon(string name)
    {
        JsonElement power = CanonicalJson.LoadCanonicalPowers(Path.Join(Canonical, "powers"), [name])[name];
        return new ChainAttack { FullName = name, Damage = 0.0, CastTime = power.ReqNum("activation_time"), RechargeTime = power.ReqNum("recharge_time"), EndCost = power.ReqNum("endurance_cost"), Profile = AttackProfiles.Classify(power) };
    }

    private static IReadOnlyList<PetSummon> Build(string name, int casterLevel = 50, IReadOnlyDictionary<string, double>? enhancement = null)
    {
        return PetDamage.BuildPetSummons([RealSummon(name)], Canonical, Fixtures.Store.AttribMods, Fixtures.Store.Archetypes, casterLevel, enhancement);
    }

    private static Dictionary<string, double> NoEnhancement(string name)
    {
        return new Dictionary<string, double>(StringComparer.Ordinal) { [name] = 0.0 };
    }

    private static ChainAttack SyntheticSummon(TempTree tree, IEnumerable<JsonObject> summonEffects, bool entity = true, bool pets = true)
    {
        WritePower(tree, Summon, PowerJson(Summon, summonEffects, area: "Location", range: 60.0));
        if (entity)
        {
            tree.Write("entities/tst_pet.json", """{"name":"Tst_Pet","defaults":{"character_class_name":"minion_pets","power_full_names":["Tst.Set.Zap","Tst.Set.Aura"]},"levels":[]}""");
        }

        if (pets)
        {
            JsonObject[] zap =
            [
                Effect(Damage("1 seconds", 0.5)),
                Effect(Damage(), requires: "Source.Mode?(x)"),
                Effect(Damage(), ppm: 3.5),
                Effect(Damage(), pvp: "PVP_ONLY"),
                Effect(Damage("UntilKilled", 0.5)),
                Effect(Damage(), children: [Effect(Damage(), pvp: "PVP_ONLY")]),
                Effect(Template(["Smashing_Dmg"], "Absolute", "Self", entity: false)),
            ];
            WritePower(tree, "Tst.Set.Zap", PowerJson("Tst.Set.Zap", zap));
            WritePower(tree, "Tst.Set.Aura", PowerJson("Tst.Set.Aura", [Effect(Damage())], "Toggle"));
        }

        AttackProfile profile = new(Summon, Delivery.Ranged, Shape.Location, Role.PetSummon, 60.0, 0.0, 0.0, 0, [], []);
        return new ChainAttack { FullName = Summon, Damage = 0.0, CastTime = 1.0, RechargeTime = 100.0, EndCost = 5.0, Profile = profile };
    }

    private static IReadOnlyList<PetSummon> BuildSynthetic(TempTree tree, ChainAttack summon, AttribMods? mods = null, ArchetypeDb? classes = null, int casterLevel = 50, IReadOnlyDictionary<string, double>? enhancement = null)
    {
        return PetDamage.BuildPetSummons([summon], tree.Root, mods ?? Fixtures.Store.AttribMods, classes ?? Fixtures.Store.Archetypes, casterLevel, enhancement);
    }

    private static JsonObject CreateEntity(string table = "Ranged_Ones", string duration = "UntilKilled")
    {
        return Template(["Create_Entity"], table: table, duration: duration);
    }

    private static DamageContext Context(Scenario? scenario = null, params PetSummon[] pets)
    {
        return new DamageContext { Attacks = [], Budget = Unlimited, Stance = Stance.Melee, Scenario = scenario ?? new Scenario(1), Movement = Walk, ArchetypeClass = "Class_Test", Pets = pets };
    }

    private static ChainAttack PetAttack(string name, double damage, double cast, double recharge, Shape shape = Shape.SingleTarget)
    {
        AttackProfile profile = new(name, Delivery.Melee, shape, Role.Chain, 7.0, 0.0, 0.0, 0, [], []);
        return new ChainAttack { FullName = name, Damage = damage, CastTime = cast, RechargeTime = recharge, EndCost = 0.0, Profile = profile };
    }

    private static PetSummon Pet(double duration, int count = 2, double cast = 2.03, double recharge = 100.0, double end = 26.0)
    {
        return new PetSummon { SummonName = "S", PetName = "P", PetAttacks = [PetAttack("P.Hit", 100.0, 1.0, 1.0)], PetCount = count, Duration = duration, SummonCastTime = cast, SummonRecharge = recharge, SummonEndCost = end, DamageBuffBaseline = 1.95 };
    }

    private static double SingleDps(PetSummon pet)
    {
        return AttackChains.Plan(pet.PetAttacks, Stance.Melee, Walk, new Scenario(), Unlimited).BestOverall!.Dps;
    }

    private static string Message(Action action)
    {
        return Assert.Throws<EngineException>(action).Message;
    }

    [Fact]
    public void Spiderlings_resolve_from_canonical_data()
    {
        PetSummon pet = Assert.Single(Build(Spiderlings));

        Assert.Equal("Spiderling 3 240 600 26", $"{pet.PetName} {pet.PetCount} {pet.Duration} {pet.SummonRecharge} {pet.SummonEndCost}");
        Near.Rel(2.03, pet.SummonCastTime);
        Near.Rel(1.0 + PetDamage.DefaultPetDamageEnhancement, pet.DamageBuffBaseline);
        Assert.Contains("level 48", pet.Label, StringComparison.Ordinal);
        Assert.Equal("Villain_Pets.Arachnobot_Spiderling.Chain_Gun,Villain_Pets.Arachnobot_Spiderling.Claw", string.Join(',', pet.PetAttacks.Select(attack => attack.FullName).Order(StringComparer.Ordinal)));
        Assert.All(pet.PetAttacks, attack => Assert.True(attack.EndCost == 0.0 && attack.Profile.Role == Role.Chain));
    }

    [Fact]
    public void Claw_damage_is_scale_times_the_table_value_times_one_plus_the_enhancement()
    {
        ChainAttack claw = Assert.Single(Build(Spiderlings)).PetAttacks.First(attack => string.Equals(attack.FullName, Claw, StringComparison.Ordinal));

        Near.Rel(0.68 * 55.61 * 1.95, claw.Damage, 1e-5);
        Assert.Equal((0.67, 2.0), (claw.CastTime, claw.RechargeTime));
    }

    [Fact]
    public void An_enhancement_override_and_the_tick_count_scale_chain_gun()
    {
        PetSummon pet = Assert.Single(Build(Spiderlings, enhancement: NoEnhancement(Spiderlings)));
        ChainAttack gun = pet.PetAttacks.First(attack => attack.FullName.EndsWith("Chain_Gun", StringComparison.Ordinal));

        Near.Rel(0.28 * 44.488 * 4, gun.Damage, 1e-5);
        Assert.Equal(1.0, pet.DamageBuffBaseline);
    }

    [Fact]
    public void Pet_level_follows_the_caster_level_and_ones_tables()
    {
        Assert.Contains("level 28", Assert.Single(Build(Spiderlings, 30)).Label, StringComparison.Ordinal);
        Assert.All(Build(Soldiers, 40), pet => Assert.Contains("level 40", pet.Label, StringComparison.Ordinal));
    }

    [Fact]
    public void Mastermind_soldiers_resolve_two_entities_with_the_cast_charged_once()
    {
        IReadOnlyList<PetSummon> pets = Build(Soldiers);
        PetSummon soldier = pets[0];
        PetSummon medic = pets[1];

        Assert.Equal("Soldier 2 True Medic 1", $"{soldier.PetName} {soldier.PetCount} {double.IsPositiveInfinity(soldier.Duration)} {medic.PetName} {medic.PetCount}");
        Assert.True(soldier.SummonCastTime > 0 && medic.SummonCastTime == 0.0 && soldier.SummonEndCost > 0 && medic.SummonEndCost == 0.0);
        Assert.Equal("5 5", $"{soldier.SummonRecharge} {medic.SummonRecharge}");
        Assert.Equal("Mastermind_Pets.Soldier.ASMG_Burst,Mastermind_Pets.Soldier.Brawl", string.Join(',', soldier.PetAttacks.Select(attack => attack.FullName).Order(StringComparer.Ordinal)));
        Assert.All(pets.SelectMany(pet => pet.PetAttacks), attack => Assert.True(attack.Damage > 0));
        Assert.DoesNotContain(medic.PetAttacks, attack => attack.FullName.EndsWith("Med_Kit", StringComparison.Ordinal));
        Near.Rel(0.29 * 26.088 * 3 * 1.95, soldier.PetAttacks.First(attack => attack.FullName.EndsWith("ASMG_Burst", StringComparison.Ordinal)).Damage, 1e-5);
    }

    [Fact]
    public void A_timed_pets_uptime_and_amortisation()
    {
        PetSummon pet = Pet(240.0, 3, recharge: 600.0);
        SourceResult result = PetDamage.PetSource(Context(pets: pet))!;
        double cycle = AttackChains.ArcanaCastTime(2.03) + 600.0;
        double uptime = 240.0 / cycle;

        Assert.Equal("pet P", $"{result.Source} {result.Detail[0].Name}");
        Near.Rel(SingleDps(pet) * 3 * uptime, result.Dps, 1e-5);
        Near.Rel(AttackChains.ArcanaCastTime(2.03) / cycle, result.TimelineShare, 1e-5);
        Near.Rel(26.0 / cycle, result.EndPerSec, 1e-5);
        Assert.Equal(result.Dps, result.Detail[0].Dps);
        Near.Rel(1.95, result.DamageBuffBaseline!.Value);
        Assert.Contains("approximate", result.Label, StringComparison.Ordinal);
        Assert.Contains($"P x3 uptime {PyFloat.Percent(uptime, 0)}", result.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void A_permanent_pets_label_says_permanent()
    {
        Assert.Contains("P x2 uptime 100% (permanent)", PetDamage.PetSource(Context(pets: Pet(double.PositiveInfinity)))!.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void A_long_lived_timed_pet_is_capped_at_full_uptime()
    {
        PetSummon pet = Pet(1000.0);

        SourceResult result = PetDamage.PetSource(Context(pets: pet))!;

        Near.Rel(SingleDps(pet) * 2, result.Dps, 1e-5);
        Assert.True(result.TimelineShare > 0);
    }

    [Fact]
    public void A_permanent_pet_has_full_uptime_and_no_timeline_cost()
    {
        PetSummon pet = Pet(double.PositiveInfinity);

        SourceResult result = PetDamage.PetSource(Context(pets: pet))!;

        Near.Rel(SingleDps(pet) * 2, result.Dps, 1e-5);
        Assert.Equal((0.0, 0.0), (result.TimelineShare, result.EndPerSec));
    }

    [Fact]
    public void A_zero_cast_summon_adds_no_timeline_share()
    {
        SourceResult result = PetDamage.PetSource(Context(pets: Pet(240.0, cast: 0.0, end: 0.0)))!;

        Assert.Equal(0.0, result.TimelineShare);
        Near.Rel(SingleDps(Pet(240.0)) * 2, result.Dps, 1e-5);
    }

    [Fact]
    public void The_baseline_is_damage_weighted()
    {
        PetSummon weak = Pet(double.PositiveInfinity, 1);
        PetSummon strong = new() { SummonName = "S2", PetName = "Q", PetAttacks = [PetAttack("Q.Hit", 300.0, 1.0, 1.0)], PetCount = 1, Duration = double.PositiveInfinity, SummonCastTime = 0.0, SummonRecharge = 5.0, SummonEndCost = 0.0, DamageBuffBaseline = 1.0 };

        SourceResult result = PetDamage.PetSource(Context(pets: [weak, strong]))!;
        double weakDps = result.Detail[0].Dps;
        double strongDps = result.Detail[1].Dps;

        Near.Rel(((weakDps * 1.95) + (strongDps * 1.0)) / (weakDps + strongDps), result.DamageBuffBaseline!.Value);
    }

    [Fact]
    public void A_pet_without_attacks_contributes_nothing()
    {
        PetSummon support = new() { SummonName = "S", PetName = "Medic", PetAttacks = [], PetCount = 1, Duration = double.PositiveInfinity, SummonCastTime = 0.0, SummonRecharge = 5.0, SummonEndCost = 0.0 };

        SourceResult result = PetDamage.PetSource(Context(pets: support))!;

        Assert.Equal((0.0, 1.0), (result.Dps, result.DamageBuffBaseline));
    }

    [Fact]
    public void No_pets_still_reports_the_pet_layer_at_zero()
    {
        SourceResult result = PetDamage.PetSource(Context())!;

        Assert.Equal("0 0 0 0 no pets summoned", $"{result.Dps} {result.TimelineShare} {result.EndPerSec} {result.Detail.Count} {result.Label}");
    }

    [Fact]
    public void Spiderlings_run_end_to_end_through_the_evaluator()
    {
        DamageReport report = DamageModel.Evaluate(Context(pets: [.. Build(Spiderlings)]), [new(PetDamage.PetSource, FillsRemainder: false)], []);
        SourceResult result = Assert.Single(report.Sources);

        Assert.True(result.Dps > 0);
        Near.Rel(AttackChains.ArcanaCastTime(2.03) / (AttackChains.ArcanaCastTime(2.03) + 600.0), result.TimelineShare, 1e-5);
        Near.Rel(result.Dps, report.TotalDps);
        Near.Rel(result.Dps, report.ScaledDps.Single(pair => string.Equals(pair.Key, "pet", StringComparison.Ordinal)).Value);
    }

    [Fact]
    public void Mastermind_soldiers_are_permanent_and_cost_no_timeline()
    {
        IReadOnlyList<PetSummon> pets = Build(Soldiers);

        SourceResult result = PetDamage.PetSource(Context(pets: [pets[0], pets[1]]))!;

        Assert.True(result.Dps > 0 && result.TimelineShare == 0.0);
        Assert.Equal("Soldier,Medic", string.Join(',', result.Detail.Select(item => item.Name)));
    }

    [Fact]
    public void A_synthetic_pet_counts_ticks_and_skips_non_attacks()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);

        PetSummon pet = Assert.Single(BuildSynthetic(tree, summon, enhancement: NoEnhancement(Summon)));

        Assert.Equal("Tst_Pet True 1", $"{pet.PetName} {double.IsPositiveInfinity(pet.Duration)} {pet.PetCount}");
        Near.Rel(44.488 * 5, Assert.Single(pet.PetAttacks).Damage, 1e-5);
    }

    [Fact]
    public void Phantom_army_counts_one_costume_group_and_nets_its_heal_back()
    {
        PetSummon decoy = Assert.Single(Build(PhantomArmy, enhancement: NoEnhancement(PhantomArmy)));
        ChainAttack sword = decoy.PetAttacks.First(attack => string.Equals(attack.FullName, "Pets.Decoy.Fire_Sword", StringComparison.Ordinal));
        JsonElement entity = CanonicalJson.ReadJson(Path.Join(Canonical, "entities", "pets_decoy.json"), ErrorCodes.E38);
        string className = "Class_" + entity.Req("defaults").ReqStr("character_class_name");
        int petClass = Fixtures.Store.Archetypes.NidFromUidClass(className);
        double melee = Math.Abs(CohEngine.Core.Totals.EffectMagnitude.Modifier(Fixtures.Store.AttribMods, Fixtures.Store.Archetypes.Classes, petClass, Fixtures.Store.AttribMods.NidFromUidAttribMod("Melee_Damage"), 49));

        Assert.Equal(3, decoy.PetCount);
        Near.Rel(melee * (1.32 - 1.02), sword.Damage, 1e-5);
    }

    [Fact]
    public void Level_gated_pets_spawn_only_once_the_caster_reaches_the_level()
    {
        PetSummon low = Assert.Single(Build(Soldiers, 5));
        IReadOnlyList<PetSummon> high = Build(Soldiers, 18);

        Assert.Equal("Soldier 1 2 1", $"{low.PetName} {low.PetCount} {high[0].PetCount} {high[1].PetCount}");
    }

    [Fact]
    public void A_group_gated_only_on_the_critter_test_counts_while_other_conditions_and_heals_do_not()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);
        JsonObject[] zap =
        [
            Effect(Damage()),
            Effect(Damage(scale: -0.5), requires: "target>enttype eq 'critter'"),
            Effect(Damage(), requires: "Source.Mode?(x)"),
            Effect(Template(["Heal_Dmg"], "Absolute", table: "Ranged_Damage", entity: false)),
        ];
        WritePower(tree, "Tst.Set.Zap", PowerJson("Tst.Set.Zap", zap));

        PetSummon pet = Assert.Single(BuildSynthetic(tree, summon, enhancement: NoEnhancement(Summon)));

        Near.Rel(44.488 * 0.5, Assert.Single(pet.PetAttacks).Damage, 1e-5);
    }

    [Fact]
    public void A_malformed_pet_power_fails_loud_rather_than_being_skipped()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);
        WritePower(tree, "Tst.Set.Zap", PowerJson("Tst.Set.Zap", [Effect(Damage())], area: "Map"));

        Assert.StartsWith(ErrorCodes.E36, Message(() => BuildSynthetic(tree, summon)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_timed_duration_text_is_parsed()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity(duration: "90 seconds"))]);

        Assert.Equal(90.0, Assert.Single(BuildSynthetic(tree, summon)).Duration);
    }

    [Fact]
    public void A_non_pet_power_is_rejected_as_e64()
    {
        ChainAttack real = RealSummon("Training_Gadgets.Training_and_Gadgets.Call_Reinforcements");
        ChainAttack wrong = real with { Profile = real.Profile with { Role = Role.Chain } };

        Assert.StartsWith("E64:", Message(() => PetDamage.BuildPetSummons([wrong], Canonical, Fixtures.Store.AttribMods, Fixtures.Store.Archetypes)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_entity_file_is_e60()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())], entity: false, pets: false);

        Assert.StartsWith("E60: entity Tst_Pet", Message(() => BuildSynthetic(tree, summon)), StringComparison.Ordinal);
    }

    [Fact]
    public void Missing_pet_power_files_are_e60()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())], pets: false);

        Assert.StartsWith("E60: pet powers of Tst_Pet missing", Message(() => BuildSynthetic(tree, summon)), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_class_is_e61()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);

        Assert.StartsWith("E61: entity Tst_Pet has class 'minion_pets'", Message(() => BuildSynthetic(tree, summon, classes: new ArchetypeDb([]))), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_table_is_e61()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity("Ranged_Levelminus2"))]);

        Assert.StartsWith("E61: Tst.Set.Summon uses modifier table 'Ranged_Levelminus2'", Message(() => BuildSynthetic(tree, summon, new AttribMods([]))), StringComparison.Ordinal);
    }

    [Fact]
    public void A_pet_level_below_one_is_e61()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity("Ranged_Levelminus2"))]);

        Assert.StartsWith("E61: table Ranged_Levelminus2 gives pet level 0", Message(() => BuildSynthetic(tree, summon, casterLevel: 0)), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(UnusableSummons))]
    public void Unusable_summon_data_is_e62(string effect, string message)
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [JsonNode.Parse(effect)!.AsObject()], entity: false, pets: false);

        string error = Message(() => BuildSynthetic(tree, summon));

        Assert.True(error.StartsWith("E62: ", StringComparison.Ordinal) && error.Contains(message, StringComparison.Ordinal), error);
    }

    [Theory]
    [MemberData(nameof(BadDurations))]
    public void An_unparseable_damage_duration_is_e62(string text)
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);
        WritePower(tree, "Tst.Set.Zap", PowerJson("Tst.Set.Zap", [Effect(Damage(text))]));

        Assert.StartsWith($"{ErrorCodes.E62}: Tst.Set.Zap damage template duration is '{text}'", Message(() => BuildSynthetic(tree, summon)), StringComparison.Ordinal);
    }

    [Fact]
    public void An_empty_damage_duration_is_a_single_tick()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);
        WritePower(tree, "Tst.Set.Zap", PowerJson("Tst.Set.Zap", [Effect(Damage(string.Empty))]));

        Near.Rel(44.488, BuildSynthetic(tree, summon, enhancement: NoEnhancement(Summon))[0].PetAttacks[0].Damage, 1e-5);
    }

    [Fact]
    public void Chance_and_magnitude_scale_template_damage()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);
        WritePower(tree, "Tst.Set.Zap", PowerJson("Tst.Set.Zap", [Effect(Damage(magnitude: 2.0), chance: 0.5)]));

        Near.Rel(44.488 * 2.0 * 0.5, BuildSynthetic(tree, summon, enhancement: NoEnhancement(Summon))[0].PetAttacks[0].Damage, 1e-5);
    }

    [Fact]
    public void A_corrupt_entity_file_is_e63()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);
        tree.Write("entities/tst_pet.json", "{");

        string error = Message(() => BuildSynthetic(tree, summon));

        Assert.True(error.StartsWith("E63: ", StringComparison.Ordinal) && error.Contains("tst_pet.json is not valid JSON", StringComparison.Ordinal), error);
    }

    [Fact]
    public void Empty_display_names_fall_back_to_the_entity_name()
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);
        tree.Write("entities/tst_pet.json", """{"name":"Tst_Pet","defaults":{"character_class_name":"minion_pets","power_full_names":["Tst.Set.Zap","Tst.Set.Aura"]},"levels":[{"display_names":[]}]}""");

        Assert.Equal("Tst_Pet", Assert.Single(BuildSynthetic(tree, summon)).PetName);
    }

    [Theory]
    [MemberData(nameof(NoAttackNames))]
    public void A_pet_without_damaging_attacks_is_labelled(string names)
    {
        using TempTree tree = new();
        ChainAttack summon = SyntheticSummon(tree, [Effect(CreateEntity())]);
        tree.Write("entities/tst_pet.json", $$"""{"name":"Tst_Pet","defaults":{"character_class_name":"minion_pets","power_full_names":{{names}}},"levels":[]}""");

        PetSummon pet = Assert.Single(BuildSynthetic(tree, summon));

        Assert.True(pet.PetAttacks.Count == 0 && pet.Label.Contains("no damaging attacks found", StringComparison.Ordinal));
    }

    [Fact]
    public void One_attack_pet_dps_matches_the_hand_derivation()
    {
        SourceResult result = PetDamage.PetSource(Context(pets: Pet(double.PositiveInfinity, 3)))!;
        SourceResult timed = PetDamage.PetSource(Context(pets: Pet(240.0, 3, recharge: 600.0)))!;

        Near.Rel(3 * 100.0 / 2.188, result.Dps, 1e-5);
        Near.Rel(3 * 100.0 / 2.188 * 240.0 / (AttackChains.ArcanaCastTime(2.03) + 600.0), timed.Dps, 1e-5);
    }

    [Fact]
    public void An_area_pet_attack_scales_with_the_scenario_targets()
    {
        PetSummon area = new() { SummonName = "S", PetName = "P", PetAttacks = [PetAttack("P.Burst", 100.0, 1.0, 1.0, Shape.Pbaoe)], PetCount = 1, Duration = double.PositiveInfinity, SummonCastTime = 0.0, SummonRecharge = 5.0, SummonEndCost = 0.0 };

        double five = PetDamage.PetSource(Context(new Scenario(5), area))!.Dps;
        double one = PetDamage.PetSource(Context(new Scenario(1), area))!.Dps;

        Near.Rel(5 * one, five, 1e-5);
        Near.Rel(100.0 / 2.188, one, 1e-5);
    }
}
