using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Chain;

// Ported from tests/test_attack_profile.py, on the real canonical power files and on inline powers.
public sealed class AttackProfilesPortTests
{
    private const string Css = "Arachnos_Soldiers.Crab_Spider_Soldier.";
    private const string Colon = "Training_Gadgets.Training_and_Gadgets.Tactical_Training:_Assault";

    private static readonly string Powers = Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical", "powers");
    private static readonly string[] Fire = ["Fire_Dmg"];

    public static TheoryData<string, Delivery?, Shape?, Role> Table => new()
    {
        { Css + "Channelgun", Delivery.Ranged, Shape.SingleTarget, Role.Chain },
        { Css + "Slice", Delivery.Melee, Shape.SingleTarget, Role.Chain },
        { Css + "Arm_Lash", Delivery.Melee, Shape.Cone, Role.Chain },
        { Css + "Suppression", Delivery.Ranged, Shape.Cone, Role.Chain },
        { Css + "CS_Venom_Grenade", Delivery.Ranged, Shape.TargetedAoe, Role.Chain },
        { Css + "Frenzy", Delivery.Melee, Shape.Pbaoe, Role.Chain },
        { Css + "Omega_Maneuver", Delivery.Ranged, Shape.Location, Role.Patch },
        { Css + "Aim", Delivery.Melee, Shape.SingleTarget, Role.BuffClick },
        { "Training_Gadgets.Crab_Spider_Training.Summon_Spiderlings", null, null, Role.PetSummon },
        { "Arachnos_Soldiers.Bane_Spider_Soldier.Pulverize", Delivery.Melee, Shape.SingleTarget, Role.Chain },
        { "Brute_Defense.Fiery_Aura.Blazing_Aura", Delivery.Melee, Shape.Pbaoe, Role.Aura },
        { "Mastermind_Summon.Mercenaries.Soldiers", null, null, Role.PetSummon },
    };

    public static TheoryData<string, Role> Linked => new()
    {
        { "Defender_Buff.Thermal_Radiation.Melt_Armor", Role.Support },
        { "Defender_Buff.Kinetics.Siphon_Power", Role.BuffClick },
        { "Defender_Buff.Kinetics.Kinetic_Transfer", Role.BuffClick },
        { "Defender_Buff.Storm_Summoning.Fog", Role.Patch },
        { "Defender_Buff.Dark_Miasma.Tar_Patch", Role.Support },
        { "Defender_Buff.Radiation_Emission.Enervating__Field", Role.PersistentDebuff },
        { "Defender_Buff.Poison.Venomous_Gas", Role.PersistentDebuff },
        { "Corruptor_Buff.Sonic_Resonance.Sonic_Siphon", Role.Support },
        { "Defender_Buff.Poison.Envenom", Role.Support },
        { "Sentinel_Ranged.Archery.Aim", Role.BuffClick },
        { "Scrapper_Melee.Katana.Build_Up", Role.BuffClick },
        { "Peacebringer_Offensive.Luminous_Blast.Bright_Nova_Blast", Role.Chain },
    };

    public static TheoryData<string> Redirected => ["Warshade_Offensive.Umbral_Blast.Shadow_Bolt", "Feral_Might.Feral_Might.Brutal_Swipe"];

    public static TheoryData<Delivery, string[]> Agreeing => new()
    {
        { Delivery.Melee, ["Universal Damage Sets", "Brute Archetype Sets"] },
        { Delivery.Melee, ["Melee Damage"] },
        { Delivery.Ranged, ["Ranged Damage"] },
        { Delivery.Ranged, ["Melee Damage", "Ranged Damage"] },
    };

    private static JsonElement Real(string name)
    {
        return CanonicalJson.LoadCanonicalPowers(Powers, [name])[name];
    }

    private static string Effect(string attrib, string aspect, string target, string? table = null, double scale = 1.0, string? extra = null)
    {
        string tableText = table is null ? string.Empty : $",\"table\":\"{table}\",\"scale\":{scale.ToString("R", System.Globalization.CultureInfo.InvariantCulture)}";
        string template = $"{{\"attribs\":[\"{attrib}\"],\"aspect\":\"{aspect}\",\"target\":\"{target}\"{tableText}{extra}}}";
        return $"{{\"templates\":[{template}],\"child_effects\":[]}}";
    }

