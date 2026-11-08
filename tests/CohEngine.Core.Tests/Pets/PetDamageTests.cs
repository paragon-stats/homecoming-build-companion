using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Pets;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Pets;

public sealed class PetDamageTests
{
    private static readonly ChainAttack Spawn = ChainFactory.Attack("Mm.Summon.Spawn", Role.PetSummon, damage: 0.0, cast: 1.0, recharge: 60.0, end: 10.0);

    private static IReadOnlyList<PetSummon> Build(TempTree tree, double level = 49.0, IReadOnlyDictionary<string, double>? enhancement = null, ChainAttack? summon = null)
    {
        return PetDamage.BuildPetSummons([summon ?? Spawn], tree.Root, PetWorld.Mods(level), PetWorld.Classes(), damageEnhancement: enhancement);
    }

    private static string Message(TempTree tree, double level = 49.0, ChainAttack? summon = null)
    {
        return Assert.Throws<ChainException>(() => Build(tree, level, null, summon)).Message;
    }

    private static TempTree World(string table = "Pet_Levelminus1", string duration = "60 seconds", string effectExtra = "")
    {
        TempTree tree = new();
        PetWorld.Write(tree, table, duration, effectExtra);
        return tree;
    }

    [Fact]
    public void A_summon_becomes_a_pet_with_its_attacks_at_the_pets_level()
    {
        using TempTree tree = World();

        PetSummon pet = Assert.Single(Build(tree));

        Assert.Equal("Imp 1 60 Thing level 49", $"{pet.PetName} {pet.PetCount} {pet.Duration} {pet.Label}");
    }

    [Fact]
    public void A_pet_attack_deals_the_table_damage_times_one_plus_the_enhancement()
    {
        using TempTree tree = World();

        Assert.Equal(F32.Round(F32.Round(10.0) * (1.0 + PetDamage.DefaultPetDamageEnhancement)), Assert.Single(Assert.Single(Build(tree)).PetAttacks).Damage);
    }

    [Fact]
    public void A_per_summon_override_replaces_the_default_enhancement()
    {
        using TempTree tree = World();
        IReadOnlyDictionary<string, double> overrides = new Dictionary<string, double>(StringComparer.Ordinal) { ["Mm.Summon.Spawn"] = 0.5 };

        PetSummon pet = Assert.Single(Build(tree, enhancement: overrides));

        Assert.Equal("1.5 15", $"{F32.Round(1.0 + 0.5)} {Assert.Single(pet.PetAttacks).Damage}");
    }

    [Fact]
    public void The_summon_cast_time_and_cost_are_carried_once()
    {
        using TempTree tree = World();

        PetSummon pet = Assert.Single(Build(tree));

        Assert.Equal("1 60 10", $"{pet.SummonCastTime} {pet.SummonRecharge} {pet.SummonEndCost}");
    }

    [Fact]
    public void A_permanent_pet_lasts_forever()
    {
        using TempTree tree = World(duration: "UntilKilled");

        Assert.True(double.IsPositiveInfinity(Assert.Single(Build(tree)).Duration));
    }

    [Fact]
    public void A_ones_table_takes_the_caster_level()
    {
        using TempTree tree = World("Ones");

        Assert.Equal("Thing level 50", Assert.Single(Build(tree)).Label);
    }

