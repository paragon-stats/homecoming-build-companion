using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Damage;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Chain;

public sealed class AttackProfilesTests
{
    private static readonly string FoeDamage = CanonicalPowers.Template(["Smashing_Dmg"]);
    private static readonly string SelfBuff = CanonicalPowers.Template(["Damage_Dmg"], "Strength", "Self", "Buff_Dmg");
    private static readonly string TeamBuff = CanonicalPowers.Template(["Damage_Dmg"], "Strength", "AnyAffected", "Buff_Dmg");
    private static readonly string FoeRes = CanonicalPowers.Template(["Fire_Dmg"], "Resistance", "Foe", "Ranged_Debuff_Res_Dmg", -1.0);
    private static readonly string Summon = CanonicalPowers.Template(["Create_Entity"], "Absolute", "Self", "Ones", 1.0, ",\"params\":{\"entity_def\":\"Thing\"}");
    private static readonly string[] PetCategory = ["Pet Damage"];

    public static TheoryData<string, Role> Roles => new()
    {
        { CanonicalPowers.Text(effects: [CanonicalPowers.Effect([FoeDamage])]), Role.Chain },
        { CanonicalPowers.Text(type: "Auto", effects: [CanonicalPowers.Effect([FoeDamage])]), Role.Aura },
        { CanonicalPowers.Text(type: "Toggle", effects: [CanonicalPowers.Effect([FoeDamage])]), Role.Aura },
        { CanonicalPowers.Text(effects: [CanonicalPowers.Effect([Summon])], categories: PetCategory), Role.PetSummon },
        { CanonicalPowers.Text(effects: [CanonicalPowers.Effect([Summon])]), Role.Patch },
        { CanonicalPowers.Text(effects: [CanonicalPowers.Effect([Summon, FoeRes])]), Role.Support },
        { CanonicalPowers.Text(effects: [CanonicalPowers.Effect([FoeRes])]), Role.Support },
        { CanonicalPowers.Text(effects: [CanonicalPowers.Effect([TeamBuff])]), Role.Support },
        { CanonicalPowers.Text(effects: [CanonicalPowers.Effect([SelfBuff])]), Role.BuffClick },
        { CanonicalPowers.Text(type: "Toggle", effects: [CanonicalPowers.Effect([FoeRes])]), Role.PersistentDebuff },
        { CanonicalPowers.Text(effects: [CanonicalPowers.Effect([], ",\"child_effects\":[" + CanonicalPowers.Effect([FoeDamage]) + "]")]), Role.Chain },
    };

    public static TheoryData<string> Unclassifiable => new()
    {
        CanonicalPowers.Text(type: "Toggle", effects: [CanonicalPowers.Effect([SelfBuff])]),
        CanonicalPowers.Text(effects: [CanonicalPowers.Effect([CanonicalPowers.Template(["Fire_Dmg"], "Strength", "Self", "Ranged_Dmg")])]),
        CanonicalPowers.Text(type: "Grant"),
    };

    public static TheoryData<string, Shape> Shapes => new()
    {
        { CanonicalPowers.Text(area: "SingleTarget", effects: [CanonicalPowers.Effect([FoeDamage])]), Shape.SingleTarget },
        { CanonicalPowers.Text(area: "Cone", effects: [CanonicalPowers.Effect([FoeDamage])]), Shape.Cone },
        { CanonicalPowers.Text(area: "Location", effects: [CanonicalPowers.Effect([FoeDamage])]), Shape.Location },
        { CanonicalPowers.Text(area: "Chain", effects: [CanonicalPowers.Effect([FoeDamage])]), Shape.TargetedAoe },
        { CanonicalPowers.Text(area: "AoE", range: 40.0, effects: [CanonicalPowers.Effect([FoeDamage])]), Shape.TargetedAoe },
        { CanonicalPowers.Text(area: "AoE", range: 0.0, effects: [CanonicalPowers.Effect([FoeDamage])]), Shape.Pbaoe },
    };

