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
    private static readonly string Tanker = Path.Join(Capture.Store.MidsDirectory, "builds", "wpss_tanker_leadership");
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
                .Select(slot => slot!["Enhancement"])
                .OfType<JsonObject>()
                .First(candidate => Mbd.IoLevelBounds(candidate["Uid"]!.GetValue<string>(), database) is { Max: < 49 });
            enhancement["IoLevel"] = 50;
        });

        Assert.Contains("H-FMT-002", Ids(CliCapture.Run(Source, "validate", path, "--format", "json").Output));
    }

    // Each message and exit code is the Python engine's output for the same file, captured once at commit 1adeb627.
    [Theory]
    [InlineData("\"50\"", "'50'", 1)]
    [InlineData("5", "5", 1)]
    [InlineData("-1", "-1", 1)]
    [InlineData("5.5", "5.5", 1)]
    [InlineData("1e30", "1e+30", 1)]
    [InlineData("12345678901234567890", "12345678901234567890", 1)]
    [InlineData("true", "True", 1)]
    [InlineData("false", "False", 1)]
    public void A_header_level_is_judged_as_written(string token, string shown, int exitCode)
    {
        string path = WithLevel(token);

        (string output, int code) = CliCapture.Run(Source, "validate", path, "--format", "json");

        Assert.Equal((exitCode, $"the header Level {shown} is not a string of an integer from 0 to 49"), (code, Rules(output).Single(rule => string.Equals(rule.Id, "H-FMT-003", StringComparison.Ordinal)).Message));
    }

    [Fact]
    public void A_string_level_in_range_is_not_reported()
    {
        Assert.DoesNotContain("H-FMT-003", Ids(CliCapture.Run(Source, "validate", WithLevel("\"5\""), "--format", "json").Output));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("\"abc\"")]
    [InlineData("\"5.5\"")]
    public void A_level_that_is_not_an_integer_is_e118(string token)
    {
        Assert.Equal((1, true), RunErrorStartsWith(WithLevel(token), "error: E118: "));
    }

    [Fact]
    public void A_missing_level_key_is_e118()
    {
        string path = Mutated(document => document.AsObject().Remove("Level"));

        Assert.Equal((1, true), RunErrorStartsWith(path, "error: E118: "));
    }

    [Fact]
    public void A_missing_built_with_block_is_e118()
    {
        string path = Mutated(document => document.AsObject().Remove("BuiltWith"));

        Assert.Equal((1, true), RunErrorStartsWith(path, "error: E118: "));
    }

    [Fact]
    public void A_default_directory_without_dumps_names_the_directory_and_the_flag()
    {
        string missing = Path.Join(scratch, "no-dumps");
        MbdBuildSource source = new(missing);

        using StringWriter output = new();
        using StringWriter error = new() { NewLine = "\n" };
        int code = CliRunner.Run(["validate", Bane], output, error, source);

        Assert.Equal((1, $"error: E72: cannot read the default Mids dump directory {missing}, which holds no powers.json; pass --mids-dir <dir> with the shared dumps\n"), (code, error.ToString()));
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
        string path = Path.Join(scratch, "broken.mbd");
        File.WriteAllText(path, "{ not json");

        (int code, string error) = RunError(path);

        Assert.Equal((1, true), (code, error.StartsWith("error: E118: ", StringComparison.Ordinal)));
    }

    [Fact]
    public void The_default_mids_directory_is_the_repositorys_dump()
    {
        string nowhere = Directory.CreateDirectory(Path.Join(scratch, "empty")).FullName;

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
        MbdBuildSource source = new(Path.Join(scratch, "not-there"));

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

    private static List<(string Id, string Severity, string Message)> Rules(string json)
    {
        using JsonDocument document = JsonDocument.Parse(json);
        return [.. document.RootElement.EnumerateArray().Select(item => (item.GetProperty("rule_id").GetString()!, item.GetProperty("severity").GetString()!, item.GetProperty("message").GetString()!))];
    }

    private static (int Code, bool Matches) RunErrorStartsWith(string path, string prefix)
    {
        (int code, string error) = RunError(path);
        return (code, error.StartsWith(prefix, StringComparison.Ordinal));
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
        string path = Path.Join(scratch, "mutated.mbd");
        File.WriteAllText(path, document.ToJsonString());
        return path;
    }

    // The token is written as given, so a number keeps the form the file holds.
    private string WithLevel(string token)
    {
        string path = Mutated(document => document["Level"] = "@@");
        File.WriteAllText(path, File.ReadAllText(path).Replace("\"@@\"", token, StringComparison.Ordinal));
        return path;
    }
}
