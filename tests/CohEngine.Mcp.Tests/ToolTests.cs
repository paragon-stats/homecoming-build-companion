using System.Globalization;
using System.Text.Json.Nodes;

using CohEngine.Core;
using CohEngine.Core.Data;
using CohEngine.Core.Maths;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Mcp.Tests;

public sealed class ToolTests
{
    private const string Slotted = "shield_scrapper_set_bonuses";
    private const string Assembled = "Stalker (Fiery Melee - Invulnerability)";

    private static readonly string[] Sections = ["legality", "hard_limits", "enhancement_aspects", "metrics"];

    [Fact]
    public async Task Rules_lists_what_the_cli_lists_and_carries_the_data_version()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        JsonNode result = await harness.CallJsonAsync("rules").ConfigureAwait(true);

        (string Output, int ExitCode) cli = CliCapture.Run("rules");
        string[] listed = Sections
            .SelectMany(section => result[section]!.AsArray().Select(rule => rule!["rule_id"]!.GetValue<string>()))
            .ToArray();
        string[] printed = cli.Output.Split('\n').Where(line => line.StartsWith("  ", StringComparison.Ordinal)).Select(line => line.Trim()).ToArray();
        Assert.Equal(printed, listed, StringComparer.Ordinal);
        Assert.Equal(EngineInfo.Version, result["data_version"]!.GetValue<string>());
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

        (string Output, int ExitCode) cli = CliCapture.Run("validate", buildDir, "--format", "json");
        JsonArray together = [.. hardLimits["diagnostics"]!.AsArray().Concat(legality["diagnostics"]!.AsArray()).Select(item => item!.DeepClone())];
        Assert.True(JsonNode.DeepEquals(JsonNode.Parse(cli.Output), together));
        int[] codes = [hardLimits["exit_code"]!.GetValue<int>(), legality["exit_code"]!.GetValue<int>()];
        Assert.Equal(cli.ExitCode, codes.Contains(1) ? 1 : codes.Contains(2) ? 2 : 0);
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

    [Fact]
    public async Task Power_lookup_returns_game_levels()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        BuildFixture fixture = McpHarness.Store.Build(Slotted);
        Power power = fixture.Powers.First(candidate => candidate.PickLevel > 0);

        JsonNode result = await harness.CallJsonAsync("power_lookup", ("fullName", power.FullName.ToUpperInvariant()), ("buildDir", fixture.Directory)).ConfigureAwait(true);

        JsonNode found = result["power"]!;
        Assert.Equal(power.FullName, found["full_name"]!.GetValue<string>());
        Assert.Equal(power.Level + 1, found["available_level"]!.GetValue<int>());
        Assert.Equal(power.PickLevel + 1, found["pick_level"]!.GetValue<int>());
        Assert.Equal(power.StatInclude, found["stat_include"]!.GetValue<bool>());
        Assert.Equal(power.Effects.Select(effect => effect.EffectType).Distinct(StringComparer.Ordinal), found["effect_types"]!.AsArray().Select(item => item!.GetValue<string>()), StringComparer.Ordinal);
    }

    [Fact]
    public async Task Chain_reports_it_is_not_available_in_this_build()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);

        string error = await harness.CallErrorAsync("chain").ConfigureAwait(true);

        Assert.Equal("E165: the chain tool is not available in this build; use score or totals for the build's numbers instead", error);
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
    public async Task An_unknown_set_or_power_is_a_named_error()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string buildDir = McpHarness.BuildDir(Slotted);

        string set = await harness.CallErrorAsync("set_bonus_lookup", ("setName", "No Such Set"), ("buildDir", buildDir)).ConfigureAwait(true);
        string power = await harness.CallErrorAsync("power_lookup", ("fullName", "No.Such.Power"), ("buildDir", buildDir)).ConfigureAwait(true);

        Assert.StartsWith("E166: no set named 'No Such Set'", set, StringComparison.Ordinal);
        Assert.StartsWith("E167: no power named 'No.Such.Power'", power, StringComparison.Ordinal);
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

            Assert.StartsWith("E72: cannot read an input", error, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(empty);
        }
    }
}
