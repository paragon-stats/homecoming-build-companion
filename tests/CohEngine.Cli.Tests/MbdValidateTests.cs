using System.Text.Json;
using System.Text.Json.Nodes;

using CohEngine.Core;
using CohEngine.Core.BuildFile;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

/// <summary><c>validate</c> on a bare <c>.mbd</c> through <see cref="MbdBuildSource"/>, the Python engine's <c>run_validate_mbd</c>.</summary>
public sealed class MbdValidateTests : IDisposable
{
    private static readonly CliCapture Capture = new(FixtureStore.Locate(AppContext.BaseDirectory));
    private static readonly string Bane = CliCapture.MbdPath("Laser - ARACHNOS - Arachnos Soldier (Bane Spider Soldier - Bane Spider Training)");
    private static readonly string Tanker = Path.Combine(Capture.Store.MidsDirectory, "builds", "wpss_tanker_leadership");
    private static readonly MbdBuildSource Source = new(Capture.Store.MidsDirectory);

    private readonly string scratch = Directory.CreateTempSubdirectory("coh-mbd-validate").FullName;

    public static TheoryData<string> Builds()
    {
        return [.. Capture.Names("validate-mbd")];
    }

    public static TheoryData<string> CuratedSamples()
    {
        return [.. BuildFileFixtures.SampleMbd()
            .Where(path => path.Contains("operator-builds", StringComparison.Ordinal) || path.Contains("Mids Archives", StringComparison.Ordinal))];
    }