    public static TheoryData<string, string, string, string, double, bool> FoeResCases => new()
    {
        { "Fire_Dmg", "Resistance", "Foe", "Ranged_Debuff_Res_Dmg", 1.0, true },
        { "Fire_Dmg", "Resistance", "Foe", "Ranged_Res_Dmg", -1.0, true },
        { "Fire_Dmg", "Resistance", "Foe", "Ranged_Res_Dmg", 1.0, false },
        { "Fire_Dmg", "Resistance", "Self", "Ranged_Debuff_Res_Dmg", -1.0, false },
        { "Fire_Dmg", "Resistance", "Foe", "Ranged_Buff_Res_Dmg", -1.0, false },
        { "Heal_Dmg", "Resistance", "Foe", "Ranged_Debuff_Res_Dmg", -1.0, false },
        { "Fire_Dmg", "Absolute", "Foe", "Ranged_Debuff_Res_Dmg", -1.0, false },
        { "Fire_Mez", "Resistance", "Foe", "Ranged_Debuff_Res_Dmg", -1.0, false },
    };

    [Theory]
    [MemberData(nameof(Roles))]
    public void Classify_assigns_the_role_by_precedence(string json, Role expected)
    {
        Assert.Equal(expected, AttackProfiles.Classify(CanonicalPowers.Parse(json)).Role);
    }

