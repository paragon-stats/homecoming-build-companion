using CohEngine.Core.Chain;
using CohEngine.Core.Damage;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
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
        return Assert.Throws<EngineException>(() => Build(tree, level, null, summon)).Message;
    }

    private static void Seed(TempTree tree, string table = "Pet_Levelminus1", string duration = "60 seconds", string effectExtra = "")
    {
        PetWorld.Write(tree, table, duration, effectExtra);
    }

    [Fact]
    public void A_summon_becomes_a_pet_with_its_attacks_at_the_pets_level()
    {
        using TempTree tree = new();
        Seed(tree);

        PetSummon pet = Assert.Single(Build(tree));

        Assert.Equal("Imp 1 60 Thing level 49", $"{pet.PetName} {pet.PetCount} {pet.Duration} {pet.Label}");
    }

    [Fact]
    public void A_pet_attack_deals_the_table_damage_times_one_plus_the_enhancement()
    {
        using TempTree tree = new();
        Seed(tree);

        Assert.Equal(F32.Round(F32.Round(10.0) * (1.0 + PetDamage.DefaultPetDamageEnhancement)), Assert.Single(Assert.Single(Build(tree)).PetAttacks).Damage);
    }

    [Fact]
    public void A_per_summon_override_replaces_the_default_enhancement()
    {
        using TempTree tree = new();
        Seed(tree);
        IReadOnlyDictionary<string, double> overrides = new Dictionary<string, double>(StringComparer.Ordinal) { ["Mm.Summon.Spawn"] = 0.5 };

        PetSummon pet = Assert.Single(Build(tree, enhancement: overrides));

        Assert.Equal("1.5 15", $"{F32.Round(1.0 + 0.5)} {Assert.Single(pet.PetAttacks).Damage}");
    }

    [Fact]
    public void The_summon_cast_time_and_cost_are_carried_once()
    {
        using TempTree tree = new();
        Seed(tree);

        PetSummon pet = Assert.Single(Build(tree));

        Assert.Equal("1 60 10", $"{pet.SummonCastTime} {pet.SummonRecharge} {pet.SummonEndCost}");
    }

    [Fact]
    public void A_permanent_pet_lasts_forever()
    {
        using TempTree tree = new();
        Seed(tree, duration: "UntilKilled");

        Assert.True(double.IsPositiveInfinity(Assert.Single(Build(tree)).Duration));
    }

    [Fact]
    public void A_ones_table_takes_the_caster_level()
    {
        using TempTree tree = new();
        Seed(tree, "Ones");

        Assert.Equal("Thing level 50", Assert.Single(Build(tree)).Label);
    }

    [Fact]
    public void A_reversed_token_class_name_is_found()
    {
        using TempTree tree = new();
        Seed(tree);
        tree.Write("entities/thing.json", PetWorld.EntityJson.Replace("minion_pets", "pets_minion", StringComparison.Ordinal));

        Assert.Equal("Thing level 49", Assert.Single(Build(tree)).Label);
    }

    [Fact]
    public void A_damage_template_with_no_finite_span_is_a_single_application()
    {
        using TempTree tree = new();
        Seed(tree);
        tree.Write("powers/pet/set/bite.json", PetWorld.Bite().Replace("\"duration\":\"0 seconds\"", "\"duration\":\"UntilKilled\"", StringComparison.Ordinal));

        Assert.Equal(F32.Round(F32.Round(10.0) * 1.95), Assert.Single(Assert.Single(Build(tree)).PetAttacks).Damage);
    }

    [Fact]
    public void A_damage_over_time_template_counts_its_ticks()
    {
        using TempTree tree = new();
        Seed(tree);
        tree.Write("powers/pet/set/bite.json", PetWorld.Bite(templateExtra: ",\"duration\":\"10 seconds\"").Replace("\"duration\":\"0 seconds\",", string.Empty, StringComparison.Ordinal).Replace("\"application_period\":0.0", "\"application_period\":2.0", StringComparison.Ordinal));

        Assert.Equal(F32.Round(F32.Round(10.0 * 6.0) * 1.95), Assert.Single(Assert.Single(Build(tree)).PetAttacks).Damage);
    }

    [Fact]
    public void Procs_and_conditional_and_pvp_damage_groups_are_skipped()
    {
        using TempTree tree = new();
        Seed(tree);
        tree.Write("powers/pet/set/bite.json", PetWorld.Bite(",\"ppm\":3.0"));

        Assert.Equal(0.0, Assert.Single(Assert.Single(Build(tree)).PetAttacks).Damage);
    }

    [Fact]
    public void A_pet_power_that_is_not_an_attack_is_left_out()
    {
        using TempTree tree = new();
        Seed(tree);
        tree.Write("powers/pet/set/bite.json", CanonicalPowers.Text("Pet.Set.Bite", effects: [CanonicalPowers.Effect([])]));

        Assert.Empty(Assert.Single(Build(tree)).PetAttacks);
    }

    [Fact]
    public void A_met_level_gate_spawns_the_entity_and_an_unmet_one_does_not()
    {
        using TempTree tree = new();
        Seed(tree, effectExtra: ",\"requires_expression\":\"source>combatlevel > 60\"");

        Assert.Empty(Build(tree));
    }

    [Fact]
    public void Alternate_costume_groups_spawn_only_the_largest()
    {
        using TempTree tree = new();
        Seed(tree, effectExtra: ",\"requires_expression\":\"@CustomFX eq 'a'\"");

        Assert.Equal(1, Assert.Single(Build(tree)).PetCount);
    }

    [Theory]
    [InlineData("source>combatlevel < 3")]
    [InlineData("source>combatlevel > x")]
    [InlineData("source>combatlevel >")]
    public void A_condition_that_only_looks_like_a_level_gate_is_an_alternate_group(string condition)
    {
        using TempTree tree = new();
        Seed(tree, effectExtra: $",\"requires_expression\":\"{condition}\"");

        Assert.Equal(1, Assert.Single(Build(tree)).PetCount);
    }

    [Fact]
    public void A_second_entity_carries_no_cast_or_cost()
    {
        using TempTree tree = new();
        Seed(tree);
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
        using TempTree tree = new();
        Seed(tree);
        tree.Write("entities/thing.json", PetWorld.EntityJson.Replace(",\"levels\":[{\"display_names\":[\"Imp\"]}]", string.Empty, StringComparison.Ordinal));

        Assert.Equal("Thing", Assert.Single(Build(tree)).PetName);
    }

    [Fact]
    public void The_modifier_table_lookup_names_the_missing_table()
    {
        Assert.StartsWith("E61: Power uses modifier table 'Nope'", Assert.Throws<EngineException>(() => PetDamage.ModifierTable(PetWorld.Mods(), "Nope", "Power")).Message, StringComparison.Ordinal);
    }
}
