using System.Text.Json;
using System.Text.Json.Nodes;

using CohEngine.Core.Chain;
using CohEngine.Core.DamageLayers;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.TestSupport;

namespace CohEngine.Core.Tests.Chain;

// Ported from tests/test_chain_report.py (the fixture-build cases), on the shield_scrapper_set_bonuses dump.
public sealed class ChainReportPortTests
{
    private const string Moonbeam = "Epic.Stalker_Soul_Mastery.Moonbeam";

    private static readonly string Build = Path.Join(Fixtures.Store.MidsDirectory, "builds", "shield_scrapper_set_bonuses");
    private static readonly string[] Needles = ["== melee stance ==", "rotation: Scrapper_Melee.Dark_Melee.Shadow_Maul > Scrapper_Melee.Dark_Melee.Siphon_Life > ", "cycle ", "total:", "x1.100", "not sustainable", "health: max", "best sustainable: ", "best overall: ", "P-CHAIN-001", "    Scrapper_Melee.Dark_Melee.Smite: "];
    private static readonly string[] VirtualNames = ["A.B.Summon", "A.B.Plain", "A.B.Hit", "A.B.Virtual"];
    private static readonly string[] ExpectedSkips = ["Inherent.Inherent.Special_Set_Bonuses", "Incarnate.Lore.X"];
    private static readonly Stance[] AllStances = [Stance.Melee, Stance.Ranged, Stance.Hybrid];

    public static TheoryData<string> BadAssumptions => ["a", "a=x", "=1", "a=nan", "a=inf", "a=-inf", "a=-Infinity"];

    public static TheoryData<string> BadTargets => ["many", "spawn", "spawn:0", "spawn:x", "spawn:-1", "spawn:²", "spawn:٣"];

    private static ChainSetup Setup(string? build = null, double? standoff = null, IReadOnlyDictionary<string, double>? assumptions = null, Scenario? scenario = null, ChainHooks? hooks = null)
    {
        return ChainReport.BuildChainSetup(
            new ChainRequest { BuildDir = build ?? Build, MidsDir = Fixtures.Store.MidsDirectory, RepoRoot = Fixtures.Store.RepositoryRoot, StandoffFt = standoff, Assumptions = assumptions, Scenario = scenario },
            hooks);
    }

    private static string Rules(params StanceReport[] results)
    {
        return string.Join(',', results.SelectMany(result => result.Diagnostics).Select(diagnostic => diagnostic.RuleId));
    }

    private static ChainSetup WithoutMoonbeam(ChainSetup setup)
    {
        return setup with { Context = setup.Context with { Attacks = [.. setup.Context.Attacks.Where(attack => !string.Equals(attack.FullName, Moonbeam, StringComparison.Ordinal))] } };
    }

    private static ChainSetup Drained(ChainSetup setup)
    {
        return setup with { Context = setup.Context with { Budget = new EnduranceBudget(100.0, 0.0, 50.0) } };
    }

    private static ChainSetup SingleAttack(double maxEnd)
    {
        AttackProfile profile = new("Bolt", Delivery.Ranged, Shape.SingleTarget, Role.Chain, 80.0, 0.0, 0.0, 0, [], []);
        ChainAttack bolt = new() { FullName = "Bolt", Damage = 100.0, CastTime = 1.0, RechargeTime = CohEngine.Core.Maths.F32.Round(10.0 - AttackChains.ArcanaCastTime(1.0)), EndCost = 5.0, Profile = profile };
        ChainSetup setup = Setup();
        return setup with { Context = setup.Context with { Attacks = [bolt], Budget = new EnduranceBudget(maxEnd, 0.0, 0.0) } };
    }

    private static ChainSetup TwoForms()
    {
        ChainSetup setup = Setup();
        ChainAttack first = setup.Context.Attacks[0];
        ChainAttack nova = first with { FullName = "Nova.Bolt", Profile = first.Profile with { ModesRequired = ["Nova"] } };
        return setup with { Context = setup.Context with { Attacks = [.. setup.Context.Attacks, nova] } };
    }

