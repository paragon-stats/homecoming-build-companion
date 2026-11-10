using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Pets;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Chain;

public sealed class ChainSetupTests
{
    private const string Brawl = "Inherent.Inherent.Brawl";
    private const string AcidMortar = "Mastermind_Buff.Traps.Acid_Mortar";

    private static readonly string Build = Path.Join(Fixtures.Store.MidsDirectory, "builds", "shield_scrapper_set_bonuses");
    private static readonly string AttribModDirectory = Path.Join(Fixtures.Store.Root, "attribmod");

    private static ChainRequest Request(string? build = null, string? clicksOff = null, string? midsDb = null, string move = "run", double? standoff = null, string? mids = null)
    {
        return new ChainRequest
        {
            BuildDir = build ?? Build,
            MidsDir = mids ?? Fixtures.Store.MidsDirectory,
            ClicksOffDir = clicksOff,
            MidsDb = midsDb,
            Move = move,
            StandoffFt = standoff,
            RepoRoot = Fixtures.Store.RepositoryRoot,
        };
    }

    private static string Error(ChainRequest request, ChainHooks? hooks = null)
    {
        return Assert.Throws<EngineException>(() => ChainReport.BuildChainSetup(request, hooks)).Message;
    }

    private static (IReadOnlyList<ChainAttack> Attacks, IReadOnlyList<(string Name, string Reason)> Skipped) Mortar(AttackProfile profile)
    {
        ChainAttack mortar = new() { FullName = AcidMortar, Damage = 0.0, CastTime = 1.0, RechargeTime = 60.0, EndCost = 5.0, Profile = profile with { Role = Role.Patch } };
        return ([mortar, ChainFactory.Attack("Other")], []);
    }

    private static ChainHooks Forced(string name, Role role, Func<IReadOnlyList<ChainAttack>, string, AttribMods, ArchetypeDb, IReadOnlyList<PetSummon>>? pets = null)
    {
        return new ChainHooks
        {
            Classify = (power, range, resolve) =>
            {
                AttackProfile profile = AttackProfiles.Classify(power, range, resolve);
                return string.Equals(profile.FullName, name, StringComparison.Ordinal) ? profile with { Role = role } : profile;
            },
            BuildPets = pets,
        };
    }

    [Fact]
    public void The_chain_rules_are_registered_in_order()
    {
        Assert.Equal("_rule_end_002,_rule_chain_001,_rule_chain_002,_rule_chain_002_skipped", string.Join(',', ChainReport.RegisteredChainRules()));
    }

    [Fact]
    public void A_dump_loads_into_a_melee_context_for_its_class()
    {
        ChainSetup setup = ChainReport.BuildChainSetup(Request());

        Assert.Equal("Class_Scrapper Melee True 4 40", $"{setup.Context.ArchetypeClass} {setup.Context.Stance} {setup.ClicksAlwaysOn} {setup.Context.Assume("chain.min_cycles", 0.0)} {setup.Context.Movement.StandoffFt}");
    }

    [Fact]
    public void The_damage_baseline_adds_the_totals_buff_and_the_assumed_enhancement()
    {
        ChainSetup setup = ChainReport.BuildChainSetup(Request() with { Assumptions = new Dictionary<string, double>(StringComparer.Ordinal) { ["damage.enhancement_avg"] = 0.5 } });

        Assert.Equal(1.0 + setup.Capped.BuffDam + 0.5, setup.Context.DamageBuffBaseline, 5);
    }

    [Fact]
    public void An_explicit_clicks_off_directory_wins()
    {
        using TempTree tree = new();
        string off = tree.CopyFiles(Build, "off");

        Assert.Equal(off, ChainReport.BuildChainSetup(Request(clicksOff: off)).Source);
    }