    [Fact]
    public void A_pet_level_below_one_is_e61()
    {
        using TempTree tree = World();

        Assert.StartsWith("E61: table Pet_Levelminus1 gives pet level 0 at caster level 50", Message(tree, 0.0), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_modifier_table_is_e61()
    {
        using TempTree tree = World("Missing_Table");

        Assert.StartsWith("E61: Mm.Summon.Spawn uses modifier table 'Missing_Table', which the AttribMod table lacks", Message(tree), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_pet_class_is_e61()
    {
        using TempTree tree = World();
        tree.Write("entities/thing.json", PetWorld.EntityJson.Replace("minion_pets", "mystery", StringComparison.Ordinal));

        Assert.StartsWith("E61: entity Thing has class 'mystery' with no Class_mystery or reversed-token class", Message(tree), StringComparison.Ordinal);
    }

    [Fact]
    public void A_reversed_token_class_name_is_found()
    {
        using TempTree tree = World();
        tree.Write("entities/thing.json", PetWorld.EntityJson.Replace("minion_pets", "pets_minion", StringComparison.Ordinal));

        Assert.Equal("Thing level 49", Assert.Single(Build(tree)).Label);
    }

    [Fact]
    public void A_role_other_than_summon_is_e64()
    {
        using TempTree tree = World();

        Assert.StartsWith("E64: Mm.Summon.Spawn has role chain, expected pet_summon", Message(tree, summon: ChainFactory.Attack("Mm.Summon.Spawn")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_entity_file_is_e60()
    {
        using TempTree tree = World();
        File.Delete(tree.FullPath("entities/thing.json"));

        Assert.StartsWith("E60: entity Thing (summoned by Mm.Summon.Spawn) has no file", Message(tree), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_pet_power_file_is_e60()
    {
        using TempTree tree = World();
        File.Delete(tree.FullPath("powers/pet/set/bite.json"));

        Assert.StartsWith("E60: pet powers of Thing missing (E35:", Message(tree), StringComparison.Ordinal);
    }

    [Fact]
    public void A_summon_with_no_create_entity_template_is_e62()
    {
        using TempTree tree = new();
        tree.Write("powers/mm/summon/spawn.json", CanonicalPowers.Text("Mm.Summon.Spawn", effects: [CanonicalPowers.Effect([])]));

        Assert.Equal("E62: Mm.Summon.Spawn has no Create_Entity template; pass only pet summon powers", Message(tree));
    }

    [Fact]
    public void A_create_entity_template_without_an_entity_def_is_e62()
    {
        using TempTree tree = new();
        string create = CanonicalPowers.Template(["Create_Entity"], "Absolute", "Self", "Ones", 1.0, ",\"duration\":\"60 seconds\"");
        tree.Write("powers/mm/summon/spawn.json", CanonicalPowers.Text("Mm.Summon.Spawn", effects: [CanonicalPowers.Effect([create])]));

        Assert.StartsWith("E62: Mm.Summon.Spawn has a Create_Entity template with no params.entity_def", Message(tree), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("soon")]
    [InlineData("12 minutes")]
    [InlineData("many seconds")]
    public void An_unreadable_entity_duration_is_e62(string duration)
    {
        using TempTree tree = World(duration: duration);

        Assert.StartsWith($"E62: Mm.Summon.Spawn Create_Entity duration is '{duration}', expected 'N seconds' or 'UntilKilled'", Message(tree), StringComparison.Ordinal);
    }

    [Fact]
    public void An_unreadable_damage_duration_is_e62()
    {
        using TempTree tree = World();
        tree.Write("powers/pet/set/bite.json", PetWorld.Bite(templateExtra: ",\"duration\":\"forever\"").Replace("\"duration\":\"0 seconds\",", string.Empty, StringComparison.Ordinal));

        Assert.StartsWith("E62: Pet.Set.Bite damage template duration is 'forever'", Message(tree), StringComparison.Ordinal);
    }

    [Fact]
    public void A_damage_over_time_template_counts_its_ticks()
    {
        using TempTree tree = World();
        tree.Write("powers/pet/set/bite.json", PetWorld.Bite(templateExtra: ",\"duration\":\"10 seconds\"").Replace("\"duration\":\"0 seconds\",", string.Empty, StringComparison.Ordinal).Replace("\"application_period\":0.0", "\"application_period\":2.0", StringComparison.Ordinal));

        Assert.Equal(F32.Round(F32.Round(10.0 * 6.0) * 1.95), Assert.Single(Assert.Single(Build(tree)).PetAttacks).Damage);
    }

    [Fact]
    public void Procs_and_conditional_and_pvp_damage_groups_are_skipped()
    {
        using TempTree tree = World();
        tree.Write("powers/pet/set/bite.json", PetWorld.Bite(",\"ppm\":3.0"));

        Assert.Equal(0.0, Assert.Single(Assert.Single(Build(tree)).PetAttacks).Damage);
    }

    [Fact]
    public void A_pet_with_no_damaging_attack_says_so()
    {
        using TempTree tree = World();
        tree.Write("powers/pet/set/bite.json", CanonicalPowers.Text("Pet.Set.Bite", type: "Toggle", effects: [CanonicalPowers.Effect([])]));

        Assert.Equal("Thing level 49 (no damaging attacks found)", Assert.Single(Build(tree)).Label);
    }

    [Fact]
    public void A_pet_power_that_is_not_an_attack_is_left_out()
    {
        using TempTree tree = World();
        tree.Write("powers/pet/set/bite.json", CanonicalPowers.Text("Pet.Set.Bite", effects: [CanonicalPowers.Effect([])]));

        Assert.Empty(Assert.Single(Build(tree)).PetAttacks);
    }

    [Fact]
    public void A_met_level_gate_spawns_the_entity_and_an_unmet_one_does_not()
    {
        using TempTree tree = World(effectExtra: ",\"requires_expression\":\"source>combatlevel > 60\"");

        Assert.Empty(Build(tree));
    }

    [Fact]
    public void Alternate_costume_groups_spawn_only_the_largest()
    {
        using TempTree tree = World(effectExtra: ",\"requires_expression\":\"@CustomFX eq 'a'\"");

        Assert.Equal(1, Assert.Single(Build(tree)).PetCount);
    }

    [Fact]
    public void Pvp_only_groups_are_skipped()
    {
        using TempTree tree = World(effectExtra: ",\"is_pvp\":\"PVP_ONLY\"");

        Assert.Equal("E62: Mm.Summon.Spawn has no Create_Entity template; pass only pet summon powers", Message(tree));
    }

    [Fact]
    public void A_second_entity_carries_no_cast_or_cost()
    {
        using TempTree tree = World();
        string create = CanonicalPowers.Template(["Create_Entity"], "Absolute", "Self", "Pet_Levelminus1", 1.0, ",\"duration\":\"60 seconds\",\"params\":{\"entity_def\":\"Thing\"}");
        string second = CanonicalPowers.Template(["Create_Entity"], "Absolute", "Self", "Pet_Levelminus1", 1.0, ",\"duration\":\"60 seconds\",\"params\":{\"entity_def\":\"Other\"}");
        tree.Write("powers/mm/summon/spawn.json", CanonicalPowers.Text("Mm.Summon.Spawn", effects: [CanonicalPowers.Effect([create, second])], categories: ["Pet Damage"]));
        tree.Write("entities/other.json", PetWorld.EntityJson.Replace("\"Thing\"", "\"Other\"", StringComparison.Ordinal));

        IReadOnlyList<PetSummon> pets = Build(tree);

        Assert.Equal("1 0 10 0", $"{pets[0].SummonCastTime} {pets[1].SummonCastTime} {pets[0].SummonEndCost} {pets[1].SummonEndCost}");
    }

    [Fact]
    public void The_pet_name_falls_back_to_the_entity_name()
    {
        using TempTree tree = World();
        tree.Write("entities/thing.json", PetWorld.EntityJson.Replace(",\"levels\":[{\"display_names\":[\"Imp\"]}]", string.Empty, StringComparison.Ordinal));

        Assert.Equal("Thing", Assert.Single(Build(tree)).PetName);
    }

    [Fact]
    public void The_modifier_table_lookup_names_the_missing_table()
    {
        Assert.StartsWith("E61: Power uses modifier table 'Nope'", Assert.Throws<ChainException>(() => PetDamage.ModifierTable(PetWorld.Mods(), "Nope", "Power")).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void No_pets_is_a_zero_source_with_a_label()
    {
        SourceResult source = PetDamage.PetSource(ChainFactory.Context([]))!;

        Assert.Equal("0 no pets summoned", $"{source.Dps} {source.Label}");
    }

    [Theory]
    [InlineData(60.0, "Imp x2 uptime 59%;")]
    [InlineData(double.PositiveInfinity, "Imp x2 uptime 100% (permanent)")]
    public void A_pets_uptime_is_its_lifetime_over_the_summon_cycle(double duration, string expected)
    {
        PetSummon pet = new()
        {
            SummonName = "S",
            PetName = "Imp",
            PetAttacks = [ChainFactory.Attack("Bite", damage: 100.0, cast: 1.0, recharge: 4.0)],
            PetCount = 2,
            Duration = duration,
            SummonCastTime = 1.0,
            SummonRecharge = 100.0,
            SummonEndCost = 10.0,
            DamageBuffBaseline = 1.95,
        };

        SourceResult source = PetDamage.PetSource(ChainFactory.Context([]) with { Pets = [pet] })!;

        Assert.Contains(expected.TrimEnd(';'), source.Label, StringComparison.Ordinal);
    }

    [Fact]
    public void A_pet_with_no_attacks_adds_nothing_and_keeps_the_default_baseline()
    {
        PetSummon pet = new() { SummonName = "S", PetName = "Imp", PetAttacks = [], PetCount = 1, Duration = double.PositiveInfinity, SummonCastTime = 0.0, SummonRecharge = 0.0, SummonEndCost = 0.0 };

        SourceResult source = PetDamage.PetSource(ChainFactory.Context([]) with { Pets = [pet] })!;

        Assert.Equal("0 1", $"{source.Dps} {source.DamageBuffBaseline}");
    }
}