    [Theory]
    [MemberData(nameof(Unclassifiable))]
    public void Classify_rejects_a_power_with_nothing_to_model_as_e37(string json)
    {
        Assert.StartsWith(ErrorCodes.E37, Assert.Throws<EngineException>(() => AttackProfiles.Classify(CanonicalPowers.Parse(json))).Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Shapes))]
    public void Classify_maps_the_effect_area_to_a_shape(string json, Shape expected)
    {
        Assert.Equal(expected, AttackProfiles.Classify(CanonicalPowers.Parse(json)).Shape);
    }

    [Fact]
    public void Classify_names_an_unmapped_area_in_e36()
    {
        EngineException error = Assert.Throws<EngineException>(() => AttackProfiles.Classify(CanonicalPowers.Parse(CanonicalPowers.Text(area: "Self", effects: [CanonicalPowers.Effect([FoeDamage])]))));

        Assert.Equal("E36: Test.Set.Power has effect_area 'Self', expected one of ['AoE', 'SingleTarget', 'Cone', 'Location', 'Chain']; add the value to attack_profile._SHAPES or filter the power", error.Message);
    }

    [Theory]
    [InlineData(20.0, Delivery.Melee)]
    [InlineData(20.5, Delivery.Ranged)]
    public void Classify_splits_delivery_at_the_melee_range(double range, Delivery expected)
    {
        Assert.Equal(expected, AttackProfiles.Classify(CanonicalPowers.Parse(CanonicalPowers.Text(range: range, effects: [CanonicalPowers.Effect([FoeDamage])]))).Delivery);
    }

    [Fact]
    public void Classify_reads_the_modes_a_power_requires()
    {
        JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text(effects: [CanonicalPowers.Effect([FoeDamage])], extra: ",\"modes_required\":[\"Nova\"]"));

        Assert.Equal(["Nova"], AttackProfiles.Classify(power).ModesRequired);
    }

    [Theory]
    [MemberData(nameof(FoeResCases))]
    public void FoeRes_reads_direction_from_the_table_name(string attrib, string aspect, string target, string table, double scale, bool expected)
    {
        Assert.Equal(expected, AttackProfiles.FoeRes(attrib, aspect, target, table, scale));
    }

    [Theory]
    [InlineData("{\"is_pvp\":\"PVP_ONLY\"}", false)]
    [InlineData("{\"requires_expression\":\"target>enttype eq 'critter'\"}", true)]
    [InlineData("{\"requires_expression\":\" \"}", true)]
    [InlineData("{\"requires_expression\":\"source>level gt 3\"}", false)]
    [InlineData("{}", true)]
    public void AppliesInPve_allows_only_unconditional_or_critter_gated_groups(string effect, bool expected)
    {
        Assert.Equal(expected, AttackProfiles.AppliesInPve(CanonicalPowers.Parse(effect)));
    }

    [Fact]
    public void Redirected_effectless_power_follows_the_default_redirect()
    {
        JsonElement target = CanonicalPowers.Parse(CanonicalPowers.Text(name: "Test.Set.Real", effects: [CanonicalPowers.Effect([FoeDamage])]));
        JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text(extra: ",\"redirect\":[{\"name\":\"Test.Set.Nova\",\"condition_expression\":\"Source.Mode?(Nova)\"},{\"name\":\"Test.Set.Real\",\"condition_expression\":\"!Source.Mode?(Nova)\"}]"));

        Assert.Equal(Role.Chain, AttackProfiles.Classify(power, resolve: name => string.Equals(name, "Test.Set.Real", StringComparison.Ordinal) ? target : null).Role);
    }

    [Fact]
    public void Redirect_with_no_decidable_condition_leaves_the_power_unclassifiable()
    {
        JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text(extra: ",\"redirect\":[{\"name\":\"Test.Set.Nova\",\"condition_expression\":\"Source.Mode?(Nova)\"}]"));

        Assert.Throws<EngineException>(() => AttackProfiles.Classify(power, resolve: _ => null));
    }

    [Fact]
    public void Linked_powers_count_as_the_powers_own()
    {
        JsonElement linked = CanonicalPowers.Parse(CanonicalPowers.Text(name: "Test.Set.Hit", effects: [CanonicalPowers.Effect([FoeDamage])]));
        string execute = CanonicalPowers.Template(["Execute_Power"], "Absolute", "Self", "Ones", 1.0, ",\"params\":{\"power_names\":[\"Test.Set.Hit\",\"Test.Set.Missing\"]}");
        JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text(effects: [CanonicalPowers.Effect([execute])]));

        Assert.Equal(Role.Chain, AttackProfiles.Classify(power, resolve: name => string.Equals(name, "Test.Set.Hit", StringComparison.Ordinal) ? linked : null).Role);
    }

    [Fact]
    public void An_entity_definition_links_its_powers_one_level()
    {
        JsonElement entity = CanonicalPowers.Parse("{\"defaults\":{\"power_full_names\":[\"Test.Set.Zap\"]}}");
        JsonElement zap = CanonicalPowers.Parse(CanonicalPowers.Text(name: "Test.Set.Zap", effects: [CanonicalPowers.Effect([FoeDamage])]));
        JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text(effects: [CanonicalPowers.Effect([Summon])]));
        PowerResolver resolve = name => string.Equals(name, "Thing", StringComparison.Ordinal) ? entity : string.Equals(name, "Test.Set.Zap", StringComparison.Ordinal) ? zap : null;

        Assert.Equal(Role.Patch, AttackProfiles.Classify(power, resolve: resolve).Role);
    }

    [Fact]
    public void Foe_res_templates_come_from_the_power_its_children_and_linked_powers_in_pve_only()
    {
        JsonElement linked = CanonicalPowers.Parse(CanonicalPowers.Text(name: "Test.Set.Debuff", effects: [CanonicalPowers.Effect([FoeRes]), CanonicalPowers.Effect([FoeRes], ",\"is_pvp\":\"PVP_ONLY\"")]));
        string execute = CanonicalPowers.Template(["Execute_Power"], "Absolute", "Self", "Ones", 1.0, ",\"params\":{\"power_names\":[\"Test.Set.Debuff\"]}");
        JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text(effects: [CanonicalPowers.Effect([execute], ",\"child_effects\":[" + CanonicalPowers.Effect([FoeRes]) + "]")]));

        Assert.Equal(2, AttackProfiles.FoeResDebuffTemplates(power, name => string.Equals(name, "Test.Set.Debuff", StringComparison.Ordinal) ? linked : null).Count);
    }

    [Fact]
    public void Foe_res_templates_without_a_resolver_come_from_the_power_alone()
    {
        JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text(effects: [CanonicalPowers.Effect([FoeRes])]));

        Assert.Single(AttackProfiles.FoeResDebuffTemplates(power, null));
    }

    [Theory]
    [InlineData(Delivery.Melee, "Ranged Damage", "Test.Set.Power is classified MELEE (range 80 ft) but its set categories ['Ranged Damage'] are ranged; check the melee range threshold or the power's range")]
    [InlineData(Delivery.Ranged, "Melee Damage", "Test.Set.Power is classified RANGED (range 80 ft) but its set categories ['Melee Damage'] are melee; check the melee range threshold or the power's range")]
    [InlineData(Delivery.Melee, "Melee Damage", null)]
    [InlineData(Delivery.Ranged, "Ranged Damage", null)]
    public void CategoryDisagreement_names_a_conflict_between_delivery_and_categories(Delivery delivery, string category, string? expected)
    {
        AttackProfile profile = ChainFactory.Profile(delivery: delivery) with { SetCategories = [category] };

        Assert.Equal(expected, AttackProfiles.CategoryDisagreement(profile));
    }

    [Fact]
    public void CategoryDisagreement_is_silent_when_both_kinds_vote()
    {
        AttackProfile profile = ChainFactory.Profile(delivery: Delivery.Melee) with { SetCategories = ["Melee Damage", "Ranged Damage"] };

        Assert.Null(AttackProfiles.CategoryDisagreement(profile));
    }
}