    private static JsonElement Report(ChainSetup setup, StanceReport result)
    {
        return CanonicalPowers.Parse(PyJson.Dumps(ChainReport.StanceToJson(setup, result)));
    }

    private static string Source(StanceReport result, string name, Func<SourceResult, string> read)
    {
        return read(result.Report.Sources.Single(source => string.Equals(source.Source, name, StringComparison.Ordinal)));
    }

    [Fact]
    public void A_real_dump_reports_every_stance_with_positive_dps()
    {
        IReadOnlyList<StanceReport> results = ChainReport.RunChain(Setup(), AllStances);

        Assert.Equal(AllStances, results.Select(result => result.Stance));
        Assert.True(results[0].Report.ScaledDps.Single(pair => string.Equals(pair.Key, "chain", StringComparison.Ordinal)).Value > 0.0 && results[2].Report.ScaledDps.Single(pair => string.Equals(pair.Key, "chain", StringComparison.Ordinal)).Value > 0.0);
        Assert.Equal(Moonbeam, Assert.Single(results[1].Chain!.Rotation));
        Assert.All(results, result => Assert.True(result.Report.TotalDps > 0.0));
    }

    [Fact]
    public void Without_a_clicks_off_dump_an_info_says_click_buffs_count_as_always_on()
    {
        ChainSetup setup = Setup();

        string[] messages = [.. ChainReport.RunChain(setup, [Stance.Melee])[0].Diagnostics.Where(diagnostic => string.Equals(diagnostic.RuleId, "P-CHAIN-002", StringComparison.Ordinal)).Select(diagnostic => diagnostic.Message)];

        Assert.True(setup.ClicksAlwaysOn);
        Assert.Contains(messages, message => message.Contains("click buffs are counted always-on", StringComparison.Ordinal));
        Assert.Contains(messages, message => message.Contains("Shield_Charge", StringComparison.Ordinal));
    }

    [Fact]
    public void A_clicks_off_sibling_supplies_the_damage_figures()
    {
        using TempTree tree = new();
        string build = tree.CopyFiles(Build, "b");
        string off = tree.CopyFiles(Build, "b_clicksoff");
        JsonArray powers = JsonNode.Parse(File.ReadAllText(Path.Join(off, "enhanced_powers.json")))!.AsArray();
        powers.First(node => node!["FullName"]!.GetValue<string>().EndsWith(".Smite", StringComparison.Ordinal))!["DamagePerActivation"] = 1234.5;
        File.WriteAllText(Path.Join(off, "enhanced_powers.json"), powers.ToJsonString());

        ChainSetup setup = Setup(build);

        Assert.Equal("False 1234.5 False", $"{setup.ClicksAlwaysOn} {setup.Context.Attacks.Single(attack => attack.FullName.EndsWith(".Smite", StringComparison.Ordinal)).Damage} {ChainReport.RunChain(setup, [Stance.Melee]).SelectMany(result => result.Diagnostics).Any(diagnostic => diagnostic.Message.Contains("always-on", StringComparison.Ordinal))}");
    }

    [Fact]
    public void Scenario_standoff_and_assumptions_flow_into_the_context_and_the_baseline_uses_the_average()
    {
        ChainSetup setup = Setup(standoff: 30.0, assumptions: new Dictionary<string, double>(StringComparer.Ordinal) { ["damage.enhancement_avg"] = 0.5, ["x.y"] = 1.0 }, scenario: ChainReport.ParseTarget("spawn:5"));
        ChainSetup plain = Setup();

        Assert.Equal("5 30 1 40", $"{setup.Context.Scenario.Targets} {setup.Context.Movement.StandoffFt} {setup.Context.Assumptions["x.y"]} {plain.Context.Movement.StandoffFt}");
        Assert.Equal(setup.Capped.RunSpd, setup.Context.Movement.SpeedFtPerSec);
        Near.Abs(1.0 + setup.Capped.BuffDam + 0.5, setup.Context.DamageBuffBaseline, 1e-5);
        Near.Abs(1.0 + setup.Capped.BuffDam + 0.95, plain.Context.DamageBuffBaseline, 1e-5);
    }

