using System.Text.Json;

using CohEngine.Core;
using CohEngine.Core.Data;
using CohEngine.Core.Diagnostics;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

public sealed class ValidateCommandTests
{
    private const string Build = "shield_scrapper_set_bonuses";

    private static readonly FixtureStore Store = FixtureStore.Locate(AppContext.BaseDirectory);

    private static string BuildDirectory => Path.Join(Store.MidsDirectory, "builds", Build);

    private static (int Code, string Output, string Error) Run(IMbdBuildSource? source, params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int code = CliRunner.Run(args, output, error, source);
        return (code, output.ToString().ReplaceLineEndings("\n"), error.ToString().ReplaceLineEndings("\n"));
    }

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        return Run(null, args);
    }

    private static (int Code, string Output, string Error) RunThrowing(Exception exception)
    {
        return Run(new ThrowingSource(exception), "validate", "x.mbd", "--mids-dir", Store.MidsDirectory);
    }

    public static TheoryData<string[], string> BadCommandLines()
    {
        return new()
        {
            { ["validate"], "the following arguments are required: build_dir" },
            { ["validate", "a", "b"], "unrecognized arguments: b" },
            { ["validate", "a", "--bogus"], "unrecognized arguments: --bogus" },
            { ["validate", "a", "--format"], "argument --format: expected one argument" },
            { ["validate", "a", "--mids-dir"], "argument --mids-dir: expected one argument" },
            { ["validate", "a", "--format", "xml"], "argument --format: invalid choice: 'xml' (choose from 'text', 'json')" },
            { ["validate", "a", "--format=yaml"], "argument --format: invalid choice: 'yaml' (choose from 'text', 'json')" },
        };
    }

    [Theory]
    [MemberData(nameof(BadCommandLines))]
    public void A_bad_validate_command_line_is_a_usage_error(string[] args, string problem)
    {
        (int code, string output, string error) = Run(args);

        Assert.Equal((2, string.Empty, true), (code, output, error.EndsWith($"coh-engine validate: error: {problem}\n", StringComparison.Ordinal)));
    }

    [Fact]
    public void Equals_forms_of_the_options_are_accepted()
    {
        (int code, string output, _) = Run("validate", $"--mids-dir={Store.MidsDirectory}", "--format=json", BuildDirectory);

        Assert.Equal((1, true), (code, output.StartsWith("[\n  {", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_trailing_separator_on_the_build_directory_still_finds_the_shared_dumps()
    {
        Assert.Equal(1, Run("validate", BuildDirectory + Path.DirectorySeparatorChar).Code);
    }

    [Fact]
    public void Missing_dump_files_are_e72_naming_the_file()
    {
        string empty = Path.Join(Path.GetTempPath(), "coh-empty-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(empty);
        try
        {
            (int code, string output, string error) = Run("validate", empty, "--mids-dir", Store.MidsDirectory);

            Assert.Equal((1, string.Empty, true), (code, output, error.StartsWith("error: E72: cannot read ", StringComparison.Ordinal) && error.Contains("powers_effects.json: No such file or directory; check that the path exists and is readable", StringComparison.Ordinal)));
        }
        finally
        {
            Directory.Delete(empty);
        }
    }

    [Fact]
    public void A_missing_directory_is_e72_without_a_file_name()
    {
        Assert.Equal("error: E72: cannot read an input file: No such file or directory; check that the path exists and is readable\n", RunThrowing(new DirectoryNotFoundException("gone")).Error);
    }

    [Fact]
    public void A_missing_file_names_itself()
    {
        Assert.Equal("error: E72: cannot read slots.json: No such file or directory; check that the path exists and is readable\n", RunThrowing(new FileNotFoundException("gone", "slots.json")).Error);
    }

    [Fact]
    public void Another_io_failure_prints_its_own_reason()
    {
        Assert.Equal("error: E72: cannot read an input file: disk on fire; check that the path exists and is readable\n", RunThrowing(new IOException("disk on fire")).Error);
    }

    [Fact]
    public void A_denied_read_is_e72()
    {
        Assert.Equal((1, true), (RunThrowing(new UnauthorizedAccessException("denied")).Code, RunThrowing(new UnauthorizedAccessException("denied")).Error.Contains("denied", StringComparison.Ordinal)));
    }

    [Fact]
    public void An_engine_exception_prints_its_message_as_is()
    {
        Assert.Equal((1, "error: E99: rejected input\n"), RunThrowing(new EngineException("E99: rejected input")) is (var code, _, var error) ? (code, error) : default);
    }

    [Fact]
    public void An_argument_exception_loses_its_parameter_suffix()
    {
        Assert.Equal("error: E09: unknown archetype class name: 'Class_X'\n", RunThrowing(NamedArgument("Class_X")).Error);
    }

    [Fact]
    public void An_argument_exception_without_a_parameter_prints_whole()
    {
        Assert.Equal("error: E11: nothing\n", RunThrowing(new ArgumentException("E11: nothing", (string?)null)).Error);
    }

    [Fact]
    public void An_invalid_data_exception_prints_its_message()
    {
        Assert.Equal("error: H-ENH-001: x\n", RunThrowing(new InvalidDataException("H-ENH-001: x")).Error);
    }

    [Theory]
    [InlineData(typeof(KeyNotFoundException))]
    [InlineData(typeof(JsonException))]
    public void A_dump_of_the_wrong_shape_is_e72(Type exceptionType)
    {
        Exception exception = (Exception)Activator.CreateInstance(exceptionType, "missing 'Class'")!;

        Assert.Equal(
            "error: E72: an input is missing a key or holds the wrong shape (missing 'Class'); re-dump the build or check the data matches its schema\n",
            RunThrowing(exception).Error);
    }

    [Fact]
    public void A_mbd_without_a_source_is_unavailable()
    {
        (int code, string output, string error) = Run("validate", "build.mbd");

        Assert.Equal((1, string.Empty, "error: E72: cannot read build.mbd: .mbd validation is not available in this build; validate a Mids dump directory instead\n"), (code, output, error));
    }

    [Fact]
    public void A_mbd_goes_through_the_source_with_the_format_rules_armed()
    {
        BuildFixture fixture = Store.Build(Build);
        MbdFormat format = new(["a", "b"], "50", new Dictionary<int, (int Min, int Max)>());
        FixedSource source = new(Store.MidsDirectory, new MbdBuild(fixture.Powers, fixture.Slots.ToDictionary(entry => entry.BuildIndex, entry => entry.Slots), format));

        (int code, string output, _) = Run(source, "validate", "x.mbd", "--format", "json");

        Assert.Equal((1, true, true), (code, output.Contains("H-FMT-001", StringComparison.Ordinal), output.Contains("H-FMT-003", StringComparison.Ordinal)));
    }

    [Fact]
    public void A_mbd_uses_the_given_mids_directory_over_the_default()
    {
        FixedSource source = new(Path.Join(Path.GetTempPath(), "coh-not-there"), new MbdBuild([], new Dictionary<int, IReadOnlyList<SlotRef>>(), new MbdFormat(new string[8], "49", new Dictionary<int, (int Min, int Max)>())));

        Assert.Equal((0, "no violations\n"), Run(source, "validate", "x.mbd", "--mids-dir", Store.MidsDirectory) is (var code, var output, _) ? (code, output) : default);
    }

    [Fact]
    public void The_pure_proc_exemption_is_armed_through_the_effects_dump()
    {
        BuildFixture fixture = Store.Build(Build);
        Dictionary<int, IReadOnlyList<SlotRef>> slots = fixture.Slots.ToDictionary(entry => entry.BuildIndex, entry => entry.Slots);
        IReadOnlyDictionary<int, EnhancementLegality> enhancements = EnhancementLegality.Load(Path.Join(Store.MidsDirectory, "enhancements.json"));

        int armed = LegalityRunner.Check(fixture.Powers, slots, enhancements, Store.SetBonuses, Store.EnhancementEffects).Count(IsLoneSetPiece);
        int bare = LegalityRunner.Check(fixture.Powers, slots, enhancements, Store.SetBonuses).Count(IsLoneSetPiece);
        int viaCommand = ValidateCommand.RunDump(BuildDirectory, Store.MidsDirectory).Count(IsLoneSetPiece);

        Assert.Equal((armed, true), (viaCommand, bare >= armed));
    }

    [Fact]
    public void An_empty_command_line_prints_the_status_banner()
    {
        (int code, string output, _) = Run();

        Assert.Equal((0, "coh_engine: MidsReborn build-math port.\nSpec: docs/engine/mids-port-spec.md. Commands: rules, validate, chain (score lands with the build-eval pipeline).\n"), (code, output));
    }

    [Fact]
    public void The_command_refuses_null_arguments()
    {
        Assert.Throws<ArgumentNullException>(() => ValidateCommand.RunDump(null!, Store.MidsDirectory));
        Assert.Throws<ArgumentNullException>(() => ValidateCommand.RunDump(BuildDirectory, null!));
        Assert.Throws<ArgumentNullException>(() => ValidateCommand.RunMbd(null!, "x.mbd", Store.MidsDirectory));
    }

    private static ArgumentException NamedArgument(string path)
    {
        return new ArgumentException($"E09: unknown archetype class name: '{path}'", nameof(path));
    }

    private static bool IsLoneSetPiece(Diagnostic diagnostic)
    {
        return string.Equals(diagnostic.RuleId, "P-SET-001", StringComparison.Ordinal);
    }

    private sealed class ThrowingSource(Exception exception) : IMbdBuildSource
    {
        public string DefaultMidsDirectory => string.Empty;

        public MbdBuild Load(string path, string midsDirectory)
        {
            throw exception;
        }
    }

    private sealed class FixedSource(string defaultMids, MbdBuild build) : IMbdBuildSource
    {
        public string DefaultMidsDirectory => defaultMids;

        public MbdBuild Load(string path, string midsDirectory)
        {
            return build;
        }
    }
}
