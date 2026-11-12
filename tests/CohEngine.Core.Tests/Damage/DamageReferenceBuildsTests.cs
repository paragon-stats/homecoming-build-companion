using System.Text.Json;

using CohEngine.Core.Chain;
using CohEngine.Core.Damage;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Maths;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Damage;

// Ported from tests/test_damage_reference.py, test_attack_profile.py and the loader cases that read real or edited dumps.
public sealed class DamageReferenceBuildsTests
{
    private const string Css = "Arachnos_Soldiers.Crab_Spider_Soldier.";

    private static readonly string Builds = Path.Join(Fixtures.Store.MidsDirectory, "builds");

    public static TheoryData<string> BuildNames => [.. Fixtures.Store.BuildNames()];

    private static JsonElement Canonical(string name)
    {
        return CanonicalJson.LoadCanonicalPowers(Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical", "powers"), [name])[name];
    }

    [Fact]
    public void A_target_damage_debuff_is_not_a_self_buff()
    {
        PowerDamageRef reference = DamageReference.LoadDamageRefs(Path.Join(Builds, "arsenal_dominator_cardiac"))["Dominator_Control.Arsenal_Control.Tear_Gas"];

        Assert.Equal((0.0, 0.0), (reference.SelfDamageBuff, reference.TargetResDebuff));
    }

    [Theory]
    [MemberData(nameof(BuildNames))]
    public void Every_fixture_build_loads(string build)
    {
        IReadOnlyDictionary<string, PowerDamageRef> refs = DamageReference.LoadDamageRefs(Path.Join(Builds, build));

        Assert.True(refs.Count > 0 && refs.All(pair => string.Equals(pair.Key, pair.Value.FullName, StringComparison.Ordinal)));
    }

    [Fact]
    public void A_redirected_power_keeps_the_picked_name_and_the_redirect_damage()
    {
        PowerDamageRef reference = DamageReference.LoadDamageRefs(Path.Join(Builds, "arsenal_dominator_cardiac"))["Dominator_Assault.Arsenal_Assault.Sniper_Rifle"];

        Assert.Equal(F32.Round(237.73366), reference.DamagePerActivation);
    }

    [Fact]
    public void A_minus_res_toggle_folds_like_a_click_and_only_the_power_type_differs()
    {
        using TempTree tree = new();
        string[] types = ["Smashing", "Lethal", "Fire", "Cold", "Energy", "Negative", "Toxic", "Psionic"];
        string enhanced = $$"""[{"BuildIndex":1,"DamagePerActivation":0,"CastTime":1,"RechargeTime":2,"EndCost":3,"Effects":[{{string.Join(",", types.Select((_, index) => $$"""{"Index":{{index}},"Math_Mag":-0.3,"Math_Duration":20}"""))}}]}]""";
        string structural = $$"""[{"BuildIndex":1,"FullName":"P.Field","ActivatePeriod":2.0,"PowerType":"Toggle","StatInclude":true,"Effects":[{{string.Join(",", types.Select((type, index) => $$"""{"Index":{{index}},"EffectType":"Resistance","ToWho":"Target","DamageType":"{{type}}"}"""))}}]}]""";
        tree.Write("enhanced_powers.json", enhanced);
        tree.Write("powers_effects.json", structural);

        PowerDamageRef reference = DamageReference.LoadDamageRefs(tree.Root)["P.Field"];

        Assert.Equal(("Toggle", 2.0, F32.Round(0.3), 20.0), (reference.PowerType, reference.ActivatePeriod, reference.TargetResDebuff, reference.TargetResDebuffDuration));
    }

    [Fact]
    public void The_loaded_references_are_in_dump_order()
    {
        Assert.Equal("Arsenal", DamageReference.LoadDamageRefs(Path.Join(Builds, "arsenal_dominator_cardiac")).Keys.First().Split('.')[1][..7]);
    }

    [Fact]
    public void Arm_lash_keeps_its_range_target_cap_arc_and_categories()
    {
        AttackProfile armLash = AttackProfiles.Classify(Canonical(Css + "Arm_Lash"));

        Assert.Equal((10.0, 5), (armLash.RangeFt, armLash.MaxTargets));
        Near.Abs(0.5236, armLash.ArcRad, 1e-6 + 1e-6);
        Assert.Contains("Melee AoE Damage", armLash.SetCategories, StringComparer.Ordinal);
        Assert.Equal(10.0, AttackProfiles.Classify(Canonical(Css + "Frenzy")).RadiusFt);
    }

    [Fact]
    public void An_empty_canonical_file_alone_is_e35()
    {
        using TempTree tree = new();
        tree.Write("a/b/empty.json", string.Empty);

        Assert.StartsWith(ErrorCodes.E35, Assert.Throws<EngineException>(() => CanonicalJson.LoadCanonicalPowers(tree.Root, ["A.B.Empty"])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_melee_threshold_is_a_parameter_and_real_powers_agree_with_their_categories()
    {
        JsonElement armLash = Canonical(Css + "Arm_Lash");

        Assert.Equal(Delivery.Ranged, AttackProfiles.Classify(armLash, 5.0).Delivery);
        Assert.Null(AttackProfiles.CategoryDisagreement(AttackProfiles.Classify(armLash)));
    }

    [Fact]
    public void The_path_maps_a_colon_and_lower_cases()
    {
        string expected = Path.Join("root", "training_gadgets", "training_and_gadgets", "tactical_training__assault.json");

        Assert.Equal(expected, CanonicalJson.CanonicalPowerPath("root", "Training_Gadgets.Training_and_Gadgets.Tactical_Training:_Assault"));
    }
}