    [Fact]
    public void The_reported_chain_is_the_one_the_chain_source_ran_on()
    {
        static ChainAttack Bolt(string name, Role role, double buff = 0.0, double duration = 0.0)
        {
            AttackProfile profile = new(name, Delivery.Ranged, Shape.SingleTarget, role, 80.0, 0.0, 0.0, 0, [], []);
            return new ChainAttack { FullName = name, Damage = 100.0, CastTime = 1.0, RechargeTime = 5.0, EndCost = 4.0, Profile = profile, SelfDamageBuff = buff, SelfDamageBuffDuration = duration };
        }

        ChainSetup setup = Setup();
        setup = setup with { Context = setup.Context with { Attacks = [Bolt("Bolt", Role.Chain), Bolt("Patch", Role.Patch), Bolt("Buff", Role.BuffClick, 0.5, 5.0)] } };

        StanceReport result = ChainReport.RunChain(setup, [Stance.Ranged])[0];
        SourceResult chain = result.Report.Sources.Single(source => string.Equals(source.Source, "chain", StringComparison.Ordinal));

        Assert.Equal(chain.Dps, result.Chain!.Dps);
        Assert.Equal(string.Join(',', chain.Detail.Select(item => item.Name)), string.Join(',', result.Chain.Rotation));
    }

    [Fact]
    public void A_dump_with_a_redirecting_snipe_classifies_it_as_a_ranged_chain_attack_and_skips_nothing()
    {
        ChainSetup setup = Setup();
        ChainAttack moonbeam = setup.Context.Attacks.Single(attack => string.Equals(attack.FullName, Moonbeam, StringComparison.Ordinal));

        Assert.Equal("0 Chain Ranged", $"{setup.Skipped.Count} {moonbeam.Profile.Role} {moonbeam.Profile.Delivery}");
    }

    [Fact]
    public void Classification_receives_a_canonical_resolver()
    {
        List<PowerResolver?> seen = [];
        ChainHooks hooks = new()
        {
            Classify = (power, range, resolve) =>
            {
                seen.Add(resolve);
                return AttackProfiles.Classify(power, range, resolve);
            },
        };

        Setup(hooks: hooks);

        Assert.True(seen.Count > 0 && seen.All(resolve => resolve is not null));
    }

    private static Diagnostic[] DiagnosticsOf(ChainSetup setup, Stance stance)
    {
        return [.. ChainReport.RunChain(setup, [stance])[0].Diagnostics];
    }

    private static PowerDamageRef Ref(string name, double damage = 0.0)
    {
        return new PowerDamageRef(name, damage, 1.0, 1.0, 1.0, 0.0, "Click", true, 0.0, 0.0, 0.0, 0.0);
    }

    private static void WritePower(TempTree tree, string name, string effects)
    {
        tree.Write("powers/" + string.Join('/', name.ToLowerInvariant().Split('.')) + ".json", $"{{\"full_name\":\"{name}\",\"effects\":{effects}}}");
    }

