using CohEngine.Core;
using CohEngine.Core.Data;
using CohEngine.Core.Registry;
using CohEngine.TestSupport;

namespace CohEngine.Cli.Tests;

public sealed class ValidateExpectTests
{
    private const string Clean = "shield_scrapper_noslots";

    private static readonly FixtureStore Store = FixtureStore.Locate(AppContext.BaseDirectory);

    private static string BuildDirectory => Path.Join(Store.MidsDirectory, "builds", Clean);

    private static (int Code, string Output, string Error) Run(params string[] args)
    {
        using StringWriter output = new();
        using StringWriter error = new();
        int code = CliRunner.Run(args, output, error);
        return (code, output.ToString().ReplaceLineEndings("\n"), error.ToString().ReplaceLineEndings("\n"));
    }

    private static string Metric(string name)
    {
        ComputedBuild build = DumpScoring.Compute(BuildDirectory, Store.MidsDirectory);
        return build.Metrics.Single(metric => string.Equals(metric.Name, name, StringComparison.Ordinal)).Value.ToPython();
    }

    [Fact]
    public void A_claim_that_holds_to_its_own_decimal_places_passes()
    {
        double actual = double.Parse(Metric("resist_min"), System.Globalization.CultureInfo.InvariantCulture);
        string claim = Math.Round(actual, 1, MidpointRounding.AwayFromZero).ToString("F1", System.Globalization.CultureInfo.InvariantCulture);

        (int code, string output, string error) = Run("validate", BuildDirectory, "--expect", "resist_min=" + claim);

        Assert.Equal((0, "no violations\n", string.Empty), (code, output, error));
    }

    [Fact]
    public void A_flag_claim_that_holds_passes_in_the_equals_form()
    {
        Assert.Equal((0, "no violations\n"), Run("validate", BuildDirectory, "--expect=perma_hasten=false") is (var code, var output, _) ? (code, output) : default);
    }

    [Fact]
    public void A_claim_that_differs_prints_one_diagnostic_and_exits_1()
    {
        (int code, string output, string error) = Run("validate", BuildDirectory, "--expect", "resist_min=99.99");

        string expected = $"{BuildDirectory}:1:1: error: P-EXPECT-001: E160: resist_min is {Metric("resist_min")}, expected 99.99; correct the claim, or change the build until the metric holds the claimed value\n";
        Assert.Equal((1, expected, string.Empty), (code, output, error));
    }

    [Fact]
    public void Each_mismatched_metric_gets_its_own_diagnostic()
    {
        (int code, string output, _) = Run("validate", BuildDirectory, "--expect", "resist_min=99.99", "--expect", "defense_positional_min=88.8", "--expect", "perma_dom=false");

        string[] lines = output.TrimEnd('\n').Split('\n');
        Assert.Equal(1, code);
        Assert.Equal(2, lines.Length);
        Assert.Contains("resist_min is", lines[0], StringComparison.Ordinal);
        Assert.Contains("defense_positional_min is", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void A_flag_mismatch_reads_true_or_false()
    {
        (int code, string output, _) = Run("validate", BuildDirectory, "--expect", "perma_hasten=true");

        Assert.Equal(1, code);
        Assert.Contains("E160: perma_hasten is False, expected true; ", output, StringComparison.Ordinal);
    }

    [Fact]
    public void The_json_form_carries_the_expected_and_actual_values()
    {
        (int code, string output, _) = Run("validate", BuildDirectory, "--format", "json", "--expect", "resist_min=99.99");

        Assert.Equal(1, code);
        Assert.Contains("\"rule_id\": \"P-EXPECT-001\"", output, StringComparison.Ordinal);
        Assert.Contains("\"expected\": 99.99", output, StringComparison.Ordinal);
        Assert.Contains("\"actual\": " + Metric("resist_min"), output, StringComparison.Ordinal);
    }

    [Fact]
    public void An_unknown_metric_is_an_input_error()
    {
        (int code, string output, string error) = Run("validate", BuildDirectory, "--expect", "bogus=1");

        Assert.Equal((1, string.Empty), (code, output));
        Assert.StartsWith("error: E161: unknown scoring metric 'bogus' in --expect; use one of: defense_positional_min, perma_dom, perma_hasten, resist_min", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("resist_min=abc", "E162: --expect resist_min=abc is not a number; resist_min is a numeric metric")]
    [InlineData("perma_dom=maybe", "E162: --expect perma_dom=maybe is not true or false; perma_dom is a flag metric")]
    public void A_value_that_does_not_fit_its_metric_is_an_input_error(string claim, string message)
    {
        (int code, _, string error) = Run("validate", BuildDirectory, "--expect", claim);

        Assert.Equal((1, "error: " + message + "\n"), (code, error));
    }

    [Theory]
    [InlineData("resist_min")]
    [InlineData("=1")]
    [InlineData("resist_min=")]
    public void A_claim_without_a_metric_and_a_value_is_a_usage_error(string claim)
    {
        (int code, _, string error) = Run("validate", BuildDirectory, "--expect", claim);

        Assert.Equal(2, code);
        Assert.EndsWith($"coh-engine validate: error: argument --expect: expected metric=value, got '{claim}'\n", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_dangling_expect_is_a_usage_error()
    {
        (int code, _, string error) = Run("validate", BuildDirectory, "--expect");

        Assert.Equal(2, code);
        Assert.EndsWith("coh-engine validate: error: argument --expect: expected one argument\n", error, StringComparison.Ordinal);
    }

    [Fact]
    public void A_mbd_file_cannot_be_checked_against_a_claim()
    {
        BuildFixture fixture = Store.Build(Clean);
        FixedSource source = new(Store.MidsDirectory, new MbdBuild(fixture.Powers, fixture.Slots.ToDictionary(entry => entry.BuildIndex, entry => entry.Slots), new MbdFormat(new string[8], "49", new Dictionary<int, (int Min, int Max)>())));
        using StringWriter output = new();
        using StringWriter error = new();

        int code = CliRunner.Run(["validate", "x.mbd", "--expect", "resist_min=1"], output, error, source);

        Assert.Equal(1, code);
        Assert.Equal("error: E163: cannot compute metrics for x.mbd: --expect needs a Mids dump directory, not a .mbd file; validate a dump directory instead" + Environment.NewLine, error.ToString());
    }

    [Fact]
    public void The_help_line_names_the_new_command_and_option()
    {
        (int code, string output, _) = Run("--help");

        Assert.Equal(0, code);
        Assert.Contains("mcp", output, StringComparison.Ordinal);
        Assert.Contains("--expect metric=value", output, StringComparison.Ordinal);
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