    private static JsonElement Inline(string effects, string type = "Click", string area = "SingleTarget", double range = 40.0, string[]? cats = null)
    {
        return CanonicalPowers.Parse(CanonicalPowers.Text("X.Y.Z", type, [effects], area, range, cats));
    }

    private static Role RoleOf(JsonElement power, PowerResolver? resolve = null)
    {
        return AttackProfiles.Classify(power, resolve: resolve).Role;
    }

    private static string ErrorOf(JsonElement power, PowerResolver? resolve = null)
    {
        return Assert.Throws<EngineException>(() => AttackProfiles.Classify(power, resolve: resolve)).Message;
    }

    private static PowerResolver Lookup(Dictionary<string, JsonElement> entries)
    {
        return name => entries.TryGetValue(name, out JsonElement value) ? value : null;
    }

    [Theory]
    [MemberData(nameof(Table))]
    public void A_real_power_classifies_to_its_delivery_shape_and_role(string name, Delivery? delivery, Shape? shape, Role role)
    {
        AttackProfile profile = AttackProfiles.Classify(Real(name));

        Assert.Equal(name, profile.FullName);
        Assert.Equal(role, profile.Role);
        Assert.Equal(delivery is null ? "-" : $"{delivery} {shape}", delivery is null ? "-" : $"{profile.Delivery} {profile.Shape}");
    }

    [Fact]
    public void Every_real_power_agrees_with_its_categories()
    {
        string[] names = [.. Table.Select(row => row.Data.Item1)];

        Assert.All(names, name => Assert.Null(AttackProfiles.CategoryDisagreement(AttackProfiles.Classify(Real(name)))));
    }

    [Theory]
    [MemberData(nameof(Redirected))]
    public void An_effectless_power_classifies_through_its_default_redirect(string name)
    {
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Powers);
        JsonElement power = resolve(name)!.Value;