    [Fact]
    public void Candidates_keep_entity_creators_and_skip_missing_virtual_powers()
    {
        using TempTree tree = new();
        WritePower(tree, "A.B.Summon", """[{"templates":[],"child_effects":[{"templates":[{"attribs":["Create_Entity"]}]}]}]""");
        WritePower(tree, "A.B.Plain", """[{"templates":[{"attribs":["Defense"]}]}]""");
        WritePower(tree, "A.B.Hit", """[{"templates":[{"attribs":["Defense"]}]}]""");
        Dictionary<string, PowerDamageRef> refs = VirtualNames.ToDictionary(name => name, name => Ref(name, string.Equals(name, "A.B.Hit", StringComparison.Ordinal) ? 5.0 : 0.0), StringComparer.Ordinal);

        (Dictionary<string, JsonElement> found, List<(string Name, string Reason)> skipped) = ChainReport.Candidates(refs, tree.FullPath("powers"));

        Assert.Equal("A.B.Hit,A.B.Summon A.B.Virtual", string.Join(',', found.Keys.Order(StringComparer.Ordinal)) + " " + string.Join(',', skipped.Select(skip => skip.Name)));
        Assert.Contains("E35", skipped[0].Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Inherent_and_incarnate_skips_are_not_recorded()
    {
        using TempTree tree = new();
        Dictionary<string, PowerDamageRef> refs = ExpectedSkips.ToDictionary(name => name, name => Ref(name), StringComparer.Ordinal);

        (Dictionary<string, JsonElement> found, List<(string Name, string Reason)> skipped) = ChainReport.Candidates(refs, tree.FullPath("powers"));

        Assert.True(found.Count == 0 && skipped.Count == 0);
    }

    [Fact]
    public void A_toggle_damage_buff_needs_no_canonical_file_and_a_click_buff_does()
    {
        using TempTree tree = new();
        PowerDamageRef toggle = Ref("A.B.Assault") with { PowerType = "Toggle", SelfDamageBuff = 0.1, SelfDamageBuffDuration = 0.75 };
        PowerDamageRef click = Ref("A.B.Aim") with { SelfDamageBuff = 0.625, SelfDamageBuffDuration = 10.0 };
        WritePower(tree, "A.B.Aim", """[{"templates":[{"attribs":["Smashing_Dmg"]}]}]""");

        (Dictionary<string, JsonElement> found, List<(string Name, string Reason)> skipped) = ChainReport.Candidates(new Dictionary<string, PowerDamageRef>(StringComparer.Ordinal) { ["A.B.Assault"] = toggle, ["A.B.Aim"] = click }, tree.FullPath("powers"));

        Assert.Equal("A.B.Aim A.B.Assault", string.Join(',', found.Keys.Order(StringComparer.Ordinal)) + " " + string.Join(',', skipped.Select(skip => skip.Name)));
    }

    [Fact]
    public void Incarnate_powers_are_never_candidates()
    {
        using TempTree tree = new();
        WritePower(tree, "Incarnate.Lore.Knives", """[{"templates":[{"attribs":["Create_Entity"]}]}]""");
        WritePower(tree, "Incarnate.Judgement.Ion", """[{"templates":[{"attribs":["Energy_Dmg"]}]}]""");
        Dictionary<string, PowerDamageRef> refs = new(StringComparer.Ordinal) { ["Incarnate.Lore.Knives"] = Ref("Incarnate.Lore.Knives"), ["Incarnate.Judgement.Ion"] = Ref("x", 300.0) };

        (Dictionary<string, JsonElement> found, List<(string Name, string Reason)> skipped) = ChainReport.Candidates(refs, tree.FullPath("powers"));

        Assert.True(found.Count == 0 && skipped.Count == 0);
    }

    [Fact]
    public void Candidates_propagate_errors_other_than_a_missing_file()
    {
        using TempTree tree = new();
        tree.Write("powers/a/b/c.json", "{oops");

        Assert.StartsWith(ErrorCodes.E38, Assert.Throws<EngineException>(() => ChainReport.Candidates(new Dictionary<string, PowerDamageRef>(StringComparer.Ordinal) { ["A.B.C"] = Ref("A.B.C") }, tree.FullPath("powers"))).Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(BadAssumptions))]
    public void A_bad_assumption_is_e67(string item)
    {
        Assert.StartsWith(ErrorCodes.E67, Assert.Throws<EngineException>(() => ChainReport.ParseAssumptions([item])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_good_assumption_followed_by_a_bad_one_is_e67()
    {
        Assert.StartsWith(ErrorCodes.E67, Assert.Throws<EngineException>(() => ChainReport.ParseAssumptions(["a=1", "b"])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void The_e67_hint_names_a_real_assumption_key()
    {
        Assert.Contains("--assume Class_Brute.fury_level=50", Assert.Throws<EngineException>(() => ChainReport.ParseAssumptions(["oops"])).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Assumptions_parse_to_floats()
    {
        IReadOnlyDictionary<string, double> parsed = ChainReport.ParseAssumptions(["a.b=0.5", " c = 2"]);

        Assert.Equal("0.5 2", $"{parsed["a.b"]} {parsed["c"]}");
    }

    [Theory]
    [MemberData(nameof(BadTargets))]
    public void A_bad_target_is_e68(string text)
    {
        Assert.StartsWith(ErrorCodes.E68, Assert.Throws<EngineException>(() => ChainReport.ParseTarget(text)).Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Targets_parse()
    {
        Assert.Equal("1 8", $"{ChainReport.ParseTarget("single").Targets} {ChainReport.ParseTarget("spawn:8").Targets}");
    }

    [Fact]
    public void P_end_002_warns_when_nothing_is_affordable_stating_the_floor_and_the_empty_time()
    {
        Diagnostic warning = ChainReport.RunChain(Drained(Setup()), [Stance.Melee])[0].Diagnostics.Single(diagnostic => string.Equals(diagnostic.RuleId, "P-END-002", StringComparison.Ordinal));

        Assert.True(string.Equals(warning.Severity, "warning", StringComparison.Ordinal) && ((string)warning.Expected!).StartsWith(">= 4 cycles (", StringComparison.Ordinal) && ((string)warning.Actual!).EndsWith(" s", StringComparison.Ordinal));
    }

    [Fact]
    public void A_chain_affordable_exactly_at_the_minimum_cycles_is_labelled_so_and_raises_no_warning()
    {
        StanceReport result = ChainReport.RunChain(SingleAttack(20.0), [Stance.Melee])[0];

        Assert.Equal("10 40 False", $"{result.Chain!.CycleTime} {result.Chain.TimeToEmpty} {Rules(result).Contains("P-END-002", StringComparison.Ordinal)}");
        Assert.Contains("affordable: empties in", Source(result, "chain", source => source.Label), StringComparison.Ordinal);
        Assert.DoesNotContain("not affordable", Source(result, "chain", source => source.Label), StringComparison.Ordinal);
    }

    [Fact]
    public void A_chain_just_below_the_minimum_cycles_is_not_affordable()
    {
        StanceReport result = ChainReport.RunChain(SingleAttack(19.5), [Stance.Melee])[0];

        Assert.Equal("39 True", $"{result.Chain!.TimeToEmpty} {Rules(result).Contains("P-END-002", StringComparison.Ordinal)}");
        Assert.Contains("not affordable", Source(result, "chain", source => source.Label), StringComparison.Ordinal);
    }

    [Fact]
    public void A_sustainable_chain_raises_no_endurance_warning()
    {
        Assert.DoesNotContain("P-END-002", Rules([.. ChainReport.RunChain(Setup(), AllStances)]), StringComparison.Ordinal);
    }

    [Fact]
    public void P_chain_001_reads_the_stance_defense_and_goes_quiet_at_the_soft_cap()
    {
        ChainSetup setup = Setup();
        StanceReport[] low = [.. ChainReport.RunChain(setup, AllStances)];
        string[] texts = [.. low.Select(result => result.Diagnostics.First(diagnostic => string.Equals(diagnostic.RuleId, "P-CHAIN-001", StringComparison.Ordinal)).Message)];

        Assert.Contains("melee defense is 14.5%", texts[0], StringComparison.Ordinal);
        Assert.Contains("ranged defense is 35.0%", texts[1], StringComparison.Ordinal);
        Assert.Contains("melee defense is 14.5%", texts[2], StringComparison.Ordinal);
        ChainSetup capped = setup with { Capped = setup.Capped with { Def = [.. Enumerable.Repeat(0.5, setup.Capped.Def.Count)] } };
        Assert.DoesNotContain("P-CHAIN-001", Rules([.. ChainReport.RunChain(capped, AllStances)]), StringComparison.Ordinal);
    }

    [Fact]
    public void The_json_view_is_complete_and_infinity_becomes_null()
    {
        ChainSetup setup = Setup();
        StanceReport[] results = [.. ChainReport.RunChain(setup, AllStances)];
        JsonElement melee = Report(setup, results[0]);
        JsonElement ranged = Report(setup, results[1]);
        string[] attacks = [.. melee.Req("rotation").Req("attacks").EnumerateArray().Select(item => item.GetString()!)];
        JsonElement items = melee.Req("dps").Req("detail").Req("chain");

        Assert.Equal("Scrapper_Melee.Dark_Melee.Shadow_Maul,Scrapper_Melee.Dark_Melee.Siphon_Life,Scrapper_Melee.Dark_Melee.Smite,Inherent.Inherent.Brawl", string.Join(',', attacks));
        Assert.Equal(string.Join(',', attacks), string.Join(',', items.EnumerateArray().Select(item => item.ReqStr("name"))));
        Near.Rel(melee.Req("dps").Req("unscaled").ReqNum("chain"), items.EnumerateArray().Sum(item => item.ReqNum("dps")), 1e-4);
        Assert.Contains("#31", melee.Req("dps").Req("labels").ReqStr("chain"), StringComparison.Ordinal);
        Assert.True(melee.Req("endurance").ReqNum("time_to_empty_s") >= 4 * melee.Req("rotation").ReqNum("cycle_time_s"));
        Assert.Equal(Moonbeam, ranged.Req("rotation").Req("attacks")[0].GetString());
        JsonElement alternatives = melee.Req("alternatives");
        Assert.True(alternatives.Req("best_overall").ReqNum("chain_dps") >= alternatives.Req("best_sustainable").ReqNum("chain_dps") && alternatives.Req("best_sustainable").ReqNum("chain_dps") > 0.0);
        Assert.True(alternatives.Req("best_sustainable").Req("sustainable").GetBoolean());
        Assert.True(melee.Req("health").ReqNum("max_hp") > 0.0 && melee.Req("modifiers")[0].ReqNum("factor") > 1.0);
        Assert.StartsWith("P-", melee.Req("diagnostics")[0].ReqStr("rule_id"), StringComparison.Ordinal);
    }

    [Fact]
    public void A_stance_with_nothing_to_cast_has_no_rotation_and_a_null_empty_time()
    {
        ChainSetup meleeOnly = WithoutMoonbeam(Setup());

        JsonElement bare = Report(meleeOnly, ChainReport.RunChain(meleeOnly, [Stance.Ranged])[0]);

        Assert.Equal("Null Null Null Null", $"{bare.Req("endurance").Req("time_to_empty_s").ValueKind} {bare.Req("rotation").ValueKind} {bare.Req("alternatives").Req("best_sustainable").ValueKind} {bare.Req("alternatives").Req("best_overall").ValueKind}");
    }

    [Fact]
    public void The_text_view_names_the_rotation_layers_modifiers_endurance_health_and_diagnostics()
    {
        ChainSetup setup = Setup();
        StanceReport[] results = [.. ChainReport.RunChain(setup, AllStances)];
        string text = ChainReport.StanceToText(setup, results[0]);

        foreach (string needle in Needles)
        {
            Assert.Contains(needle, text, StringComparison.Ordinal);
        }

        Assert.Contains($"rotation: {Moonbeam}", ChainReport.StanceToText(setup, results[1]), StringComparison.Ordinal);
    }

    [Fact]
    public void Text_for_a_castless_stance_and_a_drained_build()
    {
        ChainSetup meleeOnly = WithoutMoonbeam(Setup());
        string rangedText = ChainReport.StanceToText(meleeOnly, ChainReport.RunChain(meleeOnly, [Stance.Ranged])[0]);
        ChainSetup drained = Drained(Setup());
        string drainedText = ChainReport.StanceToText(drained, ChainReport.RunChain(drained, [Stance.Melee])[0]);

        Assert.Contains("rotation: none", rangedText, StringComparison.Ordinal);
        Assert.Contains("time to empty sustainable", rangedText, StringComparison.Ordinal);
        Assert.Contains("not sustainable", drainedText, StringComparison.Ordinal);
        Assert.Contains("P-END-002", drainedText, StringComparison.Ordinal);
        Assert.DoesNotContain("time to empty sustainable", drainedText, StringComparison.Ordinal);
    }

    [Fact]
    public void A_modifier_without_assumption_text_prints_a_placeholder_and_no_modifiers_prints_none()
    {
        ChainSetup setup = Setup();
        StanceReport result = ChainReport.RunChain(setup, [Stance.Melee])[0];
        StanceReport blank = result with { Report = result.Report with { Modifiers = [result.Report.Modifiers[0] with { Assumption = string.Empty }] } };
        StanceReport empty = result with { Report = result.Report with { Modifiers = [] } };

        Assert.Contains("no assumption", ChainReport.StanceToText(setup, blank), StringComparison.Ordinal);
        Assert.Contains("modifiers:\n  none", ChainReport.StanceToText(setup, empty), StringComparison.Ordinal);
    }

    [Fact]
    public void A_build_without_forms_has_only_the_base_form()
    {
        ChainSetup setup = Setup();
        StanceReport[] results = [.. ChainReport.RunChain(setup, AllStances)];

        Assert.Equal("|,,", string.Join(',', ChainReport.AvailableForms(setup.Context)) + "|" + string.Join(',', results.Select(result => result.Form)));
        Assert.True(ChainReport.StanceToText(setup, results[0]).StartsWith("== melee stance ==\n", StringComparison.Ordinal) && Report(setup, results[0]).ReqStr("form").Length == 0);
    }

    [Fact]
    public void Each_stance_runs_once_per_form_in_sorted_order()
    {
        ChainSetup setup = TwoForms();

        IReadOnlyList<StanceReport> results = ChainReport.RunChain(setup, [Stance.Melee, Stance.Ranged]);

        Assert.Equal(",Nova", string.Join(',', ChainReport.AvailableForms(setup.Context)));
        Assert.Equal("Melee: Melee:Nova Ranged: Ranged:Nova", string.Join(' ', results.Select(result => $"{result.Stance}:{result.Form}")));
    }

    [Fact]
    public void A_form_block_is_labelled_in_text_and_json()
    {
        ChainSetup setup = TwoForms();
        IReadOnlyList<StanceReport> results = ChainReport.RunChain(setup, [Stance.Melee]);

        Assert.Equal("True True Nova", $"{ChainReport.StanceToText(setup, results[0]).StartsWith("== melee stance ==\n", StringComparison.Ordinal)} {ChainReport.StanceToText(setup, results[1]).StartsWith("== melee stance, Nova form ==\n", StringComparison.Ordinal)} {Report(setup, results[1]).ReqStr("form")}");
    }

    [Fact]
    public void Forms_can_be_restricted_to_one()
    {
        ChainSetup setup = TwoForms();

        Assert.Equal("Nova|", string.Join(',', ChainReport.RunChain(setup, [Stance.Melee], forms: ["Nova"]).Select(result => result.Form)) + "|" + string.Join(',', ChainReport.RunChain(setup, [Stance.Melee], forms: [string.Empty]).Select(result => result.Form)));
    }

    [Fact]
    public void An_unknown_form_names_the_available_forms()
    {
        string message = Assert.Throws<EngineException>(() => ChainReport.RunChain(TwoForms(), [Stance.Melee], forms: ["Dwarf"])).Message;

        Assert.Equal("E65: form 'Dwarf' is not a form of this build; choose one of 'base', 'Nova' ('' is base)", message);
    }

    [Fact]
    public void The_base_form_has_no_rotation_when_every_attack_is_form_locked()
    {
        ChainSetup setup = Setup();
        setup = setup with { Context = setup.Context with { Attacks = [.. setup.Context.Attacks.Select(attack => attack with { Profile = attack.Profile with { ModesRequired = ["Nova"] } })] } };

        IReadOnlyList<StanceReport> results = ChainReport.RunChain(setup, [Stance.Melee]);

        Assert.Contains("rotation: none", ChainReport.StanceToText(setup, results[0]), StringComparison.Ordinal);
        Assert.NotNull(results[1].Chain);
    }

    [Fact]
    public void Freezing_rain_counts_once_and_melt_armor_has_its_own_templates()
    {
        AttribMods mods = Fixtures.Store.AttribMods;
        ArchetypeDb classes = Fixtures.Store.Archetypes;
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical", "powers"));
        int defender = classes.NidFromUidClass("Class_Defender");

        (double magnitude, _) = ChainReport.LinkedResDebuff(resolve("Defender_Buff.Storm_Summoning.Fog")!.Value, resolve, mods, classes, defender);
        (double meltMagnitude, double meltDuration) = ChainReport.LinkedResDebuff(resolve("Defender_Buff.Thermal_Radiation.Melt_Armor")!.Value, resolve, mods, classes, defender);
        double table = Math.Abs(CohEngine.Core.Totals.EffectMagnitude.Modifier(mods, classes.Classes, defender, mods.NidFromUidAttribMod("Ranged_Debuff_Res_Dmg"), 49));

        Near.Rel(4.0 * table, magnitude, 1e-5);
        Assert.True(meltMagnitude > 0.0 && meltDuration > 0.0);
    }

    [Fact]
    public void A_dumped_acid_mortar_debuff_matches_the_table_value_for_its_class()
    {
        AttribMods mods = Fixtures.Store.AttribMods;
        ArchetypeDb classes = Fixtures.Store.Archetypes;
        PowerResolver resolve = CanonicalJson.CanonicalResolver(Path.Join(Fixtures.Store.RepositoryRoot, "data", "canonical", "powers"));
        int mastermind = classes.NidFromUidClass("Class_Mastermind");

        (double magnitude, double duration) = ChainReport.LinkedResDebuff(resolve("Mastermind_Buff.Traps.Acid_Mortar")!.Value, resolve, mods, classes, mastermind);

        Near.Rel(2.0 * Math.Abs(CohEngine.Core.Totals.EffectMagnitude.Modifier(mods, classes.Classes, mastermind, mods.NidFromUidAttribMod("Ranged_Res_Dmg"), 49)), magnitude, 1e-5);
        Assert.Equal(60.0, duration);
    }

    [Fact]
    public void A_skipped_power_reports_with_a_fix_for_every_finding()
    {
        ChainSetup setup = Setup() with { Skipped = [("A.B.Gone", "no canonical file (E35)"), ("A.B.Odd", "not damaging (E37)")] };

        Diagnostic[] found = [.. DiagnosticsOf(setup, Stance.Melee).Where(diagnostic => diagnostic.Message.Contains("A.B.", StringComparison.Ordinal))];

        Assert.True(found.Select(diagnostic => diagnostic.RuleId).SequenceEqual(["P-CHAIN-002", "P-CHAIN-002"], StringComparer.Ordinal) && found.All(diagnostic => string.Equals(diagnostic.Severity, "info", StringComparison.Ordinal) && diagnostic.Fix.Length > 0));
        Assert.Contains("A.B.Gone", found[0].Message, StringComparison.Ordinal);
    }
}