    [Fact]
    public void A_missing_explicit_clicks_off_directory_is_e65()
    {
        Assert.StartsWith("E65: clicks-off directory", Error(Request(clicksOff: Path.Join(Fixtures.Store.Root, "nope"))), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("jump")]
    [InlineData("run")]
    public void A_movement_mode_reads_its_speed_from_the_capped_totals(string move)
    {
        ChainSetup setup = ChainReport.BuildChainSetup(Request(move: move));

        Assert.Equal(string.Equals(move, "run", StringComparison.Ordinal) ? setup.Capped.RunSpd : setup.Capped.JumpSpd, setup.Context.Movement.SpeedFtPerSec);
    }

    [Fact]
    public void A_mode_with_no_speed_is_e69()
    {
        Assert.StartsWith("E69: movement mode 'fly' has speed 0 ft/s in this build", Error(Request(move: "fly")), StringComparison.Ordinal);
    }

    [Fact]
    public void A_standoff_that_is_not_positive_is_e70()
    {
        Assert.StartsWith("E70: standoff is 0 ft but must be positive", Error(Request(standoff: 0.0)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_totals_file_is_e65()
    {
        using TempTree tree = new();

        Assert.StartsWith("E65: totals file", Error(Request(tree.Root)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_missing_archetype_table_is_e65()
    {
        using TempTree tree = new();

        Assert.StartsWith("E65: archetype table", Error(Request(mids: tree.Root)), StringComparison.Ordinal);
    }

    [Fact]
    public void A_class_missing_from_the_table_is_e65()
    {
        using TempTree tree = new();
        string build = tree.CopyFiles(Build, "b");
        File.WriteAllText(Path.Join(build, "totals.json"), File.ReadAllText(Path.Join(Build, "totals.json")).Replace("Class_Scrapper", "Class_Nope", StringComparison.Ordinal));

        Assert.StartsWith("E65: class 'Class_Nope' is not in", Error(Request(build)), StringComparison.Ordinal);
    }

    [Fact]
    public void Only_e37_drops_a_power_while_classifying()
    {
        ChainHooks nothing = new() { Classify = (_, _, _) => throw new EngineException("E37: not damaging") };

        Assert.StartsWith("E71: no power in", Error(Request(), nothing), StringComparison.Ordinal);
    }

    [Fact]
    public void Any_other_classifier_error_propagates()
    {
        ChainHooks broken = new() { Classify = (_, _, _) => throw new EngineException("E36: bad area") };

        Assert.StartsWith(ErrorCodes.E36, Error(Request(), broken), StringComparison.Ordinal);
    }

    [Fact]
    public void A_damaging_power_the_classifier_rejects_is_named_as_skipped()
    {
        ChainHooks hooks = new()
        {
            Classify = (power, range, resolve) => string.Equals(power.ReqStr("full_name"), "Scrapper_Melee.Dark_Melee.Smite", StringComparison.Ordinal)
                ? throw new EngineException("E37: no foe damage")
                : AttackProfiles.Classify(power, range, resolve),
        };

        ChainSetup setup = ChainReport.BuildChainSetup(Request(), hooks);

        Assert.Equal("not classifiable as an attack (E37: no foe damage)", setup.Skipped.Single(skip => skip.Name.EndsWith(".Smite", StringComparison.Ordinal)).Reason);
    }

    [Fact]
    public void A_summon_without_the_attribute_tables_is_e66()
    {
        Assert.StartsWith("E66: ['Inherent.Inherent.Brawl'] summon pets but --mids-db is not set", Error(Request(), Forced(Brawl, Role.PetSummon)), StringComparison.Ordinal);
    }

    [Fact]
    public void Summons_go_through_the_pet_builder_when_the_tables_are_given()
    {
        PetSummon pet = new() { SummonName = Brawl, PetName = "Imp", PetAttacks = [], PetCount = 1, Duration = 1.0, SummonCastTime = 0.0, SummonRecharge = 0.0, SummonEndCost = 0.0 };
        ChainHooks hooks = Forced(Brawl, Role.PetSummon, (_, _, _, _) => [pet]);

        Assert.Equal("Imp", Assert.Single(ChainReport.BuildChainSetup(Request(midsDb: AttribModDirectory), hooks).Context.Pets).PetName);
    }

    [Fact]
    public void A_pet_data_gap_skips_that_summon_and_reports_it()
    {
        ChainHooks hooks = Forced(Brawl, Role.PetSummon, (_, _, _, _) => throw new EngineException("E61: entity X has class 'q' with no class"));

        ChainSetup setup = ChainReport.BuildChainSetup(Request(midsDb: AttribModDirectory), hooks);

        Assert.Equal("pet not modelled, data gap (E61: entity X has class 'q' with no class)", setup.Skipped.Single(skip => string.Equals(skip.Name, Brawl, StringComparison.Ordinal)).Reason);
    }

    [Fact]
    public void A_pet_error_that_is_not_a_data_gap_propagates()
    {
        ChainHooks hooks = Forced(Brawl, Role.PetSummon, (_, _, _, _) => throw new EngineException("E36: unknown effect_area"));

        Assert.StartsWith(ErrorCodes.E36, Error(Request(midsDb: AttribModDirectory), hooks), StringComparison.Ordinal);
    }

    [Fact]
    public void The_real_pet_builder_runs_when_no_replacement_is_given()
    {
        ChainSetup setup = ChainReport.BuildChainSetup(Request(midsDb: AttribModDirectory), Forced(Brawl, Role.PetSummon));

        Assert.StartsWith("pet not modelled, data gap (E62", setup.Skipped.Single(skip => string.Equals(skip.Name, Brawl, StringComparison.Ordinal)).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Acid_mortars_minus_res_is_read_from_its_entity()
    {
        AttribMods mods = Fixtures.Store.AttribMods;
        ArchetypeDb classes = Fixtures.Store.Archetypes;
        string powers = Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical", "powers");
        PowerResolver resolve = CanonicalJson.CanonicalResolver(powers);

        (double magnitude, double duration) = ChainReport.LinkedResDebuff(resolve(AcidMortar)!.Value, resolve, mods, classes, classes.NidFromUidClass("Class_Mastermind"));

        Assert.True(magnitude > 0.0 && duration == 60.0);
    }

    [Fact]
    public void An_unknown_table_in_a_debuff_template_is_e61()
    {
        string template = CanonicalPowers.Template(["Fire_Dmg"], "Resistance", "Foe", "No_Such_Debuff_Res_Dmg", -1.0, ",\"duration\":\"10 seconds\"");
        System.Text.Json.JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text("Tst.Set.Melt", effects: [CanonicalPowers.Effect([template])]));

        Assert.StartsWith("E61: Tst.Set.Melt uses modifier table 'No_Such_Debuff_Res_Dmg'", Assert.Throws<EngineException>(() => ChainReport.LinkedResDebuff(power, _ => null, Fixtures.Store.AttribMods, Fixtures.Store.Archetypes, 0)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_debuff_that_misses_a_core_type_counts_zero_and_a_bad_duration_reads_zero()
    {
        string template = CanonicalPowers.Template(["Fire_Dmg"], "Resistance", "Foe", "Ranged_Debuff_Res_Dmg", -1.0, ",\"duration\":\"soon\"");
        System.Text.Json.JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text("Tst.Set.Melt", effects: [CanonicalPowers.Effect([template])]));

        Assert.Equal((0.0, 0.0), ChainReport.LinkedResDebuff(power, _ => null, Fixtures.Store.AttribMods, Fixtures.Store.Archetypes, 0));
    }

    [Fact]
    public void An_entity_spawning_effect_extends_the_duration()
    {
        string summon = CanonicalPowers.Template(["Create_Entity"], "Absolute", "Self", "Ones", 1.0, ",\"duration\":\"45.5 seconds\"");
        System.Text.Json.JsonElement power = CanonicalPowers.Parse(CanonicalPowers.Text("Tst.Set.Mortar", effects: [CanonicalPowers.Effect([summon])]));

        Assert.Equal(45.5, ChainReport.LinkedResDebuff(power, _ => null, Fixtures.Store.AttribMods, Fixtures.Store.Archetypes, 0).Duration);
    }

    [Fact]
    public void A_patch_with_foe_res_in_its_canonical_data_but_none_in_the_dump_is_reported_without_the_tables()
    {
        AttackProfile profile = AttackProfiles.Classify(CanonicalJson.CanonicalResolver(Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical", "powers"))(AcidMortar)!.Value);
        ChainHooks hooks = new()
        {
            Attacks = (refs, canonical, range) => Mortar(profile),
        };

        Assert.Contains("--mids-db", ChainReport.BuildChainSetup(Request(), hooks).Skipped.Single(skip => string.Equals(skip.Name, AcidMortar, StringComparison.Ordinal)).Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void With_the_tables_the_patch_debuff_is_filled()
    {
        AttackProfile profile = AttackProfiles.Classify(CanonicalJson.CanonicalResolver(Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical", "powers"))(AcidMortar)!.Value);
        ChainHooks hooks = new()
        {
            Attacks = (refs, canonical, range) => Mortar(profile),
        };

        ChainSetup setup = ChainReport.BuildChainSetup(Request(midsDb: AttribModDirectory), hooks);

        Assert.True(setup.Context.Attacks.Single(attack => string.Equals(attack.FullName, AcidMortar, StringComparison.Ordinal)).TargetResDebuff > 0.0);
    }
}
