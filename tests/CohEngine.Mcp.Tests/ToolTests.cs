using System.Globalization;
using System.Text.Json.Nodes;

using CohEngine.Core.BuildFile;
using CohEngine.Core.Chain;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace CohEngine.Mcp.Tests;

public sealed class ToolTests
{
    private const string Slotted = "shield_scrapper_set_bonuses";
    private const string Assembled = "community__stalker__general__unknown-fiery-invulnerability";
    private const string Keywords = "keywords_endcost_owner_arch";
    private const string Mastermind = "community__mastermind__general__unknown-petless";
    private const string Bane = "reference/arachnos-soldier/bane-mace/bane-leviathan.mbd";

    private static readonly string[] Sections = ["legality", "hard_limits", "enhancement_aspects", "metrics"];

    [Fact]
    public async Task Rules_lists_what_the_cli_lists_and_carries_the_data_version()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("rules").ConfigureAwait(true);

        (string output, _) = CliCapture.Run("rules");
        string[] listed = [.. Sections.SelectMany(section => result[section]!.AsArray().Select(rule => rule!["rule_id"]!.GetValue<string>()))];
        string[] printed = [.. output.Split('\n').Where(line => line.StartsWith("  ", StringComparison.Ordinal)).Select(line => line.Trim())];
        Assert.Equal(printed, listed, StringComparer.Ordinal);
    }

    [Fact]
    public async Task Check_slotting_reports_the_widow_build_with_a_data_version()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir("community__arachnos-widow__general__unknown-melee");

        JsonNode result = await harness.CallJsonAsync("check_slotting", ("buildDir", buildDir)).ConfigureAwait(true);

        string[] ruleIds = [.. result["diagnostics"]!.AsArray().Select(item => item!["rule_id"]!.GetValue<string>())];
        Assert.Contains("P-ED-002", ruleIds, StringComparer.Ordinal);
        Assert.Contains("P-POWER-001", ruleIds, StringComparer.Ordinal);
        Assert.DoesNotContain("P-SET-001", ruleIds, StringComparer.Ordinal);
        Assert.Equal(2, result["exit_code"]!.GetValue<int>());
        Assert.Matches("^sha256:[0-9a-f]{64}$", result["data_version"]!.GetValue<string>());
        Assert.NotEqual(
            await VersionAsync("check_legality", ("buildDir", buildDir)).ConfigureAwait(true),
            result["data_version"]!.GetValue<string>(),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Check_slotting_reads_a_mbd_with_the_shared_directory()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string mbd = BuildFileFixtures.Sample("community/arachnos-widow/general/unknown-melee.mbd");

        JsonNode result = await harness.CallJsonAsync("check_slotting", ("buildDir", mbd), ("midsDir", McpHarness.Store.MidsDirectory)).ConfigureAwait(true);

        Assert.Contains(result["diagnostics"]!.AsArray(), item => string.Equals(item!["rule_id"]!.GetValue<string>(), "P-POWER-001", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Every_tool_result_carries_a_data_version_and_rules_says_unknown()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(Slotted);
        (string Name, (string, object)[] Arguments)[] calls =
        [
            ("check_legality", [("buildDir", buildDir)]),
            ("check_hard_limits", [("buildDir", buildDir)]),
            ("check_slotting", [("buildDir", buildDir)]),
            ("score", [("buildDir", buildDir)]),
            ("totals", [("buildDir", buildDir)]),
            ("set_bonus_lookup", [("setName", "Thunderstrike"), ("buildDir", buildDir)]),
            ("set_bonus_lookup", [("setName", "No Such Set"), ("buildDir", buildDir)]),
            ("power_lookup", [("fullName", "No.Such.Power"), ("buildDir", buildDir)]),
            ("chain", [("buildDir", buildDir), ("options", new JsonObject { ["stance"] = "melee" })]),
        ];

        JsonNode rules = await harness.CallJsonAsync("rules").ConfigureAwait(true);
        Assert.Equal("unknown", rules["data_version"]!.GetValue<string>());
        foreach ((string name, (string, object)[] arguments) in calls)
        {
            JsonNode result = await harness.CallJsonAsync(name, arguments).ConfigureAwait(true);

            Assert.Matches("^sha256:[0-9a-f]{64}$", result["data_version"]!.GetValue<string>());
        }
    }

    [Fact]
    public async Task Tools_that_read_different_files_give_different_data_versions_and_the_same_files_the_same_across_servers()
    {
        string buildDir = McpHarness.BuildDir(Slotted);

        string totals = await VersionAsync("totals", ("buildDir", buildDir)).ConfigureAwait(true);
        string totalsAgain = await VersionAsync("totals", ("buildDir", buildDir)).ConfigureAwait(true);
        string power = await VersionAsync("power_lookup", ("fullName", "No.Such.Power"), ("buildDir", buildDir)).ConfigureAwait(true);
        string legality = await VersionAsync("check_legality", ("buildDir", buildDir)).ConfigureAwait(true);

        Assert.Equal(totals, totalsAgain, StringComparer.Ordinal);
        Assert.Equal(3, new[] { totals, power, legality }.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task One_changed_byte_in_a_build_dump_file_changes_the_data_version()
    {
        using ScratchDirectory scratch = new();
        string copy = scratch.CopyOf(McpHarness.BuildDir(Slotted), "build");
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        string before = (await harness.CallJsonAsync("power_lookup", ("fullName", "No.Such.Power"), ("buildDir", copy)).ConfigureAwait(true))["data_version"]!.GetValue<string>();
        string same = (await harness.CallJsonAsync("power_lookup", ("fullName", "No.Such.Power"), ("buildDir", scratch.CopyOf(McpHarness.BuildDir(Slotted), "build-again"))).ConfigureAwait(true))["data_version"]!.GetValue<string>();
        ScratchDirectory.ChangeOneByte(Path.Join(copy, "slots.json"));
        string after = (await harness.CallJsonAsync("power_lookup", ("fullName", "No.Such.Power"), ("buildDir", copy)).ConfigureAwait(true))["data_version"]!.GetValue<string>();

        Assert.Equal(before, same, StringComparer.Ordinal);
        Assert.NotEqual(before, after, StringComparer.Ordinal);
    }

    [Fact]
    public async Task One_changed_byte_in_a_shared_dump_file_changes_the_data_version()
    {
        using ScratchDirectory scratch = new();
        string mids = scratch.CopyOf(McpHarness.Store.MidsDirectory, "mids");
        string build = scratch.CopyOf(McpHarness.BuildDir(Slotted), Path.Join("mids", "builds", "b"));
        string changedMids = scratch.CopyOf(mids, "mids-changed");
        ScratchDirectory.ChangeOneByte(Path.Join(changedMids, "levels.json"));
        string changedBuild = scratch.CopyOf(McpHarness.BuildDir(Slotted), Path.Join("mids-changed", "builds", "b"));
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        string before = (await harness.CallJsonAsync("check_hard_limits", ("buildDir", build)).ConfigureAwait(true))["data_version"]!.GetValue<string>();
        string after = (await harness.CallJsonAsync("check_hard_limits", ("buildDir", changedBuild)).ConfigureAwait(true))["data_version"]!.GetValue<string>();

        Assert.NotEqual(before, after, StringComparer.Ordinal);
    }

    [Theory]
    [InlineData(Assembled)]
    [InlineData(Slotted)]
    public async Task Both_check_tools_together_give_the_diagnostics_validate_prints(string build)
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(build);

        JsonNode hardLimits = await harness.CallJsonAsync("check_hard_limits", ("buildDir", buildDir)).ConfigureAwait(true);
        JsonNode legality = await harness.CallJsonAsync("check_legality", ("buildDir", buildDir)).ConfigureAwait(true);

        (string output, int exitCode) = CliCapture.Run("validate", buildDir, "--format", "json");
        JsonArray together = [.. hardLimits["diagnostics"]!.AsArray().Concat(legality["diagnostics"]!.AsArray()).Select(item => item!.DeepClone())];
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(output), together));
        int[] codes = [hardLimits["exit_code"]!.GetValue<int>(), legality["exit_code"]!.GetValue<int>()];

        // An error (1) outranks a warning (2), which outranks a clean run (0).
        int expected = 0;
        if (codes.Contains(2))
        {
            expected = 2;
        }

        if (codes.Contains(1))
        {
            expected = 1;
        }

        Assert.Equal(exitCode, expected);
    }

    [Fact]
    public async Task Score_gives_every_registered_metric()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("score", ("buildDir", McpHarness.BuildDir(Slotted))).ConfigureAwait(true);

        Assert.Equal("Class_Scrapper", result["class_name"]!.GetValue<string>());
        Assert.Equal(
            RuleRegistry.Metrics.Select(metric => metric.Id),
            result["metrics"]!.AsArray().Select(metric => metric!["name"]!.GetValue<string>()),
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task Score_gives_metrics_that_validate_expect_accepts()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(Slotted);

        JsonNode result = await harness.CallJsonAsync("score", ("buildDir", buildDir)).ConfigureAwait(true);

        foreach (JsonNode? metric in result["metrics"]!.AsArray())
        {
            string claim = metric!["flag"] is { } flag ? flag.GetValue<bool>().ToString().ToLowerInvariant() : Math.Round(metric["number"]!.GetValue<double>(), 2, MidpointRounding.ToEven).ToString("F2", CultureInfo.InvariantCulture);
            (string output, _) = CliCapture.Run("validate", buildDir, "--expect", metric["name"]!.GetValue<string>() + "=" + claim);
            Assert.DoesNotContain("P-EXPECT-001", output, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Score_runs_the_assembly_path_for_an_assembly_era_build()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("score", ("buildDir", McpHarness.BuildDir(Assembled))).ConfigureAwait(true);

        Assert.Equal(4, result["metrics"]!.AsArray().Count);
        Assert.False(result["metrics"]!.AsArray().Single(metric => string.Equals(metric!["name"]!.GetValue<string>(), "perma_dom", StringComparison.Ordinal))!["flag"]!.GetValue<bool>());
    }

    [Fact]
    public async Task Totals_match_the_mids_reference_dump()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        BuildFixture fixture = McpHarness.Store.Build(Slotted);

        JsonNode result = await harness.CallJsonAsync("totals", ("buildDir", fixture.Directory)).ConfigureAwait(true);

        IReadOnlyDictionary<string, int> damage = McpHarness.Store.Enums.Maps["eDamage"];
        foreach (string aspect in new[] { "Melee", "Ranged", "AoE", "Smashing" })
        {
            double want = F32.Round(fixture.Totals.Totals.Def[damage[aspect]] * 100.0);
            Assert.Equal(want, result["totals"]!["defense_percent"]![aspect]!.GetValue<double>(), 3);
        }

        Assert.Equal(fixture.Totals.Totals.HpMax, result["totals"]!["scalars"]!["HPMax"]!.GetValue<double>(), 3);
        Assert.Equal(fixture.Totals.TotalsCapped.EndRec, result["totals_capped"]!["scalars"]!["EndRec"]!.GetValue<double>(), 3);
        Assert.Equal(F32.Round(fixture.Totals.Totals.Res[damage["Smashing"]] * 100.0), result["totals"]!["resistance_percent"]!["Smashing"]!.GetValue<double>(), 3);
    }

    [Fact]
    public async Task Set_bonus_lookup_resolves_the_zephyr_defense_to_a_percentage()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("set_bonus_lookup", ("setName", "blessing_of_the_zephyr"), ("buildDir", McpHarness.BuildDir(Slotted))).ConfigureAwait(true);

        JsonNode set = result["set"]!;
        Assert.Equal("Blessing_of_the_Zephyr", set["set"]!.GetValue<string>());
        JsonNode ranged = set["tiers"]![0]!["effects"]!.AsArray().Single(effect => string.Equals(effect!["aspect"]!.GetValue<string>(), "Ranged", StringComparison.Ordinal))!;
        Assert.Equal(2, set["tiers"]![0]!["pieces"]!.GetValue<int>());
        Assert.Equal(1.25, ranged["percent"]!.GetValue<double>(), 6);
        JsonNode knockback = set["special"]!.AsArray().SelectMany(tier => tier!["effects"]!.AsArray()).Single(effect => string.Equals(effect!["aspect"]!.GetValue<string>(), "Knockback", StringComparison.Ordinal))!;
        Assert.Null(knockback["percent"]);
    }

    [Fact]
    public async Task Set_bonus_lookup_accepts_an_explicit_shared_directory()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("set_bonus_lookup", ("setName", "Thunderstrike"), ("buildDir", McpHarness.BuildDir(Slotted)), ("midsDir", McpHarness.Store.MidsDirectory)).ConfigureAwait(true);

        Assert.Equal("RangedST", result["set"]!["set_type"]!.GetValue<string>());
        Assert.Equal(2.0, result["set"]!["tiers"]![0]!["effects"]![0]!["percent"]!.GetValue<double>(), 6);
    }

    [Theory]
    [InlineData(Keywords, "Blaster_Ranged.Electrical_Blast.Lightning_Bolt", 1, 1)]
    [InlineData(Keywords, "Pool.Sorcery.Arcane_Bolt", 4, 4)]
    [InlineData(Keywords, "Pool.Speed.Hasten", 4, 8)]
    [InlineData(Mastermind, "Epic.Mastermind_Mace_Mastery.Web_Envelope", 35, 38)]
    public async Task Power_lookup_gives_the_game_levels_as_literals(string build, string fullName, int available, int picked)
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("power_lookup", ("fullName", fullName), ("buildDir", McpHarness.BuildDir(build))).ConfigureAwait(true);

        Assert.Equal(available, result["power"]!["available_level"]!.GetValue<int>());
        Assert.Equal(picked, result["power"]!["pick_level"]!.GetValue<int>());
    }

    [Fact]
    public async Task A_field_an_older_dump_leaves_out_is_unknown_while_a_genuine_zero_stays_a_number()
    {
        using ScratchDirectory scratch = new();
        string older = scratch.CopyOf(McpHarness.BuildDir("shield_scrapper_slotted"), "older");
        string file = Path.Join(older, "powers_effects.json");
        JsonArray records = JsonNode.Parse(await File.ReadAllTextAsync(file, TestContext.Current.CancellationToken).ConfigureAwait(true))!.AsArray();
        foreach (JsonObject record in records.Select(node => node!.AsObject()))
        {
            foreach (string key in record.Select(field => field.Key).Where(key => key.StartsWith("Requires", StringComparison.Ordinal) || string.Equals(key, "Chosen", StringComparison.Ordinal)).ToList())
            {
                record.Remove(key);
            }
        }

        await File.WriteAllTextAsync(file, records.ToJsonString(), TestContext.Current.CancellationToken).ConfigureAwait(true);
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("power_lookup", ("fullName", "Scrapper_Melee.Dark_Melee.Smite"), ("buildDir", older)).ConfigureAwait(true);

        JsonObject power = result["power"]!.AsObject();
        Assert.False(power.ContainsKey("requires_powers"));
        Assert.False(power.ContainsKey("chosen"));
        Assert.Equal(["requires_powers", "chosen"], result["unknown_fields"]!.AsArray().Select(name => name!.GetValue<string>()), StringComparer.Ordinal);
        Assert.True(power.TryGetPropertyValue("activate_period", out JsonNode? period));
        Assert.Equal(0.0, period!.GetValue<double>());
    }

    [Fact]
    public async Task A_field_the_dump_holds_as_empty_is_a_value_not_unknown()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("power_lookup", ("fullName", "Blaster_Ranged.Electrical_Blast.Lightning_Bolt"), ("buildDir", McpHarness.BuildDir(Keywords))).ConfigureAwait(true);

        Assert.Empty(result["power"]!["requires_powers"]!.AsArray());
        Assert.True(result["power"]!["chosen"]!.GetValue<bool>());
        Assert.False(result.AsObject().ContainsKey("unknown_fields"));
    }

    [Fact]
    public async Task Power_lookup_returns_game_levels()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        BuildFixture fixture = McpHarness.Store.Build(Slotted);
        Power power = fixture.Powers.First(candidate => candidate.PickLevel > 0);

        JsonNode result = await harness.CallJsonAsync("power_lookup", ("fullName", power.FullName.ToUpperInvariant()), ("buildDir", fixture.Directory)).ConfigureAwait(true);

        JsonNode found = result["power"]!;
        Assert.Equal(power.FullName, found["full_name"]!.GetValue<string>());
        Assert.Equal(power.Level, found["available_level"]!.GetValue<int>());
        Assert.Equal(power.PickLevel + 1, found["pick_level"]!.GetValue<int>());
        Assert.Equal(power.StatInclude, found["stat_include"]!.GetValue<bool>());
        Assert.Equal(power.Effects.Select(effect => effect.EffectType).Distinct(StringComparer.Ordinal), found["effect_types"]!.AsArray().Select(item => item!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Totals_give_the_ranged_defense_of_the_reference_dump_as_a_percentage()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("totals", ("buildDir", McpHarness.BuildDir(Slotted))).ConfigureAwait(true);

        Assert.Equal(34.959, result["totals"]!["defense_percent"]!["Ranged"]!.GetValue<double>(), 3);
    }

    [Fact]
    public async Task The_chain_tool_publishes_its_planning_options_as_one_object()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        IList<McpClientTool> tools = await harness.Client.ListToolsAsync(cancellationToken: TestContext.Current.CancellationToken).ConfigureAwait(true);
        JsonNode published = JsonNode.Parse(tools.Single(tool => string.Equals(tool.Name, "chain", StringComparison.Ordinal)).JsonSchema.GetRawText())!;
        JsonNode expected = JsonNode.Parse(await File.ReadAllTextAsync(Path.Join(McpHarness.Store.RepositoryRoot, "tests", "fixtures", "mcp", "chain-input-schema.json"), TestContext.Current.CancellationToken).ConfigureAwait(true))!;

        // The whole schema: a member made required, a type, nullability, a default or a description changed fails it.
        Assert.True(JsonNode.DeepEquals(expected, published), published.ToJsonString());
    }

    [Fact]
    public async Task Chain_with_no_options_plans_as_the_cli_does_by_default()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(Slotted);

        JsonNode result = await harness.CallJsonAsync("chain", ("buildDir", buildDir)).ConfigureAwait(true);

        AssertSamePlan(result, [buildDir, "--stance", "all", "--target", "single", "--move", "run", "--format", "json"]);
    }

    [Theory]
    [InlineData("stance")]
    [InlineData("form")]
    [InlineData("target")]
    [InlineData("move")]
    [InlineData("standoff")]
    public async Task A_planning_option_sent_as_null_keeps_its_default(string member)
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(Slotted);

        JsonNode result = await harness.CallJsonAsync("chain", ("buildDir", buildDir), ("options", new JsonObject { [member] = null })).ConfigureAwait(true);

        AssertSamePlan(result, [buildDir, "--stance", "all", "--target", "single", "--move", "run", "--format", "json"]);
    }

    [Theory]
    [InlineData("Stance")]
    [InlineData("stanse")]
    public async Task A_miscased_or_unknown_planning_option_is_a_tool_error(string member)
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        CallToolResult result = await harness.CallAsync("chain", ("buildDir", McpHarness.BuildDir(Slotted)), ("options", new JsonObject { [member] = "melee" })).ConfigureAwait(true);

        Assert.True(result.IsError);
    }

    [Fact]
    public async Task Chain_with_some_options_keeps_the_defaults_for_the_rest()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(Slotted);

        JsonNode result = await harness.CallJsonAsync("chain", ("buildDir", buildDir), ("options", new JsonObject { ["stance"] = "melee", ["move"] = "jump" })).ConfigureAwait(true);

        AssertSamePlan(result, [buildDir, "--stance", "melee", "--target", "single", "--move", "jump", "--format", "json"]);
    }

    [Fact]
    public async Task Chain_gives_the_plan_the_cli_prints()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(Slotted);
        JsonObject options = new() { ["stance"] = "ranged", ["form"] = string.Empty, ["target"] = "spawn:3", ["move"] = "jump", ["standoff"] = 30.5 };

        JsonNode result = await harness.CallJsonAsync("chain", ("buildDir", buildDir), ("options", options)).ConfigureAwait(true);

        AssertSamePlan(result, [buildDir, "--stance", "ranged", "--target", "spawn:3", "--move", "jump", "--format", "json", "--form", string.Empty, "--standoff", "30.5"]);
    }

    [Fact]
    public async Task Chain_takes_the_optional_form_shared_directory_and_pet_data()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync(
            "chain",
            ("buildDir", McpHarness.BuildDir(Slotted)),
            ("midsDir", McpHarness.Store.MidsDirectory),
            ("options", new JsonObject { ["stance"] = "melee", ["form"] = string.Empty }),
            ("midsDb", Path.Join(McpHarness.Store.Root, "attribmod"))).ConfigureAwait(true);

        Assert.Equal("melee", result["stances"]![0]!["stance"]!.GetValue<string>());
    }

    [Fact]
    public async Task Chain_reports_a_bad_option_as_the_cli_does()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        string error = await harness.CallErrorAsync("chain", ("buildDir", McpHarness.BuildDir(Slotted)), ("options", new JsonObject { ["stance"] = "side" })).ConfigureAwait(true);

        Assert.Equal("argument --stance: invalid choice: 'side'", error);
    }

    [Fact]
    public async Task Chain_reports_an_unreadable_input_as_an_engine_error()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        string error = await harness.CallErrorAsync("chain", ("buildDir", McpHarness.BuildDir(Slotted)), ("midsDir", McpHarness.Store.Root)).ConfigureAwait(true);

        Assert.StartsWith("E65: archetype table ", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_mbd_file_is_checked_for_legality_and_hard_limits_like_validate_does()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string mbd = BuildFileFixtures.Sample(Bane);
        string mids = McpHarness.Store.MidsDirectory;

        JsonNode hardLimits = await harness.CallJsonAsync("check_hard_limits", ("buildDir", mbd), ("midsDir", mids)).ConfigureAwait(true);
        JsonNode legality = await harness.CallJsonAsync("check_legality", ("buildDir", mbd), ("midsDir", mids)).ConfigureAwait(true);

        (string output, _) = CliCapture.Run(new MbdBuildSource(mids), "validate", mbd, "--format", "json");
        JsonArray together = [.. hardLimits["diagnostics"]!.AsArray().Concat(legality["diagnostics"]!.AsArray()).Select(item => item!.DeepClone())];
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(output), together));
    }

    [Fact]
    public async Task Check_hard_limits_reports_a_mbd_whose_level_1_picks_are_swapped()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string scratch = Directory.CreateTempSubdirectory("coh-mcp-swapped").FullName;
        try
        {
            string swapped = BuildFileFixtures.SwappedLevelOneCopy(Bane, scratch);

            JsonNode result = await harness.CallJsonAsync("check_hard_limits", ("buildDir", swapped), ("midsDir", McpHarness.Store.MidsDirectory)).ConfigureAwait(true);

            JsonNode found = Assert.Single(result["diagnostics"]!.AsArray(), item => string.Equals(item!["rule_id"]!.GetValue<string>(), "H-POWER-006", StringComparison.Ordinal))!;
            Assert.Equal(1, result["exit_code"]!.GetValue<int>());
            Assert.StartsWith("swap 'Training_Gadgets.", found["fix"]!.GetValue<string>(), StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(scratch, recursive: true);
        }
    }

    [Fact]
    public async Task A_mbd_file_needs_the_shared_directory_named_and_existing()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string mbd = BuildFileFixtures.Sample(Bane);

        string unnamed = await harness.CallErrorAsync("check_legality", ("buildDir", mbd)).ConfigureAwait(true);
        string missingShared = await harness.CallErrorAsync("check_legality", ("buildDir", mbd), ("midsDir", Path.Join(McpHarness.Store.Root, "no-such-directory"))).ConfigureAwait(true);
        string missingFile = await harness.CallErrorAsync("check_hard_limits", ("buildDir", Path.Join(McpHarness.Store.Root, "no-such.mbd")), ("midsDir", McpHarness.Store.MidsDirectory)).ConfigureAwait(true);

        Assert.Equal("E164: midsDir is required with a .mbd file; give the shared dump directory", unnamed);
        Assert.StartsWith("E164: midsDir '", missingShared, StringComparison.Ordinal);
        Assert.StartsWith("E164: buildDir '", missingFile, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("score", "")]
    [InlineData("totals", "")]
    [InlineData("chain", "")]
    [InlineData("set_bonus_lookup", "setName")]
    [InlineData("power_lookup", "fullName")]
    public async Task A_tool_that_computes_from_a_dump_refuses_a_mbd_file_with_e180(string tool, string extra)
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        (string, object)[] arguments = extra.Length == 0 ? [("buildDir", BuildFileFixtures.Sample(Bane))] : [(extra, "x"), ("buildDir", BuildFileFixtures.Sample(Bane))];

        string error = await harness.CallErrorAsync(tool, arguments).ConfigureAwait(true);

        Assert.StartsWith("E180: buildDir '", error, StringComparison.Ordinal);
        Assert.EndsWith("is a .mbd file, which holds no power effects; give the build's Mids dump directory for this tool", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("check_legality")]
    [InlineData("check_hard_limits")]
    [InlineData("score")]
    [InlineData("totals")]
    public async Task A_missing_build_directory_is_a_named_error(string tool)
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string missing = Path.Join(McpHarness.Store.Root, "no-such-build");

        string error = await harness.CallErrorAsync(tool, ("buildDir", missing)).ConfigureAwait(true);

        Assert.StartsWith("E164: buildDir '", error, StringComparison.Ordinal);
        Assert.Contains("does not exist", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_missing_path_is_a_named_error_for_the_lookups_and_the_shared_directory()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string missing = Path.Join(McpHarness.Store.Root, "no-such-directory");

        string power = await harness.CallErrorAsync("power_lookup", ("fullName", "A.B.C"), ("buildDir", missing)).ConfigureAwait(true);
        string set = await harness.CallErrorAsync("set_bonus_lookup", ("setName", "Thunderstrike"), ("buildDir", missing)).ConfigureAwait(true);
        string shared = await harness.CallErrorAsync("check_legality", ("buildDir", McpHarness.BuildDir(Slotted)), ("midsDir", missing)).ConfigureAwait(true);

        Assert.StartsWith("E164: buildDir", power, StringComparison.Ordinal);
        Assert.StartsWith("E164: buildDir", set, StringComparison.Ordinal);
        Assert.StartsWith("E164: midsDir", shared, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_unknown_set_or_power_is_a_structured_unknown_result_not_an_error_or_an_empty_one()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(Slotted);

        JsonNode set = await harness.CallJsonAsync("set_bonus_lookup", ("setName", "No Such Set"), ("buildDir", buildDir)).ConfigureAwait(true);
        JsonNode power = await harness.CallJsonAsync("power_lookup", ("fullName", "No.Such.Power"), ("buildDir", buildDir)).ConfigureAwait(true);

        Assert.StartsWith("E166: no set named 'No Such Set'", set["unknown"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(set.AsObject().ContainsKey("set"));
        Assert.StartsWith("E167: no power named 'No.Such.Power'", power["unknown"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.False(power.AsObject().ContainsKey("power"));
    }

    [Fact]
    public async Task A_known_set_or_power_has_no_unknown_field()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        BuildFixture fixture = McpHarness.Store.Build(Slotted);

        JsonNode set = await harness.CallJsonAsync("set_bonus_lookup", ("setName", "Thunderstrike"), ("buildDir", fixture.Directory)).ConfigureAwait(true);
        JsonNode power = await harness.CallJsonAsync("power_lookup", ("fullName", fixture.Powers[0].FullName), ("buildDir", fixture.Directory)).ConfigureAwait(true);

        Assert.False(set.AsObject().ContainsKey("unknown"));
        Assert.False(power.AsObject().ContainsKey("unknown"));
    }

    [Fact]
    public async Task A_directory_without_the_dump_files_is_an_unreadable_input_error()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string empty = Path.Join(Path.GetTempPath(), "coh-engine-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            string error = await harness.CallErrorAsync("check_hard_limits", ("buildDir", empty)).ConfigureAwait(true);

            Assert.StartsWith("E72: cannot read 'powers_effects.json' (from buildDir '", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(empty);
        }
    }

    // The tool must give the plan, exit code included, that the CLI prints for the same values.
    private static void AssertSamePlan(JsonNode result, string[] cliArguments)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int code = ChainCommand.Run(cliArguments, output, error);
        Assert.Equal(string.Empty, error.ToString());
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(output.ToString()), result["stances"]));
        Assert.Equal(code, result["exit_code"]!.GetValue<int>());
    }

    private static async Task<string> VersionAsync(string tool, params (string Name, object Value)[] arguments)
    {
        McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(false);
        try
        {
            return (await harness.CallJsonAsync(tool, arguments).ConfigureAwait(false))["data_version"]!.GetValue<string>();
        }
        finally
        {
            await harness.DisposeAsync().ConfigureAwait(false);
        }
    }
}