        Assert.Empty(power.ReqItems("effects"));
        Assert.Equal(Role.Chain, RoleOf(power, resolve));
        Assert.StartsWith("E37", ErrorOf(power), StringComparison.Ordinal);
    }

    [Fact]
    public void A_redirect_on_an_undecidable_condition_is_not_followed()
    {
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Powers);

        Assert.StartsWith("E37", ErrorOf(resolve("Warshade_Defensive.Umbral_Aura.Shadow_Recall")!.Value, resolve), StringComparison.Ordinal);
    }

    [Fact]
    public void The_pve_rule_is_unconditional_or_critter_only_and_never_pvp_only()
    {
        bool[] results =
        [
            AttackProfiles.AppliesInPve(CanonicalPowers.Parse("""{"requires_expression":""}""")),
            AttackProfiles.AppliesInPve(CanonicalPowers.Parse("""{"requires_expression":" target>enttype eq 'critter' "}""")),
            AttackProfiles.AppliesInPve(CanonicalPowers.Parse("""{"requires_expression":"Source.Mode?(x)"}""")),
            AttackProfiles.AppliesInPve(CanonicalPowers.Parse("""{"requires_expression":"","is_pvp":"PVP_ONLY"}""")),
        ];

        Assert.Equal([true, true, false, false], results);
    }

    [Fact]
    public void Foe_res_templates_follow_links_and_skip_pvp()
    {
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Powers);
        JsonElement rain = resolve("Defender_Buff.Storm_Summoning.Fog")!.Value;
        JsonElement melt = resolve("Defender_Buff.Thermal_Radiation.Melt_Armor")!.Value;
        JsonElement aim = resolve("Arachnos_Soldiers.Crab_Spider_Soldier.Aim")!.Value;

        IReadOnlyList<JsonElement> rainRes = AttackProfiles.FoeResDebuffTemplates(rain, resolve);

        Assert.True(rainRes.Count > 0 && rainRes.All(template => template.ReqStr("table").EndsWith("Res_Dmg", StringComparison.Ordinal)));
        Assert.Equal(rainRes.Count, rainRes.Select(template => $"{template.ReqStr("table")}|{template.ReqNum("scale")}|{template.ReqStr("duration")}").Distinct(StringComparer.Ordinal).Count());
        Assert.True(AttackProfiles.FoeResDebuffTemplates(melt, resolve).Count > 0 && AttackProfiles.FoeResDebuffTemplates(melt, null).Count > 0);
        Assert.Empty(AttackProfiles.FoeResDebuffTemplates(aim, resolve));
    }

    [Fact]
    public void Damage_in_a_child_effect_is_found()
    {
        string nested = Effect("Fire_Dmg", "Absolute", "AnyAffected");

        Assert.Equal(Role.Chain, RoleOf(Inline($"{{\"templates\":[],\"child_effects\":[{nested}]}}")));
    }

    [Fact]
    public void A_damaging_auto_power_is_an_aura_not_a_chain_attack()
    {
        Assert.Equal(Role.Aura, RoleOf(Inline(Effect("Fire_Dmg", "Absolute", "AnyAffected"), "Auto", "AoE", 0.0)));
    }

    [Fact]
    public void A_non_damaging_power_is_e37_naming_it()
    {
        Assert.StartsWith("E37: X.Y.Z", ErrorOf(Inline(Effect("Defense", "Current", "Self"))), StringComparison.Ordinal);
    }

    [Fact]
    public void A_self_damage_buff_on_a_toggle_is_e37()
    {
        Assert.StartsWith("E37", ErrorOf(Inline(Effect("Fire_Dmg", "Strength", "Self"), "Toggle")), StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(Agreeing))]
    public void Agreement_between_delivery_and_categories_is_none(Delivery delivery, string[] categories)
    {
        AttackProfile profile = ChainFactory.Profile(delivery: delivery) with { SetCategories = categories };

        Assert.Null(AttackProfiles.CategoryDisagreement(profile));
    }

    [Fact]
    public void An_unknown_name_is_e35_listing_it()
    {
        using TempTree tree = new();
        tree.Write("a/b/here.json", """{"full_name":"A.B.Here"}""");

        Assert.Contains("['A.B.Gone']", Assert.Throws<EngineException>(() => CanonicalJson.LoadCanonicalPowers(tree.Root, ["A.B.Here", "A.B.Gone"])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_corrupt_file_alone_is_e38_with_its_path()
    {
        using TempTree tree = new();
        string path = tree.Write("a/b/bad.json", "{");

        string message = Assert.Throws<EngineException>(() => CanonicalJson.LoadCanonicalPowers(tree.Root, ["A.B.Bad"])).Message;

        Assert.True(message.StartsWith("E38: ", StringComparison.Ordinal) && message.Contains("bad.json is not valid JSON", StringComparison.Ordinal) && message.Contains(path, StringComparison.Ordinal), message);
    }

    [Fact]
    public void A_corrupt_unrelated_candidate_is_skipped_when_another_matches()
    {
        using TempTree tree = new();
        tree.Write("a/b/both.json", string.Empty);
        tree.Write("aaa/both.json", "{");
        tree.Write("mmm/both.json", """{"full_name":"X.Y.Both"}""");
        tree.Write("zzz/both.json", """{"full_name":"A.B.Both"}""");

        Assert.Equal("A.B.Both", CanonicalJson.LoadCanonicalPowers(tree.Root, ["A.B.Both"])["A.B.Both"].ReqStr("full_name"));
    }

    [Fact]
    public void A_colon_name_loads_from_its_sanitized_file()
    {
        using TempTree tree = new();
        string path = CanonicalJson.CanonicalPowerPath(tree.Root, Colon);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, $"{{\"full_name\":\"{Colon}\"}}");

        Assert.Equal(Colon, CanonicalJson.LoadCanonicalPowers(tree.Root, [Colon])[Colon].ReqStr("full_name"));
    }

    [Fact]
    public void The_real_colon_power_loads()
    {
        Assert.Equal(Colon, Real(Colon).ReqStr("full_name"));
    }

    [Theory]
    [MemberData(nameof(Linked))]
    public void A_real_support_power_has_its_role(string name, Role role)
    {
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Powers);

        Assert.Equal(role, RoleOf(resolve(name)!.Value, resolve));
    }

    [Fact]
    public void A_linked_buff_needs_the_resolver()
    {
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Powers);

        Assert.StartsWith("E37", ErrorOf(resolve("Defender_Buff.Kinetics.Siphon_Power")!.Value), StringComparison.Ordinal);
    }

    [Fact]
    public void A_location_debuff_without_a_resolver_is_a_patch()
    {
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Powers);

        Assert.Equal(Role.Patch, RoleOf(resolve("Defender_Buff.Dark_Miasma.Tar_Patch")!.Value));
    }

    [Fact]
    public void A_nova_form_toggle_is_e37_and_a_nova_attack_carries_its_mode()
    {
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Powers);

        Assert.StartsWith("E37", ErrorOf(resolve("Peacebringer_Offensive.Luminous_Blast.Bright_Nova")!.Value, resolve), StringComparison.Ordinal);
        Assert.Equal("Peacebringer_Blaster_Mode", Assert.Single(AttackProfiles.Classify(resolve("Peacebringer_Offensive.Luminous_Blast.Bright_Nova_Blast")!.Value).ModesRequired));
        Assert.Empty(AttackProfiles.Classify(resolve("Sentinel_Ranged.Archery.Aim")!.Value).ModesRequired);
    }

    [Fact]
    public void An_ally_resist_shield_is_not_a_debuff()
    {
        Assert.StartsWith("E37", ErrorOf(Inline(Effect("Smashing_Dmg", "Resistance", "AnyAffected", "Ranged_Res_Dmg", 2.0))), StringComparison.Ordinal);
    }

    [Fact]
    public void A_positive_scale_on_a_debuff_table_is_still_a_debuff()
    {
        string debuff = Effect("Smashing_Dmg", "Resistance", "AnyAffected", "Ranged_Debuff_Res_Dmg", 3.0);

        Assert.Equal([Role.Support, Role.PersistentDebuff], [RoleOf(Inline(debuff)), RoleOf(Inline(debuff, "Auto"))]);
    }

    [Fact]
    public void Heal_dmg_is_neither_damage_nor_resistance()
    {
        string heal = Effect("Heal_Dmg", "Resistance", "AnyAffected", "Ranged_Res_Dmg", -2.0);
        string healingHit = Effect("Heal_Dmg", "Absolute", "AnyAffected", "Ranged_Damage");

        Assert.Equal([true, true], [ErrorOf(Inline(heal)).StartsWith("E37", StringComparison.Ordinal), ErrorOf(Inline(healingHit)).StartsWith("E37", StringComparison.Ordinal)]);
    }

    [Fact]
    public void A_team_damage_buff_click_is_support_and_a_toggle_is_e37()
    {
        string team = Effect("Fire_Dmg", "Strength", "AnyAffected", "Ranged_Buff_Dmg");

        Assert.Equal("Support True", $"{RoleOf(Inline(team))} {ErrorOf(Inline(team, "Toggle")).StartsWith("E37", StringComparison.Ordinal)}");
    }

    [Fact]
    public void A_self_buff_click_aimed_at_self_and_pets_is_a_buff_click()
    {
        Assert.Equal(Role.BuffClick, RoleOf(Inline(Effect("Fire_Dmg", "Strength", "SelfAndPets", "Melee_Buff_Dmg"))));
    }

    [Fact]
    public void Entity_roles_follow_linked_effects()
    {
        string spawn = Effect("Create_Entity", "Current", "Self", "Ones", extra: ",\"params\":{\"redirects\":[\"L.Res\"]}");
        string both = Effect("Create_Entity", "Current", "Self", "Ones", extra: ",\"params\":{\"redirects\":[\"L.Res\",\"L.Dmg\",\"L.Gone\"]}");
        Dictionary<string, JsonElement> pets = new(StringComparer.Ordinal)
        {
            ["L.Res"] = CanonicalPowers.Parse($"{{\"effects\":[{Effect("Fire_Dmg", "Resistance", "AnyAffected", "Ranged_Debuff_Res_Dmg", -3.0)}]}}"),
            ["L.Dmg"] = CanonicalPowers.Parse($"{{\"effects\":[{Effect("Fire_Dmg", "Absolute", "AnyAffected", "Ranged_Damage")}]}}"),
        };

        Assert.Equal([Role.Support, Role.Patch], [RoleOf(Inline(spawn), Lookup(pets)), RoleOf(Inline(both), Lookup(pets))]);
    }

    [Fact]
    public void Entity_def_powers_resolve_through_the_entity_file()
    {
        string spawn = Effect("Create_Entity", "Current", "Self", "Ones", extra: ",\"params\":{\"entity_def\":\"Ent_X\"}");
        Dictionary<string, JsonElement> lookup = new(StringComparer.Ordinal)
        {
            ["Ent_X"] = CanonicalPowers.Parse("""{"defaults":{"power_full_names":["L.Res"]}}"""),
            ["L.Res"] = CanonicalPowers.Parse($"{{\"effects\":[{Effect("Fire_Dmg", "Resistance", "AnyAffected", "Ranged_Debuff_Res_Dmg", -3.0)}]}}"),
        };

        Assert.Equal([Role.Support, Role.Patch], [RoleOf(Inline(spawn), Lookup(lookup)), RoleOf(Inline(spawn), _ => null)]);
    }

    [Fact]
    public void An_execute_power_self_buff_counts_whatever_its_target()
    {
        string link = Effect("Execute_Power", "Absolute", "Self", "Ranged_Ones", extra: ",\"params\":{\"power_names\":[\"L.Buff\"]}");
        Dictionary<string, JsonElement> lookup = new(StringComparer.Ordinal)
        {
            ["L.Buff"] = CanonicalPowers.Parse($"{{\"effects\":[{Effect("Fire_Dmg", "Strength", "AnyAffected", "Ranged_Buff_Dmg")}]}}"),
        };

        Assert.Equal(Role.BuffClick, RoleOf(Inline(link), Lookup(lookup)));
    }

    [Fact]
    public void The_resolver_gives_null_for_missing_empty_and_corrupt_files_and_caches()
    {
        using TempTree tree = new();
        tree.Write("a/b/good.json", """{"full_name":"A.B.Good"}""");
        tree.Write("a/b/empty.json", string.Empty);
        tree.Write("a/b/bad.json", "{");
        PowerResolver lookup = CanonicalJson.CanonicalResolver(tree.Root);

        Assert.Equal("A.B.Good", lookup("A.B.Good")!.Value.ReqStr("full_name"));
        Assert.Equal([true, true, true], [lookup("A.B.Gone") is null, lookup("A.B.Empty") is null, lookup("A.B.Bad") is null]);
        File.Delete(CanonicalJson.CanonicalPowerPath(tree.Root, "A.B.Good"));
        Assert.NotNull(lookup("A.B.Good"));
    }

    [Fact]
    public void The_resolver_reads_entities_beside_the_powers_root()
    {
        using TempTree tree = new();
        tree.Write("entities/pets_x.json", """{"defaults":{}}""");
        tree.Write("entities/pets_bad.json", "{");
        PowerResolver lookup = CanonicalJson.CanonicalResolver(tree.FullPath("powers"));

        Assert.Equal([true, true, true], [lookup("Pets_X") is not null, lookup("Pets_Y") is null, lookup("Pets_Bad") is null]);
    }

    [Fact]
    public void A_chain_area_is_a_targeted_aoe_and_a_pet_category_makes_a_summon()
    {
        string spawn = Effect("Create_Entity", "Current", "AnyAffected");

        Assert.Equal("TargetedAoe Patch PetSummon", $"{AttackProfiles.Classify(Inline(Effect("Fire_Dmg", "Absolute", "AnyAffected"), area: "Chain")).Shape} {RoleOf(Inline(spawn))} {RoleOf(Inline(spawn, cats: ["Pet Damage"]))}");
    }
}
