using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;

namespace CohEngine.Mcp.Tests;

public sealed class ErrorTextTests
{
    private const string Slotted = "shield_scrapper_set_bonuses";

    private static string NewDirectory()
    {
        string directory = Path.Join(Path.GetTempPath(), "coh-engine-mcp-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        return directory;
    }

    [Fact]
    public async Task A_missing_dump_file_is_reported_by_file_name_and_the_clients_own_argument()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string empty = NewDirectory();
        try
        {
            string error = await harness.CallErrorAsync("score", ("buildDir", McpHarness.BuildDir(Slotted)), ("midsDir", empty), ("attribModPath", Path.Join(McpHarness.Store.Root, "attribmod", "AttribMod.json")), ("mathsPath", Path.Join(McpHarness.Store.Root, "Maths.mhd"))).ConfigureAwait(true);

            Assert.Equal($"E72: cannot read 'archetypes.json' (from buildDir '{McpHarness.BuildDir(Slotted)}', midsDir '{empty}'); check that the path exists, is readable and holds a Mids dump", error);

            // Nothing but the file name and the client's own arguments: no drive letter or path of the server's own.
            string stripped = error.Replace(McpHarness.BuildDir(Slotted), string.Empty, StringComparison.Ordinal).Replace(empty, string.Empty, StringComparison.Ordinal);
            Assert.DoesNotContain(":\\", stripped, StringComparison.Ordinal);
            Assert.DoesNotContain(":/", stripped, StringComparison.Ordinal);
        }
        finally
        {
            Directory.Delete(empty);
        }
    }

    [Fact]
    public async Task A_dump_file_of_the_wrong_shape_is_a_shape_error_without_the_file_path()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string bad = NewDirectory();
        await File.WriteAllTextAsync(Path.Join(bad, "powers_effects.json"), "{}", TestContext.Current.CancellationToken).ConfigureAwait(true);
        try
        {
            string error = await harness.CallErrorAsync("check_hard_limits", ("buildDir", bad)).ConfigureAwait(true);

            Assert.Equal($"E72: an input from buildDir '{bad}' is missing a key or holds the wrong shape; re-dump the build or check the data matches its schema", error);
        }
        finally
        {
            File.Delete(Path.Join(bad, "powers_effects.json"));
            Directory.Delete(bad);
        }
    }

    [Fact]
    public async Task A_build_directory_at_the_filesystem_root_has_no_default_shared_directory()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string root = Path.GetPathRoot(Path.GetTempPath())!;

        string error = await harness.CallErrorAsync("check_legality", ("buildDir", root)).ConfigureAwait(true);

        Assert.StartsWith($"E164: cannot derive the shared dump directory from '{root}'", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_shared_directory_at_the_filesystem_root_needs_explicit_file_paths()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string root = Path.GetPathRoot(Path.GetTempPath())!;

        string error = await harness.CallErrorAsync("score", ("buildDir", McpHarness.BuildDir(Slotted)), ("midsDir", root)).ConfigureAwait(true);

        Assert.StartsWith($"E164: cannot find the files that sit beside the shared dump directory '{root}'", error, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Explicit_data_file_paths_give_the_same_answer_as_the_default_layout()
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string build = McpHarness.BuildDir(Slotted);
        string attribMod = Path.Join(McpHarness.Store.Root, "attribmod", "AttribMod.json");
        string maths = Path.Join(McpHarness.Store.Root, "Maths.mhd");

        string byDefault = (await harness.CallJsonAsync("score", ("buildDir", build)).ConfigureAwait(true)).ToJsonString();
        string explicitPaths = (await harness.CallJsonAsync("score", ("buildDir", build), ("midsDir", McpHarness.Store.MidsDirectory), ("attribModPath", attribMod), ("mathsPath", maths)).ConfigureAwait(true)).ToJsonString();
        string totals = (await harness.CallJsonAsync("totals", ("buildDir", build), ("attribModPath", attribMod), ("mathsPath", maths)).ConfigureAwait(true)).ToJsonString();
        string set = (await harness.CallJsonAsync("set_bonus_lookup", ("setName", "Thunderstrike"), ("buildDir", build), ("attribModPath", attribMod)).ConfigureAwait(true)).ToJsonString();

        Assert.Equal(byDefault, explicitPaths);
        Assert.Contains("defense_percent", totals, StringComparison.Ordinal);
        Assert.Contains("RangedST", set, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("score", "attribModPath")]
    [InlineData("score", "mathsPath")]
    [InlineData("totals", "attribModPath")]
    [InlineData("set_bonus_lookup", "attribModPath")]
    public async Task A_missing_data_file_path_is_a_named_error(string tool, string parameter)
    {
        await using McpHarness harness = await McpHarness.StartAsync().ConfigureAwait(true);
        string missing = Path.Join(McpHarness.Store.Root, "no-such-file");

        string error = await harness.CallErrorAsync(tool, ("setName", "Thunderstrike"), ("buildDir", McpHarness.BuildDir(Slotted)), (parameter, missing)).ConfigureAwait(true);

        Assert.Equal($"E164: {parameter} '{missing}' does not exist; give an existing file", error);
    }

    [Fact]
    public void An_enum_without_a_member_for_an_index_is_a_data_shape_error()
    {
        EnumMaps enums = new(new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
        {
            ["eDamage"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["None"] = 0 },
        });

        EngineException short_ = Assert.Throws<EngineException>(() => EngineTools.Named([0.0, 1.0], enums, "eDamage", percent: false));
        EngineException absent = Assert.Throws<EngineException>(() => EngineTools.Named([0.0], enums, "eMez", percent: true));

        Assert.Equal("E168: the shared enums.json has no eDamage member at index 1; use the enums.json dumped with the build", short_.Message);
        Assert.StartsWith("E168: the shared enums.json has no eMez member at index 0", absent.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void A_vector_is_named_and_scaled_to_percentages()
    {
        EnumMaps enums = new(new Dictionary<string, IReadOnlyDictionary<string, int>>(StringComparer.Ordinal)
        {
            ["eDamage"] = new Dictionary<string, int>(StringComparer.Ordinal) { ["None"] = 0, ["Fire"] = 1 },
        });

        Dictionary<string, double> named = EngineTools.Named([0.0, 0.125], enums, "eDamage", percent: true);

        Assert.Equal(12.5, named["Fire"], 6);
    }
}