    public void Dispose()
    {
        Directory.Delete(scratch, recursive: true);
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Validate_text_matches_the_python_output(string name)
    {
        Assert.Equal((Capture.Output("validate-mbd", name, "txt"), Capture.ExitCode("validate-mbd", name)), CliCapture.Run(Source, "validate", CliCapture.MbdPath(name), "--format", "text"));
    }

    [Theory]
    [MemberData(nameof(Builds))]
    public void Validate_json_matches_the_python_output(string name)
    {
        Assert.Equal((Capture.Output("validate-mbd", name, "json"), Capture.ExitCode("validate-mbd", name)), CliCapture.Run(Source, "validate", CliCapture.MbdPath(name), "--format", "json"));
    }

    [Fact]
    public void Every_capture_has_a_build_file()
    {
        Assert.All(Capture.Names("validate-mbd"), name => Assert.True(File.Exists(CliCapture.MbdPath(name))));
    }

    [Fact]
    public void A_bare_mbd_validates_without_a_harness_dump()
    {
        (string json, int jsonCode) = CliCapture.Run(Source, "validate", Bane, "--format", "json");
        (_, int textCode) = CliCapture.Run(Source, "validate", Bane);

        Assert.Equal((true, true, true), (jsonCode is 0 or 2, textCode is 0 or 2, Rules(json).All(rule => !string.Equals(rule.Severity, "error", StringComparison.Ordinal))));
    }

    [Fact]
    public void Seven_powersets_are_reported()
    {
        string path = Mutated(document => document["PowerSets"]!.AsArray().RemoveAt(7));

        Assert.Contains("H-FMT-001", Ids(CliCapture.Run(Source, "validate", path, "--format", "json").Output));
    }

    [Fact]
    public void An_io_level_above_its_set_is_reported()
    {
        MbdDb database = BuildFileFixtures.Database;
        string path = Mutated(document =>
        {
            JsonObject enhancement = document["PowerEntries"]!.AsArray()
                .SelectMany(entry => entry!["SlotEntries"]!.AsArray())
                .Select(slot => slot!["Enhancement"] as JsonObject)
                .First(candidate => candidate is not null && Mbd.IoLevelBounds(candidate["Uid"]!.GetValue<string>(), database) is { Max: < 49 })!;
            enhancement["IoLevel"] = 50;
        });

        Assert.Contains("H-FMT-002", Ids(CliCapture.Run(Source, "validate", path, "--format", "json").Output));
    }

    [Theory]
    [InlineData("\"50\"")]
    [InlineData("49")]
    public void A_one_based_level_is_reported(string level)
    {
        string path = Mutated(document => document["Level"] = JsonNode.Parse(level));

        Assert.Contains("H-FMT-003", Ids(CliCapture.Run(Source, "validate", path, "--format", "json").Output));
    }

    [Fact]
    public void A_power_the_database_lacks_is_e77()
    {
        string path = Mutated(document => document["PowerEntries"]![0]!["PowerName"] = "Nope.Nope.Nope");

        (int code, string error) = RunError(path);

        Assert.Equal((1, true), (code, error.StartsWith("error: E77: ", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_enhancement_the_database_lacks_is_e78()
    {
        (int code, string error) = RunError(CliCapture.MbdPath("arsen-arsen_-_Dominator_Arsenal_Control_-_Arsenal_Assault"));

        Assert.Equal((1, true), (code, error.StartsWith("error: E78: Dominator_Control.Arsenal_Control.Cryo_Freeze_Ray slot at level 1 holds Gladiator's Javelin: Chance of Damage(Toxic), which enhancements.json lacks;", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_unreadable_mbd_is_e118()
    {
        string path = Path.Combine(scratch, "broken.mbd");
        File.WriteAllText(path, "{ not json");

        (int code, string error) = RunError(path);

        Assert.Equal((1, true), (code, error.StartsWith("error: E118: ", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_default_mids_directory_is_the_repositorys_dump()
    {
        string nowhere = Directory.CreateDirectory(Path.Combine(scratch, "empty")).FullName;

        Assert.Equal(
            (Capture.Store.MidsDirectory, Path.Join(nowhere, "tests", "fixtures", "mids")),
            (MbdBuildSource.LocateDefaultMidsDirectory(AppContext.BaseDirectory), MbdBuildSource.LocateDefaultMidsDirectory(nowhere)));
    }

    [Fact]
    public void The_default_mids_directory_is_used_without_the_flag()
    {
        MbdBuildSource source = new(MbdBuildSource.LocateDefaultMidsDirectory(AppContext.BaseDirectory));

        Assert.Equal(Source.DefaultMidsDirectory, source.DefaultMidsDirectory);
        Assert.Equal(CliCapture.Run(Source, "validate", Bane), CliCapture.Run(source, "validate", Bane));
    }

    [Fact]
    public void The_mids_directory_flag_overrides_the_default()
    {
        MbdBuildSource source = new(Path.Combine(scratch, "not-there"));

        Assert.Equal(CliCapture.Run(Source, "validate", Bane), CliCapture.Run(source, "validate", Bane, "--mids-dir", Capture.Store.MidsDirectory));
    }

    [Fact]
    public void The_locator_refuses_a_null_directory()
    {
        Assert.Throws<ArgumentNullException>(() => MbdBuildSource.LocateDefaultMidsDirectory(null!));
    }

    [Theory]
    [MemberData(nameof(CuratedSamples))]
    public void A_curated_sample_holds_no_error_finding(string path)
    {
        IReadOnlyList<Diagnostic> diagnostics = ValidateCommand.RunMbd(Source, path, Capture.Store.MidsDirectory);

        Assert.DoesNotContain(diagnostics, diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.Ordinal));
    }

    [Fact]
    public void The_loaded_tanker_build_validates_without_errors()
    {
        IReadOnlyList<Diagnostic> diagnostics = ValidateCommand.RunDump(Tanker, Capture.Store.MidsDirectory);

        Assert.DoesNotContain(diagnostics, diagnostic => string.Equals(diagnostic.Severity, "error", StringComparison.Ordinal));
    }

    private static List<(string Id, string Severity)> Rules(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(item => (item.GetProperty("rule_id").GetString()!, item.GetProperty("severity").GetString()!))];
    }

    private static HashSet<string> Ids(string json)
    {
        return new(Rules(json).Select(rule => rule.Id), StringComparer.Ordinal);
    }

    private static (int Code, string Error) RunError(string path)
    {
        using StringWriter output = new();
        using StringWriter error = new() { NewLine = "\n" };
        int code = CliRunner.Run(["validate", path], output, error, Source);
        return (code, error.ToString());
    }

    private string Mutated(Action<JsonNode> mutate)
    {
        JsonNode document = JsonNode.Parse(File.ReadAllText(Bane))!;
        mutate(document);
        string path = Path.Combine(scratch, "mutated.mbd");
        File.WriteAllText(path, document.ToJsonString());
        return path;
    }
}
